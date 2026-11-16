using System.Collections.Concurrent;
using System.IO;
using ManagedBass;
using ManagedBass.Mix;

namespace Bronxamp.Zvuk;

public sealed record ZvukStavka(string Putanja, object? Oznaka = null);

public enum ZvukStanje { Stoji, Svira, Pauza }

public sealed record IzlazniUredjaj(int Broj, string Naziv, bool Podrazumevani);

/// <summary>
/// Playback engine. All tracks go through one BASSmix mixer in queue mode: a queued source starts on the
/// exact sample after the previous one ends, which is what makes playback gapless. Crossfade briefly
/// switches the queue off so the next source starts early, with volume envelopes on both.
///
/// The mix-time callbacks run on BASS's mixing thread while it holds the mixer lock. They must not wait on
/// anything the UI thread holds around a BASS call, so shared state here is lock-free (Interlocked).
/// </summary>
public sealed class ZvukPlejer : IDisposable
{
    // Not exposed by ManagedBass (values from bassmix.h).
    private const BassFlags MikserRed = (BassFlags)0x8000;
    private const BassFlags IzvorAutoBrisanje = (BassFlags)0x40000;
    private const SyncFlags SyncMikserRed = (SyncFlags)0x10202;

    private const int BaferMs = 300;
    private const int Tacaka = 9;

    private sealed class Kanal(int ruckica, ZvukStavka stavka, double trajanje)
    {
        public int Ruckica { get; } = ruckica;
        public ZvukStavka Stavka { get; } = stavka;
        public double Trajanje { get; } = trajanje;
        public double Prelaz { get; set; }
    }

    private readonly ConcurrentDictionary<int, Kanal> _kanali = new();
    private readonly bool _dekodiranje;

    // Delegates are stored so the GC cannot collect them while BASS holds the pointers.
    private readonly SyncProcedure _naRedMix;
    private readonly SyncProcedure _naRedCuje;
    private readonly SyncProcedure _naKrajMiksera;
    private readonly SyncProcedure _naPrelaz;
    private readonly SyncProcedure _naPrelazCuje;

    private int _mikser;
    private int _limiter;   // limiter on the mixer (0 = none)

    private int _uredjaj;
    private int _posSync;
    private int _posSyncCuje;
    private int _posSyncKanal;
    private Kanal? _cuje;    // what the listener hears right now
    private Kanal? _mix;     // what the mixer is decoding (up to one buffer ahead of _cuje)
    private Kanal? _uRedu;   // queued, not started yet
    private Kanal? _ulazeci; // started by a crossfade, not audible yet
    private int _odlazeci;   // the track fading out during a crossfade
    private int _generacija;
    private int _generacijaSledece;
    private int _ucitavanje;
    private int _ucitavaTekucu;
    private volatile bool _pauzaPosleUcitavanja;
    private double _poslednjaPozicija;
    private float _jacina = 1f;

    public ZvukStanje Stanje { get; private set; } = ZvukStanje.Stoji;

    /// <summary>The audible track changed. Called off the UI thread.</summary>
    public event Action<ZvukStavka>? PesmaPocela;
    /// <summary>The last track has finished. Called off the UI thread.</summary>
    public event Action? RedZavrsen;
    /// <summary>A file could not be opened. Called off the UI thread.</summary>
    public event Action<ZvukStavka, string>? NijeOtvoreno;

    public ZvukPlejer(bool dekodiranje = false)
    {
        _dekodiranje = dekodiranje;
        _naRedMix = NaRedMix;
        _naRedCuje = NaRedCuje;
        _naKrajMiksera = NaKrajMiksera;
        _naPrelaz = NaPrelaz;
        _naPrelazCuje = NaPrelazCuje;
    }

    public int Mikser => _mikser;

    /// <summary>Sample rate of the mixer (= the sound card's rate). The encoder must be told this one.</summary>
    public int Frekvencija { get; private set; } = 44100;

    public double Trajanje => Volatile.Read(ref _cuje)?.Trajanje ?? 0;
    public ZvukStavka? Tekuca => Volatile.Read(ref _cuje)?.Stavka;

    /// <summary>A song asked for with PustiAsync is still being opened.</summary>
    public bool UcitavaSe => Volatile.Read(ref _ucitavaTekucu) > 0;
    public bool PauzaNaCekanju => _pauzaPosleUcitavanja;

    public static IReadOnlyList<IzlazniUredjaj> Uredjaji()
    {
        var spisak = new List<IzlazniUredjaj>();
        for (int i = 1; Bass.GetDeviceInfo(i, out var info); i++)
            if (info.IsEnabled)
                spisak.Add(new IzlazniUredjaj(i, info.Name, info.IsDefault));
        return spisak;
    }

    /// <param name="uredjaj">BASS device number: -1 default, 0 no sound (used by tests).</param>
    public bool Pokreni(int uredjaj = -1)
    {
        Bass.Configure(Configuration.IncludeDefaultDevice, true);
        if (!Bass.Init(uredjaj, 44100, DeviceInitFlags.Default, IntPtr.Zero, IntPtr.Zero) && Bass.LastError != Errors.Already)
        {
            Dnevnik.Upisi($"BASS_Init({uredjaj}): {Bass.LastError}");
            if (uredjaj == 0 || !Bass.Init(0, 44100, DeviceInitFlags.Default, IntPtr.Zero, IntPtr.Zero))
                return false;
        }

        foreach (var dodatak in new[] { "bassflac.dll", "bassopus.dll" })
            if (Bass.PluginLoad(Path.Combine(AppContext.BaseDirectory, dodatak)) == 0 && Bass.LastError != Errors.Already)
                Dnevnik.Upisi($"Dodatak {dodatak}: {Bass.LastError}");

        _uredjaj = Bass.CurrentDevice;
        Bass.PlaybackBufferLength = BaferMs;
        Bass.GetInfo(out var info);
        Frekvencija = info.SampleRate > 0 ? info.SampleRate : 44100;

        var zastavice = BassFlags.Float | MikserRed | BassFlags.MixerEnd;
        if (_dekodiranje) zastavice |= BassFlags.Decode;
        _mikser = BassMix.CreateMixerStream(Frekvencija, 2, zastavice);
            _limiter = Limiter.Postavi(_mikser);

        if (_mikser == 0)
        {
            Dnevnik.Upisi($"Mikser: {Bass.LastError}");
            return false;
        }

        Bass.ChannelSetSync(_mikser, SyncMikserRed | SyncFlags.Mixtime, 0, _naRedMix);
        Bass.ChannelSetSync(_mikser, SyncMikserRed, 0, _naRedCuje);
        Bass.ChannelSetSync(_mikser, SyncFlags.End, 0, _naKrajMiksera);
        Bass.ChannelSetAttribute(_mikser, ChannelAttribute.Volume, _jacina);
        return true;
    }

    public bool PromeniUredjaj(int uredjaj)
    {
        if (!Bass.Init(uredjaj, 44100, DeviceInitFlags.Default, IntPtr.Zero, IntPtr.Zero) && Bass.LastError != Errors.Already)
            return false;
        if (!Bass.ChannelSetDevice(_mikser, uredjaj)) return false;
        _uredjaj = uredjaj;
        return true;
    }

    public float Jacina
    {
        get => _jacina;
        set
        {
            _jacina = Math.Clamp(value, 0f, 1f);
            if (_mikser != 0) Bass.ChannelSetAttribute(_mikser, ChannelAttribute.Volume, _jacina);
        }
    }

    public double Pozicija
    {
        get
        {
            var k = Volatile.Read(ref _cuje);
            if (k == null) return 0;
            long bajtovi = BassMix.ChannelGetPosition(k.Ruckica, PositionFlags.Bytes);
            if (bajtovi >= 0) _poslednjaPozicija = Bass.ChannelBytes2Seconds(k.Ruckica, bajtovi);
            return _poslednjaPozicija;
        }
    }

    /// <summary>Starts a track right away, dropping whatever was playing or queued.</summary>
    public async Task<bool> PustiAsync(ZvukStavka stavka)
    {
        int generacija = Interlocked.Increment(ref _generacija);
        Interlocked.Increment(ref _generacijaSledece);

        if (Interlocked.Increment(ref _ucitavaTekucu) == 1) _pauzaPosleUcitavanja = false;
        try
        {
            var novi = await Task.Run(() => Otvori(stavka));
            if (generacija != Volatile.Read(ref _generacija))
            {
                if (novi != null) Oslobodi(novi.Ruckica);
                return false;
            }
            if (novi == null) return false;
            return Zameni(novi);
        }
        finally
        {
            Interlocked.Decrement(ref _ucitavaTekucu);
        }
    }

    private bool Zameni(Kanal novi)
    {
        // The mixer is empty for a moment here, and with this flag on it would report the end of the queue.
        Bass.ChannelFlags(_mikser, 0, BassFlags.MixerEnd);
        try
        {
            OcistiSve(novi.Ruckica);
            if (!BassMix.MixerAddChannel(_mikser, novi.Ruckica, IzvorAutoBrisanje | BassFlags.MixerChanDownMix))
            {
                Dnevnik.Upisi($"Dodavanje u mikser: {Bass.LastError}");
                Oslobodi(novi.Ruckica);
                return false;
            }

            Volatile.Write(ref _mix, novi);
            Volatile.Write(ref _cuje, novi);
            _poslednjaPozicija = 0;

            if (_pauzaPosleUcitavanja)
            {
                // Pause was pressed while the file was opening: the song waits at its start.
                Bass.ChannelStop(_mikser);
                Bass.ChannelSetPosition(_mikser, 0);
                _pauzaPosleUcitavanja = false;
                Stanje = ZvukStanje.Pauza;
                return true;
            }

            if (!_dekodiranje) Bass.ChannelPlay(_mikser, true);
            Stanje = ZvukStanje.Svira;
            return true;
        }
        finally
        {
            Bass.ChannelFlags(_mikser, BassFlags.MixerEnd, BassFlags.MixerEnd);
        }
    }

    /// <summary>
    /// Sets what follows the current track. Opens the file in the background and queues it, so the switch
    /// itself does no disk work. null clears the queue.
    /// </summary>
    public void PostaviSledecu(ZvukStavka? stavka, double prelaz)
    {
        int generacija = Interlocked.Increment(ref _generacijaSledece);

        var uRedu = Volatile.Read(ref _uRedu);
        if (uRedu != null && stavka != null && uRedu.Stavka == stavka && Math.Abs(uRedu.Prelaz - prelaz) < 0.01)
            return;

        UkloniIzReda();
        if (stavka == null) return;

        Interlocked.Increment(ref _ucitavanje);
        Task.Run(() =>
        {
            try
            {
                var novi = Otvori(stavka);
                if (novi == null) return;
                if (generacija != Volatile.Read(ref _generacijaSledece))
                {
                    Oslobodi(novi.Ruckica);
                    return;
                }
                StaviURed(novi, prelaz);
            }
            catch (Exception ex)
            {
                Dnevnik.Greska("PostaviSledecu", ex);
            }
            finally
            {
                Interlocked.Decrement(ref _ucitavanje);
            }
        });
    }

    private void StaviURed(Kanal novi, double prelaz)
    {
        if (!BassMix.MixerAddChannel(_mikser, novi.Ruckica,
                IzvorAutoBrisanje | BassFlags.MixerChanDownMix | BassFlags.MixerChanNoRampin))
        {
            Dnevnik.Upisi($"Red miksera: {Bass.LastError}");
            Oslobodi(novi.Ruckica);
            return;
        }

        var tekuci = Volatile.Read(ref _mix);
        if (prelaz > 0 && tekuci != null)
            novi.Prelaz = Math.Min(prelaz, Math.Min(tekuci.Trajanje, novi.Trajanje) / 2);
        Volatile.Write(ref _uRedu, novi);

        if (novi.Prelaz > 0 && tekuci != null)
        {
            long pozicija = Bass.ChannelSeconds2Bytes(tekuci.Ruckica, tekuci.Trajanje - novi.Prelaz);
            _posSyncKanal = tekuci.Ruckica;
            _posSync = Bass.ChannelSetSync(tekuci.Ruckica, SyncFlags.Position | SyncFlags.Mixtime | SyncFlags.Onetime,
                pozicija, _naPrelaz);
            _posSyncCuje = BassMix.ChannelSetSync(tekuci.Ruckica, SyncFlags.Position | SyncFlags.Onetime,
                pozicija, _naPrelazCuje);
        }

        // The current track may have ended while this one was still loading.
        if (Stanje == ZvukStanje.Svira && !_dekodiranje && Bass.ChannelIsActive(_mikser) == PlaybackState.Stopped)
            Bass.ChannelPlay(_mikser, true);
    }

    private void UkloniIzReda()
    {
        if (_posSync != 0)
        {
            Bass.ChannelRemoveSync(_posSyncKanal, _posSync);
            BassMix.ChannelRemoveSync(_posSyncKanal, _posSyncCuje);
            _posSync = 0;
            _posSyncCuje = 0;
        }
        var stari = Interlocked.Exchange(ref _uRedu, null);
        if (stari != null) Oslobodi(stari.Ruckica);
    }

    public void Pauza()
    {
        if (UcitavaSe) _pauzaPosleUcitavanja = true;
        if (Stanje != ZvukStanje.Svira) return;
        Bass.ChannelPause(_mikser);
        Stanje = ZvukStanje.Pauza;
    }

    public void Nastavi()
    {
        _pauzaPosleUcitavanja = false;
        if (Stanje != ZvukStanje.Pauza) return;
        Bass.ChannelPlay(_mikser, false);
        Stanje = ZvukStanje.Svira;
    }

    public void Zaustavi()
    {
        Interlocked.Increment(ref _generacija);
        Interlocked.Increment(ref _generacijaSledece);
        OcistiSve();
        Bass.ChannelStop(_mikser);
        Stanje = ZvukStanje.Stoji;
    }

    public void Premotaj(double sekunde)
    {
        var k = Volatile.Read(ref _cuje);
        if (k == null || k != Volatile.Read(ref _mix)) return;

        int odlazeci = Interlocked.Exchange(ref _odlazeci, 0);
        if (odlazeci != 0)
        {
            Oslobodi(odlazeci);
            BassMix.ChannelSetEnvelope(k.Ruckica, MixEnvelope.Volume, [], 0);
        }

        sekunde = Math.Clamp(sekunde, 0, Math.Max(0, k.Trajanje - 0.05));
        long bajtovi = Bass.ChannelSeconds2Bytes(k.Ruckica, sekunde);
        BassMix.ChannelSetPosition(k.Ruckica, bajtovi, PositionFlags.Bytes | PositionFlags.MixerReset);
        _poslednjaPozicija = sekunde;
    }

    /// <summary>Fills the array with an FFT of what is currently audible. Returns false when nothing plays.</summary>
    public bool Spektar(float[] podaci)
    {
        if (_mikser == 0 || Stanje != ZvukStanje.Svira) return false;
        return Bass.ChannelGetData(_mikser, podaci, (int)DataFlags.FFT4096) > 0;
    }

    /// <summary>
    /// Level of what is currently audible (0..1 per channel, peak of the last 20 ms).
    /// False when nothing plays or the mixer is not on a device (encode-only mode).
    /// </summary>
    public bool Nivo(out double levo, out double desno)
    {
        levo = desno = 0;
        if (_mikser == 0 || Stanje != ZvukStanje.Svira) return false;
        int paket = Bass.ChannelGetLevel(_mikser);
        if (paket < 0) return false;
        levo = Math.Clamp((paket & 0xFFFF) / 32768.0, 0, 1);
        desno = Math.Clamp(((paket >> 16) & 0xFFFF) / 32768.0, 0, 1);
        return true;
    }

    /// <summary>
    /// The length the decoder really plays. Tags can be far off: on the radio's own jingles by more than a
    /// minute, which is why a day is planned with this and not with the tag. Zero if the file will not open.
    /// </summary>
    public double Izmeri(string putanja)
    {
        Bass.CurrentDevice = _uredjaj;
        int ruckica = Bass.CreateStream(putanja, 0, 0, BassFlags.Decode | BassFlags.Float | BassFlags.Prescan);
        if (ruckica == 0) return 0;
        double trajanje = Bass.ChannelBytes2Seconds(ruckica, Bass.ChannelGetLength(ruckica));
        Bass.StreamFree(ruckica);
        return trajanje;
    }

    private Kanal? Otvori(ZvukStavka stavka)
    {
        // BASS keeps the device per thread, and a pool thread may remember one that no longer exists.
        Bass.CurrentDevice = _uredjaj;
        int ruckica = Bass.CreateStream(stavka.Putanja, 0, 0, BassFlags.Decode | BassFlags.Float | BassFlags.Prescan);
        if (ruckica == 0)
        {
            var greska = Bass.LastError.ToString();
            Dnevnik.Upisi($"Ne mogu da otvorim \"{stavka.Putanja}\": {greska}");
            NijeOtvoreno?.Invoke(stavka, greska);
            return null;
        }
        double trajanje = Bass.ChannelBytes2Seconds(ruckica, Bass.ChannelGetLength(ruckica));
        var kanal = new Kanal(ruckica, stavka, trajanje);
        _kanali[ruckica] = kanal;
        return kanal;
    }

    private void Oslobodi(int ruckica)
    {
        _kanali.TryRemove(ruckica, out _);
        Bass.StreamFree(ruckica);
    }

    private void OcistiSve(int osim = 0)
    {
        _posSync = 0;
        _posSyncCuje = 0;
        Volatile.Write(ref _uRedu, null);
        Volatile.Write(ref _mix, null);
        Volatile.Write(ref _cuje, null);
        Volatile.Write(ref _ulazeci, null);
        Volatile.Write(ref _odlazeci, 0);
        foreach (var ruckica in _kanali.Keys.ToArray())
            if (ruckica != osim)
                Oslobodi(ruckica);
    }

    // ---- callbacks: mixing thread ----

    private void NaRedMix(int sync, int kanal, int podatak, IntPtr korisnik)
    {
        var uRedu = Volatile.Read(ref _uRedu);
        if (uRedu != null && uRedu.Ruckica == podatak &&
            Interlocked.CompareExchange(ref _uRedu, null, uRedu) == uRedu)
            Volatile.Write(ref _mix, uRedu);
    }

    private void NaPrelaz(int sync, int kanal, int podatak, IntPtr korisnik)
    {
        var sledeci = Volatile.Read(ref _uRedu);
        var tekuci = Volatile.Read(ref _mix);
        if (sledeci == null || tekuci == null || sledeci.Prelaz <= 0) return;
        if (Interlocked.CompareExchange(ref _uRedu, null, sledeci) != sledeci) return;

        // BASS reports no dequeue for a source released this way, so the handover is recorded here.
        Volatile.Write(ref _mix, sledeci);
        Volatile.Write(ref _ulazeci, sledeci);
        Volatile.Write(ref _odlazeci, tekuci.Ruckica);
        BassMix.ChannelSetEnvelope(tekuci.Ruckica, MixEnvelope.Volume, Omotnica(sledeci.Prelaz, false), Tacaka);
        BassMix.ChannelSetEnvelope(sledeci.Ruckica, MixEnvelope.Volume, Omotnica(sledeci.Prelaz, true), Tacaka);

        // With the queue off, every queued source starts now.
        Bass.ChannelFlags(_mikser, 0, MikserRed);
        Bass.ChannelFlags(_mikser, MikserRed, MikserRed);
    }

    /// <summary>Equal-power curve, so the combined loudness does not dip in the middle of the crossfade.</summary>
    private MixerNode[] Omotnica(double sekunde, bool ulaz)
    {
        var tacke = new MixerNode[Tacaka];
        for (int i = 0; i < Tacaka; i++)
        {
            double t = i / (double)(Tacaka - 1);
            tacke[i] = new MixerNode
            {
                Position = Bass.ChannelSeconds2Bytes(_mikser, sekunde * t),
                Value = (float)(ulaz ? Math.Sin(t * Math.PI / 2) : Math.Cos(t * Math.PI / 2)),
            };
        }
        return tacke;
    }

    // ---- callbacks: sync thread, when the event is actually heard ----

    private void NaRedCuje(int sync, int kanal, int podatak, IntPtr korisnik)
    {
        if (!_kanali.TryGetValue(podatak, out var novi) || novi == Volatile.Read(ref _cuje)) return;
        Volatile.Write(ref _cuje, novi);
        _poslednjaPozicija = 0;
        PesmaPocela?.Invoke(novi.Stavka);
    }

    private void NaPrelazCuje(int sync, int kanal, int podatak, IntPtr korisnik)
    {
        var novi = Interlocked.Exchange(ref _ulazeci, null);
        if (novi == null || novi == Volatile.Read(ref _cuje)) return;
        Volatile.Write(ref _cuje, novi);
        _poslednjaPozicija = 0;
        PesmaPocela?.Invoke(novi.Stavka);
    }

    private void NaKrajMiksera(int sync, int kanal, int podatak, IntPtr korisnik)
    {
        int generacija = Volatile.Read(ref _generacija);
        _ = Task.Run(() => ZavrsiRed(generacija));
    }

    /// <summary>
    /// The end call arrives with a delay, so a new song may already have started, and it can also come a
    /// moment before BASS reports the mixer as stopped. Hence a few looks instead of a single one.
    /// </summary>
    private async Task ZavrsiRed(int generacija)
    {
        for (int pokusaj = 0; pokusaj < 12; pokusaj++)
        {
            // A slow disk can still be opening the next track; StaviURed restarts the mixer in that case.
            if (generacija != Volatile.Read(ref _generacija) || Volatile.Read(ref _ucitavanje) > 0 ||
                Volatile.Read(ref _uRedu) != null || UcitavaSe)
                return;

            if (Bass.ChannelIsActive(_mikser) == PlaybackState.Stopped)
            {
                Stanje = ZvukStanje.Stoji;
                Volatile.Write(ref _cuje, null);
                Volatile.Write(ref _mix, null);
                RedZavrsen?.Invoke();
                return;
            }
            await Task.Delay(50);
        }
    }

    public void Dispose()
    {
        if (_mikser != 0)
        {
            OcistiSve();
            Bass.StreamFree(_mikser);
            _mikser = 0;
        }
    }

    /// <summary>Releases the audio device. Once, at program exit: BASS remembers the device per thread.</summary>
    public static void Ugasi() => Bass.Free();
}
