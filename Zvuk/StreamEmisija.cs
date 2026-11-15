using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using ManagedBass;
using ManagedBass.Enc;

namespace Bronxamp.Zvuk;

public enum StreamStanje { Iskljucen, Povezuje, NaEtru, Greska }

/// <summary>
/// Sends the mixer output to an Icecast (Zeno) server as MP3 and reports the state.
/// The encoder (lame.exe) reads the mixer; BASSenc pushes that to the server and updates the song title.
/// The connection is watched: when it drops, the state says so and the program gets back on by itself.
/// </summary>
public sealed class StreamEmisija : IDisposable
{
    /// <summary>Waiting between attempts to get back on the server, in seconds; the last one repeats.</summary>
    private static readonly int[] Pauze = [2, 5, 10, 20, 30];

    private readonly ZvukPlejer _zvuk;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
    private readonly EncodeNotifyProcedure _obavestenje;   // a field: BASS must not lose the delegate
    private readonly Timer _nadzornik;
    private readonly object _katanac = new();

    private int _enkoder;
    private string _naziv = "";
    private string? _poslednjiNaslov;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<EncodeNotifyStatus, int> _obavestenja = new();
    private (string Host, int Port, string Mount, string Korisnik, string Lozinka, int Kbps) _parametri;
    private DateTime _sledeciPokusaj = DateTime.MinValue;
    private int _korak;
    private volatile bool _puklo;

    /// <summary>Local time when the user turned the cast on; midnight counts from here, not from a reconnect.</summary>
    private DateTime _ukljucenoU;

    public StreamStanje Stanje { get; private set; } = StreamStanje.Iskljucen;
    public string? Poruka { get; private set; }

    /// <summary>„127.0.0.1:8000/test.mp3“ — where it goes; the status line shows it.</summary>
    public string Adresa { get; private set; } = "";

    /// <summary>True while the user wants to be on the server, including while reconnecting.</summary>
    public bool Zeljeno { get; private set; }

    public bool JeUkljucen => _enkoder != 0;

    /// <summary>Raised on whatever thread the change happened on; the UI marshals it.</summary>
    public event Action? Promenjeno;

    public StreamEmisija(ZvukPlejer zvuk)
    {
        _zvuk = zvuk;
        _obavestenje = Obavestenje;
        _nadzornik = new Timer(Nadzor, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>True while the user wants to be on the server but nothing plays, so the stream is not opened.</summary>
    public bool CekaZvuk { get; private set; }

    /// <summary>Shown while waiting for playback; the server would drop a silent source after a few seconds.</summary>
    public const string PorukaCeka = "Čeka da nešto svira.";

    /// <summary>What the status row and the tooltip say after midnight turned the cast off.</summary>
    public const string PorukaPonoc = "Prekinuto u ponoć: dan je gotov, a na serveru nastavlja njegov Auto DJ.";

    /// <summary>True when midnight stopped the cast; it stays so until the user turns it on again.</summary>
    public bool Ponoc { get; private set; }

    /// <summary>Clock for the midnight check; tests move it forward instead of waiting for the night.</summary>
    public Func<DateTime> Sada { get; set; } = () => DateTime.Now;

    /// <summary>
    /// Midnight has passed since the cast was turned on, so the day is over: the source goes off and the
    /// server's Auto DJ takes the mount (specification, „Dva računara“ and faza 3).
    /// </summary>
    public static bool ProslaPonoc(DateTime ukljuceno, DateTime sada) =>
        ukljuceno != default && sada.Date > ukljuceno.Date;

    /// <summary>How lame is started: raw input at the mixer's own rate, so the stream is not slowed down.</summary>
    public static string LameArgumenti(string putanja, int frekvencija, int kbps) =>
        $"\"{putanja}\" -r -s {frekvencija} -b {kbps} -";

    /// <summary>Turns sending on and keeps it on: a dropped connection is repaired without the user.</summary>
    public bool Povezi(string host, int port, string mount, string korisnik, string lozinka, int kbps, string naziv)
    {
        lock (_katanac)
        {
            PrekiniEnkoder();
            _parametri = (host, port, mount, korisnik, lozinka, kbps);
            _naziv = naziv;
            Zeljeno = true;
            _ukljucenoU = Sada();
            Ponoc = false;
            _korak = 0;
            _puklo = false;
            _nadzornik.Change(2000, 2000);
            // Nothing plays: opening the stream now would only make Icecast time it out after ~15 s.
            if (_zvuk.Stanje != ZvukStanje.Svira)
            {
                CekaZvuk = true;
                Postavi(StreamStanje.Povezuje, PorukaCeka);
                return true;
            }
            CekaZvuk = false;
            return Pokreni();
        }
    }

    /// <summary>The song on air, shown to listeners. Ignored when not connected.</summary>
    public void NaslovPesme(string? izvodjac, string? naslov)
    {
        lock (_katanac)
        {
            if (_enkoder == 0 || Stanje != StreamStanje.NaEtru) return;
            var tekst = string.Join(" - ", new[] { izvodjac, naslov }.Where(s => !string.IsNullOrWhiteSpace(s)));
            // The view model refreshes the status often; sending the same title again is a needless
            // request to the server's /admin/metadata on every refresh.
            if (tekst.Length == 0 || tekst == _poslednjiNaslov) return;

            bool poslao = BassEnc.CastSetTitle(_enkoder, tekst, null);
            Dnevnik.Upisi($"Emitovanje: naslov „{tekst}“ — BASSenc {(poslao ? "poslao" : "NIJE poslao")} ({Bass.LastError})");
            _poslednjiNaslov = tekst;

            // Some servers (Zeno) do not take BASSenc's request; send it ourselves and write down the answer.
            if (!poslao) _ = PosaljiNaslovSam(tekst);
        }
    }

    /// <summary>Fallback title update over HTTP; the status code goes to the log.</summary>
    private async Task PosaljiNaslovSam(string tekst)
    {
        var (host, port, mount, korisnik, lozinka, _) = _parametri;
        var osnovni = string.IsNullOrEmpty(korisnik) ? lozinka : $"{korisnik}:{lozinka}";
        foreach (var m in Mountovi(mount))
        {
            try
            {
                var url = $"http://{host}:{port}/admin/metadata?mode=updinfo"
                          + $"&song={Uri.EscapeDataString(tekst)}&mount={Uri.EscapeDataString("/" + m)}";
                using var zahtev = new HttpRequestMessage(HttpMethod.Get, url);
                zahtev.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(osnovni)));
                using var odgovor = await Http.SendAsync(zahtev).ConfigureAwait(false);
                Dnevnik.Upisi($"Emitovanje: naslov za /{m}: HTTP {(int)odgovor.StatusCode}");
                if (odgovor.IsSuccessStatusCode) return;
            }
            catch (Exception ex)
            {
                Dnevnik.Upisi($"Emitovanje: naslov za /{m}: {ex.Message}");
            }
        }
    }

    /// <summary>The mount as given, and without a trailing „/source“ (Zeno uses it only for the source login).</summary>
    private static IEnumerable<string> Mountovi(string mount)
    {
        var m = mount.Trim('/');
        if (m.Length > 0) yield return m;
        const string sufiks = "/source";
        if (m.EndsWith(sufiks, StringComparison.OrdinalIgnoreCase))
        {
            var bez = m[..^sufiks.Length];
            if (bez.Length > 0) yield return bez;
        }
    }

    /// <summary>Turns sending off; the watch stops with it.</summary>
    public void Prekini()
    {
        lock (_katanac)
        {
            Zeljeno = false;
            CekaZvuk = false;
            _ukljucenoU = default;
            Ponoc = false;
            _nadzornik.Change(Timeout.Infinite, Timeout.Infinite);
            PrekiniEnkoder();
            Postavi(StreamStanje.Iskljucen, null);
        }
    }

    /// <summary>
    /// Midnight: sending stops by itself and the server's Auto DJ takes over, without the user's click.
    /// It stays off until it is turned on again (the next day starts with „Pokreni dan“).
    /// </summary>
    private void IskljuciUPonoc()
    {
        PrekiniEnkoder();
        Zeljeno = false;
        CekaZvuk = false;
        _ukljucenoU = default;
        Ponoc = true;
        _nadzornik.Change(Timeout.Infinite, Timeout.Infinite);
        if (Adresa.Length > 0)
            Dnevnik.Upisi($"Emitovanje {Adresa}: prekinuto u ponoć, na serveru nastavlja Auto DJ");
        Postavi(StreamStanje.Iskljucen, PorukaPonoc);
    }

    /// <summary>Starts the encoder and the cast. Called while holding the lock.</summary>
    private bool Pokreni()
    {
        CekaZvuk = false;
        Postavi(StreamStanje.Povezuje, null);
        if (_zvuk.Mikser == 0) return Otkazi("Plejer nije pokrenut.");

        var (host, port, mount, korisnik, lozinka, kbps) = _parametri;
        var putanja = mount.Length == 0 ? "/" : mount.StartsWith('/') ? mount : "/" + mount;
        Adresa = $"{host}:{port}{putanja}";

        BassEnc.CastTimeout = 8000;
        var lame = Path.Combine(AppContext.BaseDirectory, "lame.exe");
        // The mixer runs at the sound card's rate (48 kHz on most Windows machines). Telling lame a fixed
        // 44100 made the stream ~9 % slow and deep (user, 7. 11. 2026: „pesma usporena“).
        int frekvencija = _zvuk.Frekvencija > 0 ? _zvuk.Frekvencija : 44100;
        _enkoder = BassEnc.EncodeStart(_zvuk.Mikser, LameArgumenti(lame, frekvencija, kbps),
            EncodeFlags.NoHeader | EncodeFlags.ConvertFloatTo16BitInt, null, IntPtr.Zero);
        if (_enkoder == 0) return Otkazi($"Enkoder: {Bass.LastError}");

        BassEnc.EncodeSetNotify(_enkoder, _obavestenje, IntPtr.Zero);

        var server = $"{host}:{port}{putanja}";
        var loz = string.IsNullOrEmpty(korisnik) ? lozinka : $"{korisnik}:{lozinka}";
        if (!BassEnc.CastInit(_enkoder, server, loz, BassEnc.MimeMp3, _naziv, null, null, null, null, kbps, false))
        {
            var greska = Bass.LastError;
            PrekiniEnkoder();
            return Otkazi($"Veza: {greska}");
        }

        _korak = 0;
        _puklo = false;
        _poslednjiNaslov = null;   // a fresh connection must send the title again, or the server shows none
        Dnevnik.Upisi($"Emitovanje: povezan na {Adresa}, mikser {frekvencija} Hz, {kbps} kbps");
        Postavi(StreamStanje.NaEtru, null);
        return true;
    }

    /// <summary>Says why it did not work and plans the next attempt (the user may still want to be on).</summary>
    private bool Otkazi(string poruka)
    {
        _puklo = false;
        _korak = Math.Min(_korak + 1, Pauze.Length);
        _sledeciPokusaj = DateTime.UtcNow.AddSeconds(Pauze[_korak - 1]);
        if (Adresa.Length > 0) Dnevnik.Upisi($"Emitovanje {Adresa}: {poruka}");
        Postavi(StreamStanje.Greska, poruka);
        return false;
    }

    /// <summary>Every two seconds: is the encoder and the connection still alive, and should we try again.</summary>
    private void Nadzor(object? stanje)
    {
        lock (_katanac)
        {
            // Midnight: the day is over, so the cast turns itself off and the server's Auto DJ takes the mount.
            // The last songs of the day may run a little past midnight, so a song that is playing is not cut.
            if (Zeljeno && ProslaPonoc(_ukljucenoU, Sada()) && _zvuk.Stanje != ZvukStanje.Svira)
            {
                IskljuciUPonoc();
                return;
            }

            if (_enkoder != 0)
            {
                // Nothing plays any more (queue ran out or pause): close the cast at once instead of sending
                // silence to the server, and wait for music like before the first song. Not a failure.
                if (_zvuk.Stanje != ZvukStanje.Svira)
                {
                    PrekiniEnkoder();
                    CekaZvuk = true;
                    Postavi(StreamStanje.Povezuje, PorukaCeka);
                    return;
                }

                bool ziv;
                try { ziv = BassEnc.EncodeIsActive(_enkoder) != PlaybackState.Stopped; }
                catch { ziv = false; }
                if (ziv && !_puklo) return;

                bool svira = _zvuk.Stanje == ZvukStanje.Svira;
                Dnevnik.Upisi($"Emitovanje: prekidam enkoder (aktivan={ziv}, puklo={_puklo}, svira={svira}, BASS={Bass.LastError})");
                PrekiniEnkoder();

                // The music stopped (end of the queue or pause): the cast closed with the mixer. That is not
                // a failure, so it waits like it does before the first song instead of going red.
                if (!svira)
                {
                    CekaZvuk = true;
                    Postavi(StreamStanje.Povezuje, PorukaCeka);
                    return;
                }

                Otkazi(_puklo ? "Veza sa serverom je pukla." : "Enkoder je stao.");
                return;
            }

            if (!Zeljeno || DateTime.UtcNow < _sledeciPokusaj) return;

            // Nothing plays: do not open the stream, Icecast would time out a source that sends no audio.
            if (_zvuk.Stanje != ZvukStanje.Svira)
            {
                CekaZvuk = true;
                if (Stanje != StreamStanje.Povezuje || Poruka != PorukaCeka)
                    Postavi(StreamStanje.Povezuje, PorukaCeka);
                return;
            }

            CekaZvuk = false;
            Pokreni();
        }
    }

    private void Obavestenje(int handle, EncodeNotifyStatus status, IntPtr korisnik)
    {
        // Written down (a few times per status) so the reason for a drop can be seen in greske.log.
        int puta = _obavestenja.AddOrUpdate(status, 1, (_, n) => n + 1);
        if (puta <= 3) Dnevnik.Upisi($"Emitovanje {Adresa}: obaveštenje {status}");

        // Only slow sending or a few dropped bytes: the connection itself is still there.
        if (status is EncodeNotifyStatus.CastTimeout or EncodeNotifyStatus.QueueFull) return;
        _puklo = true;   // the watch does the stopping and the reconnecting, outside the BASS thread
    }

    private void PrekiniEnkoder()
    {
        if (_enkoder == 0) return;
        BassEnc.EncodeStop(_enkoder);
        _enkoder = 0;
    }

    private void Postavi(StreamStanje stanje, string? poruka)
    {
        Stanje = stanje;
        Poruka = poruka;
        Promenjeno?.Invoke();
    }

    public void Dispose()
    {
        lock (_katanac)
        {
            Zeljeno = false;
            _nadzornik.Change(Timeout.Infinite, Timeout.Infinite);
            PrekiniEnkoder();
        }
        _nadzornik.Dispose();
    }
}

