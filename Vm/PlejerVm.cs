using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using Bronxamp.Podaci;
using Bronxamp.Zvuk;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bronxamp.Vm;

public sealed record SlediStavka(Pesma Pesma, string Naslov, string Trajanje, bool Tekuca, int Unapred);

/// <summary>Everything about what is playing: queue, position, "up next" and the end-of-show clock.</summary>
public sealed partial class PlejerVm : ObservableObject, IDisposable
{
    public const string ImePrograma = "BronxSoft Bronxamp";

    private const string PocetniPodnaslov = "Izaberi album ili emisiju";
    private const int NajviseUSledi = 300;
    private const int NajviseGresakaZaredom = 10;
    private const double SekundiZaPustanje = 30;

    private static readonly Color BojaBezOmota = Color.FromRgb(0x2A, 0x26, 0x2E);

    private readonly ZvukPlejer _zvuk;
    private readonly Preslusavanje _preslusavanje = new();
    private readonly Baza _baza;
    private readonly Omoti _omoti;
    private readonly Podesavanja _podesavanja;
    private readonly Dispatcher _ui;
    private readonly DispatcherTimer _sat;
    private readonly StreamEmisija _stream;

    private bool _zabelezeno;
    private int _gresakaZaredom;
    private int _brojac;

    public Red Red { get; } = new();

    public ObservableCollection<SlediStavka> Sledi { get; } = [];

    [ObservableProperty] private Pesma? _tekuca;
    [ObservableProperty] private string _naslov = ImePrograma;
    [ObservableProperty] private string _podnaslov = PocetniPodnaslov;
    [ObservableProperty] private string _izvodjac = "";
    [ObservableProperty] private ImageSource? _omot;
    [ObservableProperty] private Brush? _prelivOmota;
    [ObservableProperty] private Color _bojaTrake = BojaBezOmota;
    [ObservableProperty] private string _vremeTekst = "00:00";
    [ObservableProperty] private string _ukupnoTekst = "00:00";
    [ObservableProperty] private string _preostaloTekst = "00:00";
    [ObservableProperty] private string _satTekst = "00:00:00";
    [ObservableProperty] private string _datumTekst = "";
    [ObservableProperty] private string _naEtruNaslov = "Ništa ne svira";
    [ObservableProperty] private string _naEtruPesma = "Ništa ne svira";
    [ObservableProperty] private string _naEtruIzvodjac = "";
    [ObservableProperty] private string _naEtruOpis = "U etru";
    [ObservableProperty] private string _statusStavke = "";
    [ObservableProperty] private string _statusKraj = "";
    [ObservableProperty] private string? _karticaSledi;
    [ObservableProperty] private string _karticaSlediVreme = "";
    [ObservableProperty] private double _odnos;
    [ObservableProperty] private bool _svira;
    [ObservableProperty] private bool _mesanje;
    [ObservableProperty] private Ponavljanje _ponavljanje;
    [ObservableProperty] private double _jacina;

    [ObservableProperty] private string _slediNaslov = "SLEDI";
    [ObservableProperty] private bool _imaRed;
    [ObservableProperty] private string _josOpis = "";
    [ObservableProperty] private string _josTrajanje = "";
    [ObservableProperty] private bool _krajVidljiv;
    [ObservableProperty] private string _krajSat = "00:00:00";
    [ObservableProperty] private string _krajOpis = "";

    [ObservableProperty] private string? _preslusaNaslov;
    [ObservableProperty] private string _preslusaVreme = "";

    /// <summary>The audible song or the playing state changed. For the Windows integration.</summary>
    public event Action? Promenjeno;
    public event Action<Pesma>? Zabelezeno;
    public event Action<string>? Poruka;

    /// <summary>
    /// Play was pressed with nothing in the queue: plays what is on screen instead. False when there is nothing to play.
    /// </summary>
    public Func<bool>? PustiSaEkrana { get; set; }

    public PlejerVm(Baza baza, Omoti omoti, Podesavanja podesavanja, Dispatcher ui, bool bezZvuka)
    {
        _baza = baza;
        _omoti = omoti;
        _podesavanja = podesavanja;
        _ui = ui;

        _zvuk = new ZvukPlejer();
        if (!_zvuk.Pokreni(bezZvuka ? 0 : -1))
            Poruka?.Invoke("Zvučna kartica nije dostupna.");
        if (!bezZvuka && !string.IsNullOrEmpty(podesavanja.Izlaz))
            PostaviIzlaz(podesavanja.Izlaz);

        _zvuk.PesmaPocela += s => _ui.BeginInvoke(() => NaPesmaPocela(s));
        _zvuk.RedZavrsen += () => _ui.BeginInvoke(NaRedZavrsen);
        _zvuk.NijeOtvoreno += (s, g) => _ui.BeginInvoke(() => NaNijeOtvoreno(s));

        _stream = new StreamEmisija(_zvuk);
        _stream.Promenjeno += () => _ui.BeginInvoke(OsveziEmitovanje);

        _jacina = podesavanja.Jacina;
        _zvuk.Jacina = podesavanja.Jacina;
        _mesanje = podesavanja.Mesanje;
        _ponavljanje = podesavanja.Ponavljanje;
        Red.Mesanje = _mesanje;
        Red.Ponavljanje = _ponavljanje;

        Prikaz.Spektar.Izvor = _zvuk.Spektar;
        Prikaz.Spektar.Frekvencija = () => _zvuk.Frekvencija;
        Prikaz.NivoMetar.Izvor = _zvuk.Nivo;

        _sat = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromMilliseconds(200) };
        _sat.Tick += (_, _) => Otkucaj();
        _sat.Start();
    }

    public ZvukPlejer Zvuk => _zvuk;

    public bool Stoji => _zvuk.Stanje == ZvukStanje.Stoji;
    public bool UcitavaSe => _zvuk.UcitavaSe;
    public double Pozicija => _zvuk.Pozicija;

    /// <summary>A day is on air: its order is fixed, so shuffle and repeat stay out of it, and songs do not overlap.</summary>
    public bool Zakljucan { get; private set; }

    public async Task PustiDan(IReadOnlyList<Pesma> pesme, string naziv, double pomak)
    {
        Zakljucan = true;
        Red.Mesanje = false;
        Red.Ponavljanje = Ponavljanje.Ne;
        Red.Postavi(pesme, 0, naziv);
        await PustiTekucu();
        if (pomak > 1) _zvuk.Premotaj(pomak);
    }

    public void Otkljucaj()
    {
        Zakljucan = false;
        Red.Mesanje = Mesanje;
        Red.Ponavljanje = Ponavljanje;
    }

    /// <summary>A crossfade would blur the album's own track transitions, so albums go gapless by default.</summary>
    public double TrenutniPrelaz =>
        Zakljucan || (Red.JeAlbum && !_podesavanja.PrelazUAlbumima) ? 0 : _podesavanja.Prelaz;

    // ---- playback ----

    public async Task PustiTekucu()
    {
        var pesma = Red.Tekuca;
        if (pesma == null)
        {
            _zvuk.Zaustavi();
            OsveziStanje();
            return;
        }

        OznaciTekucu(pesma);
        if (await _zvuk.PustiAsync(new ZvukStavka(pesma.Putanja, pesma)))
        {
            _gresakaZaredom = 0;
            PripremiSledecu();
        }
        OsveziStanje();
        OsveziSledi();
    }

    /// <summary>The queue changed, so the song waiting in the mixer may no longer be the right one.</summary>
    public void PripremiSledecu()
    {
        if (_zvuk.Stanje == ZvukStanje.Stoji) return;
        var sledeca = Red.Sledeca;
        _zvuk.PostaviSledecu(sledeca == null ? null : new ZvukStavka(sledeca.Putanja, sledeca),
            Red.Ponavljanje == Ponavljanje.Jedna ? 0 : TrenutniPrelaz);
        OsveziSledi();
    }

    private void OznaciTekucu(Pesma pesma)
    {
        Tekuca = pesma;
        Naslov = pesma.Naslov;
        Izvodjac = pesma.Izvodjac;
        var godina = pesma.Godina > 0 ? pesma.Godina : pesma.Album.Godina;
        Podnaslov = godina > 0 ? $"{pesma.Izvodjac} · {pesma.Album.Naziv} · {godina}" : $"{pesma.Izvodjac} · {pesma.Album.Naziv}";
        UkupnoTekst = pesma.TrajanjeTekst;
        NaEtruNaslov = pesma.Izvodjac.Length > 0 ? $"{pesma.Izvodjac} - {pesma.Naslov}" : pesma.Naslov;
        // Two separate lines in the top bar: the artist must stay visible even when the title is long.
        NaEtruIzvodjac = pesma.Izvodjac;
        NaEtruPesma = pesma.Naslov;
        NaEtruOpis = $"U etru · {pesma.Album.Naziv}";
        VremeTekst = "00:00";
        Odnos = 0;
        _zabelezeno = false;

        var album = pesma.Album;
        PrelivOmota = AlbumKartica.PrelivZa(album.Naziv + album.Izvodjac);
        PostaviOmot(_omoti.IzKesa(album.Id));
        if (Omot == null)
            _omoti.Zatrazi(album, () => _ui.BeginInvoke(() =>
            {
                if (Tekuca?.Album == album) PostaviOmot(_omoti.IzKesa(album.Id));
            }));
        Promenjeno?.Invoke();
    }

    private void PostaviOmot(ImageSource? slika)
    {
        Omot = slika;
        var boja = Omoti.DominantnaBoja(slika);
        boja ??= (PrelivOmota as LinearGradientBrush)?.GradientStops[0].Color;
        BojaTrake = boja == null ? BojaBezOmota : Prigusi(boja.Value);
        if (slika != null) Promenjeno?.Invoke();
    }

    /// <summary>Tames the cover color so white text stays readable on the bar.</summary>
    private static Color Prigusi(Color c)
    {
        double najjaca = Math.Max(c.R, Math.Max(c.G, c.B));
        double faktor = najjaca > 0 ? Math.Min(1, 112 / najjaca) : 1;
        return Color.FromRgb((byte)(c.R * faktor), (byte)(c.G * faktor), (byte)(c.B * faktor));
    }

    private void NaPesmaPocela(ZvukStavka stavka)
    {
        if (stavka.Oznaka is not Pesma pesma) return;
        if (Red.Sledeca == pesma) Red.Napred();
        OznaciTekucu(pesma);
        _stream.NaslovPesme(pesma.Izvodjac, pesma.Naslov);
        _gresakaZaredom = 0;
        PripremiSledecu();
        OsveziStanje();
    }

    private void NaRedZavrsen()
    {
        VremeTekst = "00:00";
        PreostaloTekst = "00:00";
        KarticaSledi = null;
        Odnos = 0;
        OsveziStanje();
        OsveziSledi();
    }

    private void NaNijeOtvoreno(ZvukStavka stavka)
    {
        if (stavka.Oznaka is not Pesma pesma) return;
        Poruka?.Invoke($"Ne mogu da otvorim „{pesma.Naslov}“. Fajl možda više ne postoji.");

        if (++_gresakaZaredom >= NajviseGresakaZaredom)
        {
            _zvuk.Zaustavi();
            OsveziStanje();
            return;
        }

        if (Red.Tekuca == pesma && Red.Tekuca != null && _zvuk.Tekuca?.Oznaka != pesma)
        {
            if (Red.RucnoNapred()) _ = PustiTekucu();
        }
        else
        {
            Red.Izbaci(pesma);
            PripremiSledecu();
        }
    }

    private void OsveziStanje()
    {
        Svira = _zvuk.Stanje == ZvukStanje.Svira;
        Promenjeno?.Invoke();
    }

    [RelayCommand]
    public void PustiPauza()
    {
        // A song is still opening: pause now, or take the waiting pause back. Never start it a second time.
        if (_zvuk.UcitavaSe)
        {
            if (_zvuk.PauzaNaCekanju) _zvuk.Nastavi();
            else _zvuk.Pauza();
            OsveziStanje();
            return;
        }

        switch (_zvuk.Stanje)
        {
            case ZvukStanje.Svira:
                _zvuk.Pauza();
                break;
            case ZvukStanje.Pauza:
                _zvuk.Nastavi();
                break;
            default:
                // Right after start the queue is empty: Play then takes the list on screen, as Winamp and RadioBOSS do.
                if (Red.Tekuca == null && PustiSaEkrana?.Invoke() == true) return;
                _ = PustiTekucu();
                return;
        }
        OsveziStanje();
    }

    [RelayCommand]
    private void Pusti()
    {
        if (_zvuk.Stanje != ZvukStanje.Svira) PustiPauza();
    }

    [RelayCommand]
    private void Pauziraj()
    {
        if (_zvuk.Stanje == ZvukStanje.Svira) PustiPauza();
    }

    public void Stop()
    {
        _zvuk.Zaustavi();
        Odnos = 0;
        PreostaloTekst = "00:00";
        OsveziStanje();
        OsveziSledi();
    }

    [RelayCommand]
    public void Sledeca()
    {
        if (Red.RucnoNapred()) _ = PustiTekucu();
    }

    [RelayCommand]
    public void Prethodna()
    {
        // As on a CD player: first press goes back to the start of the song.
        if (_zvuk.Pozicija > 3 || !Red.Nazad())
        {
            _zvuk.Premotaj(0);
            return;
        }
        _ = PustiTekucu();
    }

    [RelayCommand]
    private void PromeniMesanje()
    {
        if (Zakljucan)
        {
            Poruka?.Invoke("Dok traje dan redosled je stalan.");
            return;
        }

        Mesanje = !Mesanje;
        Red.Mesanje = Mesanje;
        _podesavanja.Mesanje = Mesanje;
        PripremiSledecu();
    }

    [RelayCommand]
    private void PromeniPonavljanje()
    {
        if (Zakljucan)
        {
            Poruka?.Invoke("Dok traje dan redosled je stalan.");
            return;
        }

        Ponavljanje = Ponavljanje switch
        {
            Ponavljanje.Ne => Ponavljanje.Sve,
            Ponavljanje.Sve => Ponavljanje.Jedna,
            _ => Ponavljanje.Ne,
        };
        Red.Ponavljanje = Ponavljanje;
        _podesavanja.Ponavljanje = Ponavljanje;
        PripremiSledecu();
    }

    partial void OnJacinaChanged(double value)
    {
        _zvuk.Jacina = (float)value;
        _podesavanja.Jacina = (float)value;
    }

    public void PremotajNa(double odnos)
    {
        if (_zvuk.Stanje == ZvukStanje.Stoji) return;
        _zvuk.Premotaj(odnos * _zvuk.Trajanje);
    }

    public void SkociNa(int unapred)
    {
        if (unapred < 0) return;
        if (Red.SkociNa(unapred)) _ = PustiTekucu();
    }

    // ---- editing the queue ----

    public void UkloniIzReda(IReadOnlyCollection<SlediStavka> stavke)
    {
        // From the back, so the positions of the remaining ones stay valid.
        foreach (var s in stavke.Where(s => !s.Tekuca).OrderByDescending(s => s.Unapred))
            Red.UkloniUnapred(s.Unapred);

        if (!stavke.Any(s => s.Tekuca))
        {
            PripremiSledecu();
            OsveziSledi();
            return;
        }

        bool imaSledeca = Red.UkloniTekucu();
        if (Red.Ukupno == 0)
        {
            IsprazniRed();
        }
        else if (imaSledeca)
        {
            _ = PustiTekucu();
        }
        else
        {
            _zvuk.Zaustavi();
            OznaciTekucu(Red.Tekuca!);
            OsveziStanje();
            OsveziSledi();
        }
    }

    public void UkloniAlbumIzReda(Album album) => UkloniIzRedaAko(p => p.AlbumId == album.Id);

    public void UkloniPesmeIzReda(IReadOnlySet<long> pesme) => UkloniIzRedaAko(p => pesme.Contains(p.Id));

    private void UkloniIzRedaAko(Func<Pesma, bool> uslov)
    {
        var stavke = Red.Preostale
            .Select((p, i) => (Pesma: p, Unapred: i))
            .Where(x => uslov(x.Pesma))
            .Select(x => new SlediStavka(x.Pesma, "", "", false, x.Unapred))
            .ToList();
        if (Red.Tekuca is { } tekuca && uslov(tekuca))
            stavke.Add(new SlediStavka(tekuca, "", "", true, -1));
        UkloniIzReda(stavke);
    }

    public void IsprazniRed()
    {
        Red.Isprazni();
        _zvuk.Zaustavi();

        Tekuca = null;
        NaEtruNaslov = "Ništa ne svira";
        NaEtruPesma = "Ništa ne svira";
        NaEtruIzvodjac = "";
        NaEtruOpis = "U etru";
        Naslov = ImePrograma;
        Podnaslov = PocetniPodnaslov;
        Izvodjac = "";
        Omot = null;
        PrelivOmota = null;
        BojaTrake = BojaBezOmota;
        VremeTekst = "00:00";
        UkupnoTekst = "00:00";
        Odnos = 0;
        OsveziStanje();
        OsveziSledi();
    }

    // ---- clock ----

    private void Otkucaj()
    {
        if (_preslusavanje.Svira)
            PreslusaVreme = $"{Vreme.Kratko(_preslusavanje.Pozicija)} / {Vreme.Kratko(_preslusavanje.Trajanje)}";
        else if (PreslusaNaslov != null)
            PreslusaNaslov = null;

        if (_zvuk.Stanje != ZvukStanje.Stoji)
        {
            double pozicija = _zvuk.Pozicija, trajanje = _zvuk.Trajanje;
            VremeTekst = Vreme.Kratko(pozicija);
            if (trajanje > 0)
            {
                UkupnoTekst = Vreme.Kratko(trajanje);
                PreostaloTekst = Vreme.Kratko(Math.Max(0, trajanje - pozicija));
                Odnos = pozicija / trajanje;
            }
            ZabeleziAkoTreba(pozicija, trajanje);
        }

        var sada = DateTime.Now;
        SatTekst = sada.ToString("HH:mm:ss");
        DatumTekst = sada.ToString("dd.MM.yyyy.");
        if (++_brojac % 5 == 0)
        {
            OsveziKraj();
            OsveziKarticuSledi();
        }
    }

    /// <summary>What the Next card says when nothing is playing, like what a waiting day starts with.</summary>
    public (string Naslov, string Vreme)? ZamenaSledi { get; set; }

    private void OsveziKarticuSledi()
    {
        if (Red.Sledeca is not { } sledeca || _zvuk.Stanje == ZvukStanje.Stoji)
        {
            KarticaSledi = ZamenaSledi?.Naslov ?? "Ništa";
            KarticaSlediVreme = ZamenaSledi?.Vreme ?? "Sledi";
            return;
        }

        double ostalo = Math.Max(0, _zvuk.Trajanje - _zvuk.Pozicija) - Math.Min(TrenutniPrelaz, _zvuk.Trajanje / 2);
        KarticaSledi = sledeca.Izvodjac.Length > 0 ? $"{sledeca.Izvodjac} - {sledeca.Naslov}" : sledeca.Naslov;
        KarticaSlediVreme = $"Sledi u {DateTime.Now.AddSeconds(Math.Max(0, ostalo)):HH:mm:ss}";
    }

    private void ZabeleziAkoTreba(double pozicija, double trajanje)
    {
        if (_zabelezeno || Tekuca == null || trajanje <= 0) return;
        if (pozicija < Math.Min(SekundiZaPustanje, trajanje / 2)) return;

        _zabelezeno = true;
        var pesma = Tekuca;

        // Items of a day are not in the library; the day keeps its own log of what went on air.
        if (pesma.Id <= 0) return;
        var sada = DateTime.Now;
        try
        {
            _baza.ZabeleziPustanje(pesma.Id, Red.Emisija?.Id, sada);
            pesma.Album.PoslednjePustanje = sada;
            pesma.PoslednjePustanje = sada;
            pesma.Pustano++;
            Zabelezeno?.Invoke(pesma);
        }
        catch (Exception ex)
        {
            Dnevnik.Greska("Beleženje puštanja", ex);
        }
    }

    public void OsveziSledi()
    {
        var naziv = Red.Naziv;
        SlediNaslov = string.IsNullOrEmpty(naziv)
            ? "SLEDI"
            : Red.Emisija != null
                ? $"SLEDI · {naziv.ToUpperInvariant()} {DateTime.Now:dd.MM.}"
                : $"SLEDI · {naziv.ToUpperInvariant()}";

        var preostale = Red.Preostale.ToList();
        var nove = new List<SlediStavka>();
        if (Red.Tekuca is { } tekuca)
            nove.Add(new SlediStavka(tekuca, tekuca.Naslov, tekuca.TrajanjeTekst, true, -1));
        for (int i = 0; i < preostale.Count && nove.Count < NajviseUSledi; i++)
            nove.Add(new SlediStavka(preostale[i], preostale[i].Naslov, preostale[i].TrajanjeTekst, false, i));

        if (!nove.SequenceEqual(Sledi))
        {
            Sledi.Clear();
            foreach (var s in nove) Sledi.Add(s);
        }

        ImaRed = Red.Ukupno > 0;
        int prikazano = nove.Count(s => !s.Tekuca);
        int skriveno = preostale.Count - prikazano;
        JosOpis = skriveno > 0 ? $"... još {skriveno}" : "";
        JosTrajanje = skriveno > 0 ? Vreme.Kratko(preostale.Skip(prikazano).Sum(p => p.Trajanje)) : "";
        OsveziKraj();
    }

    private void OsveziKraj()
    {
        StatusStavke = Red.Ukupno > 0 ? $"Stavki: {Red.Ukupno} · {Podaci.Vreme.Dugo(Red.SvePesme.Sum(p => p.Trajanje))}" : "";
        if (Red.Tekuca == null || Ponavljanje != Ponavljanje.Ne || _zvuk.Stanje == ZvukStanje.Stoji)
        {
            KrajVidljiv = false;
            StatusKraj = "";
            return;
        }

        var preostale = Red.Preostale.Select(p => p.Trajanje).ToList();
        double trajanje = _zvuk.Trajanje > 0 ? _zvuk.Trajanje : Red.Tekuca.Trajanje;
        double prelaz = TrenutniPrelaz;
        double sekundi = KrajEmisije.Preostalo(trajanje - _zvuk.Pozicija, trajanje, preostale, prelaz);

        KrajVidljiv = true;
        KrajSat = DateTime.Now.AddSeconds(sekundi).ToString("HH:mm:ss");
        StatusKraj = $"Još {Podaci.Vreme.Dugo(sekundi)} · kraj u {KrajSat}";
        var opis = $"{Podaci.Vreme.BrojPesama(preostale.Count + 1)} · još {Podaci.Vreme.Dugo(sekundi)}";
        KrajOpis = prelaz > 0 ? $"{opis} · prelaz {prelaz:0.#} s" : $"{opis} · bez prelaza";
    }

    // ---- output devices ----

    public void PostaviIzlaz(string? naziv)
    {
        var uredjaj = ZvukPlejer.Uredjaji().FirstOrDefault(u => u.Naziv == naziv);
        if (uredjaj != null && !_zvuk.PromeniUredjaj(uredjaj.Broj))
            Poruka?.Invoke($"Izlaz „{naziv}“ nije dostupan.");
    }

    /// <summary>Devices usable for headphones cue: real ones, without the "Default" alias.</summary>
    public static IReadOnlyList<IzlazniUredjaj> StvarniUredjaji() =>
        ZvukPlejer.Uredjaji().Where(u => u.Broj > 1 || u.Naziv != "Default").ToList();

    public bool MozePreslusavanje => StvarniUredjaji().Count >= 2;

    public void Preslusaj(Pesma pesma)
    {
        var uredjaji = StvarniUredjaji();
        var uredjaj = uredjaji.FirstOrDefault(u => u.Naziv == _podesavanja.Slusalice)
                      ?? uredjaji.FirstOrDefault(u => !u.Podrazumevani);
        if (uredjaj == null)
        {
            Poruka?.Invoke("Za preslušavanje treba drugi izlazni uređaj.");
            return;
        }

        if (_preslusavanje.Pusti(pesma.Putanja, uredjaj.Broj))
        {
            PreslusaNaslov = $"{pesma.Izvodjac} - {pesma.Naslov}";
            PreslusaVreme = "";
        }
        else
        {
            Poruka?.Invoke($"Preslušavanje na uređaju „{uredjaj.Naziv}“ nije uspelo.");
        }
    }

    [RelayCommand]
    private void ZaustaviPreslusavanje()
    {
        _preslusavanje.Zaustavi();
        PreslusaNaslov = null;
    }

    // ---- emitovanje (stream) ----

    private static readonly Brush SivaEmitovanja = new SolidColorBrush(Color.FromRgb(0x9A, 0xA1, 0xA7));
    private static readonly Brush ZelenaEmitovanja = new SolidColorBrush(Color.FromRgb(0x3F, 0xB2, 0x7F));
    private static readonly Brush ZutaEmitovanja = new SolidColorBrush(Color.FromRgb(0xE8, 0xB3, 0x3A));
    private static readonly Brush CrvenaEmitovanja = new SolidColorBrush(Color.FromRgb(0xD9, 0x4A, 0x3A));

    /// <summary>True once the connection was up, so a drop reads differently from a first failure.</summary>
    private bool _bioNaEtru;

    private static readonly Brush PovrsinaEmitovanja = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x2B));
    private static readonly Brush IvicaMira = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x39));

    [ObservableProperty] private string _statusEmitovanja = "Nije povezan";
    [ObservableProperty] private Brush _bojaEmitovanja = SivaEmitovanja;
    [ObservableProperty] private Brush _pozadinaEmitovanja = PovrsinaEmitovanja;
    [ObservableProperty] private Brush _ivicaEmitovanja = IvicaMira;
    [ObservableProperty] private bool _emitovanjeUToku;

    /// <summary>„Uključi emitovanje“ until the user turns it on, then „Prekini emitovanje“.</summary>
    public string NaslovEmitovanja => _stream.Zeljeno ? "Prekini emitovanje" : "Uključi emitovanje";

    /// <summary>Tooltip: the server it goes to, and why it is not there when something is wrong.</summary>
    public string OpisEmitovanja
    {
        get
        {
            var probno = _podesavanja.StreamProbno ? " (probno, lokalni server)" : "";
            if (_stream.Stanje == StreamStanje.Iskljucen)
                return _stream.Poruka ?? (_stream.Adresa.Length > 0
                    ? $"Emitovanje je isključeno ({_stream.Adresa}{probno})"
                    : "Slanje na Zeno.fm (dok je „probno“, ide na lokalni server)");
            return _stream.Stanje == StreamStanje.Greska || _stream.CekaZvuk
                ? $"{_stream.Adresa}{probno}: {_stream.Poruka}"
                : $"Povezan na {_stream.Adresa}{probno}";
        }
    }

    partial void OnEmitovanjeUTokuChanged(bool value) => OnPropertyChanged(nameof(NaslovEmitovanja));

    /// <summary>Where it goes: the local test server while "probno" is on, otherwise the Zeno details from settings.</summary>
    private (string Host, int Port, string Mount, string Korisnik, string Lozinka) ParametriEmitovanja()
    {
        var p = _podesavanja;
        if (p.StreamProbno)
            return ("127.0.0.1", 8000, "/test.mp3", "source", "bronxamp-test");
        return (p.StreamServer, p.StreamPort, p.StreamMount, p.StreamKorisnik, Sistem.Zastita.Desifruj(p.StreamLozinka));
    }

    private bool PoveziStream()
    {
        var (host, port, mount, korisnik, lozinka) = ParametriEmitovanja();
        bool ok = _stream.Povezi(host, port, mount, korisnik, lozinka, _podesavanja.StreamKbps, ImePrograma);
        if (ok && Tekuca is { } pesma) _stream.NaslovPesme(pesma.Izvodjac, pesma.Naslov);
        OsveziEmitovanje();
        return ok;
    }

    [RelayCommand]
    private void UkljuciEmitovanje()
    {
        if (_stream.Zeljeno)
        {
            _stream.Prekini();
            _bioNaEtru = false;
            OsveziEmitovanje();
            return;
        }
        _bioNaEtru = false;   // a fresh turn-on: a drop is reported only once it has really been on air
        if (!PoveziStream())
            Poruka?.Invoke(_stream.Poruka ?? "Emitovanje nije uspelo.");
    }

    /// <summary>„Test veze" in Settings: connect, report, and disconnect straight away.</summary>
    public (bool Uspelo, string Poruka) TestirajEmitovanje()
    {
        if (_stream.Zeljeno) _stream.Prekini();
        bool ok = PoveziStream();
        string poruka = ok ? "Veza sa serverom je uspostavljena." : (_stream.Poruka ?? "Veza nije uspela.");
        _stream.Prekini();
        OsveziEmitovanje();
        return (ok, poruka);
    }

    private void OsveziEmitovanje()
    {
        EmitovanjeUToku = _stream.Zeljeno;
        if (_stream.Stanje == StreamStanje.NaEtru) _bioNaEtru = true;

        switch (_stream.Stanje)
        {
            case StreamStanje.NaEtru:
                StatusEmitovanja = "Povezan na server";
                BojaEmitovanja = ZelenaEmitovanja;
                if (Tekuca is { } pesma) _stream.NaslovPesme(pesma.Izvodjac, pesma.Naslov);
                break;
            case StreamStanje.Povezuje:
                if (_stream.CekaZvuk)
                {
                    StatusEmitovanja = "Čeka muziku";
                    BojaEmitovanja = SivaEmitovanja;
                }
                else
                {
                    StatusEmitovanja = "Povezivanje…";
                    BojaEmitovanja = ZutaEmitovanja;
                }
                break;
            case StreamStanje.Greska:
                StatusEmitovanja = _bioNaEtru ? "Veza pukla — vraćam se…" : "Nije se povezao…";
                BojaEmitovanja = CrvenaEmitovanja;
                break;
            default:
                StatusEmitovanja = _stream.Ponoc ? "Prekinuto u ponoć" : "Nije povezan";
                BojaEmitovanja = SivaEmitovanja;
                break;
        }

        OnPropertyChanged(nameof(NaslovEmitovanja));
        OnPropertyChanged(nameof(OpisEmitovanja));
        ObuciEmitovanje(_stream.Stanje == StreamStanje.Iskljucen ? null : BojaEmitovanja);
    }

    /// <summary>Paints the little bar in the status row: plain while off, tinted by the state while it is on.</summary>
    private void ObuciEmitovanje(Brush? boja)
    {
        if (boja is not SolidColorBrush cetka)
        {
            PozadinaEmitovanja = PovrsinaEmitovanja;
            IvicaEmitovanja = IvicaMira;
            return;
        }
        var c = cetka.Color;
        PozadinaEmitovanja = new SolidColorBrush(Color.FromArgb(0x24, c.R, c.G, c.B));
        IvicaEmitovanja = new SolidColorBrush(Color.FromArgb(0x70, c.R, c.G, c.B));
    }

    public void Dispose()
    {
        _sat.Stop();
        _preslusavanje.Dispose();
        _stream.Dispose();
        _zvuk.Dispose();
        ZvukPlejer.Ugasi();
    }
}
