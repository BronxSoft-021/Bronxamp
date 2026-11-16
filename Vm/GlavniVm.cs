using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Threading;
using Bronxamp.Podaci;
using Bronxamp.Prikaz;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bronxamp.Vm;

public enum PrikazBiblioteke { Omoti, Lista, AS, Godina }

public sealed partial class NavStavka(string naziv, string ikona, Action akcija, string? boja = null) : ObservableObject
{
    /// <summary>The Serbian text; what is shown is its translation.</summary>
    public string Kljuc { get; } = naziv;

    public string Naziv { get; set; } = Prikaz.Rec.T(naziv);
    public string Ikona { get; } = ikona;
    public string? Boja { get; set; } = boja;
    public Emisija? Emisija { get; init; }

    [ObservableProperty] private bool _aktivna;

    [RelayCommand]
    private void Otvori() => akcija();

    public void Osvezi()
    {
        Naziv = Prikaz.Rec.T(Kljuc);
        OnPropertyChanged(nameof(Naziv));
        OnPropertyChanged(nameof(Boja));
    }
}

public sealed partial class GlavniVm : ObservableObject, IDisposable
{
    private static readonly CompareInfo Srpski = new CultureInfo("sr-Latn-RS").CompareInfo;

    /// <summary>Early versions created these on their own. The user decides which shows exist.</summary>
    private static readonly (string Ime, string Boja)[] StarePocetneEmisije =
    [
        ("Gluvo doba", "#6C5CE7"), ("Preslušavanje", "#E8763A"), ("Napred u prošlost", "#E8B33A"), ("Etnopoganija", "#3FB27F"),
    ];

    private readonly Dispatcher _ui;
    private readonly Dictionary<long, AlbumKartica> _kartice = [];
    private readonly DispatcherTimer _odlozenaPretraga;
    private readonly DispatcherTimer _skrivanjePoruke;
    private CancellationTokenSource? _filter;
    private readonly EmisijeStrana _emisijeStrana;
    private NavStavka? _aktivnaNav;
    private NavStavka? _aktivnaPilula;
    private ZidStrana? _zidAlbuma;
    private Action? _osveziStranu;

    public Baza Baza { get; }
    public Katalog Katalog { get; }
    public Skener Skener { get; }
    public Omoti Omoti { get; }
    public Podesavanja Podesavanja { get; }
    public PlejerVm Plejer { get; }
    public DanStrana Dan { get; }
    public IzabranaPesma Izabrana { get; }

    public ObservableCollection<NavStavka> NavProgram { get; } = [];
    public ObservableCollection<NavStavka> NavMuzika { get; } = [];
    public ObservableCollection<NavStavka> NavIzvestaj { get; } = [];
    public ObservableCollection<NavStavka> PiluleBiblioteke { get; } = [];
    public ObservableCollection<NavStavka> PiluleStatistike { get; } = [];
    public ObservableCollection<NavStavka> NavEmisije { get; } = [];

    [ObservableProperty] private Strana? _strana;

    /// <summary>The pills above the page in this part of the program. Null where there are none.</summary>
    [ObservableProperty] private ObservableCollection<NavStavka>? _pilule;
    [ObservableProperty] private string _upit = "";
    [ObservableProperty] private PrikazBiblioteke _prikaz;
    [ObservableProperty] private bool _prikaziTabove = true;
    [ObservableProperty] private string? _poruka;
    [ObservableProperty] private string? _statusSkeniranja;
    [ObservableProperty] private bool _nemaFoldera;
    [ObservableProperty] private bool _nemaEmisija;

    public string Verzija => "v" + Verzije.Trenutna;

    /// <summary>Plays changed, so recent albums for the taskbar list may have changed too.</summary>
    public event Action? NedavniPromenjeni;

    public GlavniVm(Dispatcher ui, bool bezZvuka)
    {
        _ui = ui;
        _emisijeStrana = new EmisijeStrana(NavEmisije);
        Podesavanja = Podesavanja.Ucitaj();
        Baza = new Baza(Putanje.Baza);
        Katalog = new Katalog(Baza);
        Skener = new Skener(Baza, Katalog);
        Omoti = new Omoti(Putanje.Omoti);
        Plejer = new PlejerVm(Baza, Omoti, Podesavanja, ui, bezZvuka);

        Dan = new DanStrana(Plejer, Podesavanja, ui);
        Izabrana = new IzabranaPesma(Omoti, ui);
        Dan.Poruka += PrikaziPoruku;
        Dan.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DanStrana.Tab) && Strana == Dan) Aktiviraj(NavProgram[MestoUMeniju(Dan.Tab)]);
        };
        Plejer.Poruka += PrikaziPoruku;
        Plejer.PustiSaEkrana = PustiSaEkrana;
        Plejer.Zabelezeno += _ => NedavniPromenjeni?.Invoke();
        Plejer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlejerVm.ImaRed)) Dan.NaPromenuReda();
            if (e.PropertyName != nameof(PlejerVm.Tekuca)) return;
            OznaciTekucuNaStrani();
            Dan.NaTekucu(Plejer.Tekuca);
            Izabrana.PustenaJe(Plejer.Tekuca);
        };

        _odlozenaPretraga = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromMilliseconds(180) };
        _odlozenaPretraga.Tick += (_, _) =>
        {
            _odlozenaPretraga.Stop();
            if (_aktivnaPilula != PiluleBiblioteke[0] || Strana?.Nazad != null) OtvoriAlbume();
            else _osveziStranu?.Invoke();
        };

        _skrivanjePoruke = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromSeconds(7) };
        _skrivanjePoruke.Tick += (_, _) =>
        {
            _skrivanjePoruke.Stop();
            Poruka = null;
        };

        _prikaz = Enum.TryParse<PrikazBiblioteke>(Podesavanja.Prikaz, out var prikaz) ? prikaz : PrikazBiblioteke.Omoti;

        NavProgram.Add(new NavStavka("U etru", "", () => OtvoriDan(TabDana.Dan)));
        NavProgram.Add(new NavStavka("Sastavi dan", "", OtvoriSastaviDan));
        NavProgram.Add(new NavStavka("Jinglovi", "", () => OtvoriDan(TabDana.Jinglovi)));
        NavMuzika.Add(new NavStavka("Biblioteka", "\uE8F1", OtvoriAlbume));
        NavMuzika.Add(new NavStavka("Folderi", "\uE8B7", OtvoriFoldere));
        NavIzvestaj.Add(new NavStavka("Emitovano", "\uE8FD", OtvoriEmitovano));
        NavIzvestaj.Add(new NavStavka("Statistika", "\uE9D2", OtvoriNajslusanije));

        PiluleBiblioteke.Add(new NavStavka("Albumi", "", OtvoriAlbume));
        PiluleBiblioteke.Add(new NavStavka("Izvođači", "", OtvoriIzvodjace));
        PiluleBiblioteke.Add(new NavStavka("Nedavno", "", OtvoriNedavno));
        PiluleBiblioteke.Add(new NavStavka("Omiljeno", "", OtvoriOmiljeno));
        PiluleBiblioteke.Add(new NavStavka("Emisije", "", OtvoriEmisije));
        PiluleStatistike.Add(new NavStavka("Najslušanije", "", OtvoriNajslusanije));
        PiluleStatistike.Add(new NavStavka("Nije puštano godinu dana", "", OtvoriNijePustano));

        UkloniPocetneEmisije();
        OsveziNavEmisije();

        Katalog.Promenjen += () => _ui.BeginInvoke(NaPromenuKataloga);
        Skener.Napredak += n => _ui.BeginInvoke(() =>
            StatusSkeniranja = n.Gotovo ? null : Bronxamp.Prikaz.Rec.T("Skeniram {0:N0} / {1:N0}", n.Obradjeno, n.Ukupno));

        // The menu holds plain strings, so they are set again when the language changes.
        Bronxamp.Prikaz.Rec.Promenjen += () => _ui.BeginInvoke(OsveziJezik);
    }

    /// <summary>Removes them once, and only if nothing was ever put in them or played from them.</summary>
    private void UkloniPocetneEmisije()
    {
        if (Podesavanja.PocetneEmisijeUklonjene) return;
        foreach (var e in Baza.Emisije())
            if (StarePocetneEmisije.Contains((e.Ime, e.Boja)) &&
                Baza.PesmeEmisije(e.Id).Count == 0 && Baza.IstorijaEmisije(e.Id, 1).Count == 0)
                Baza.ObrisiEmisiju(e.Id);
        Podesavanja.PocetneEmisijeUklonjene = true;
        Podesavanja.Sacuvaj();
    }

    /// <summary>Writes the menu again in the chosen language.</summary>
    private void OsveziJezik()
    {
        foreach (var stavka in NavProgram.Concat(NavMuzika).Concat(NavIzvestaj)
                     .Concat(PiluleBiblioteke).Concat(PiluleStatistike).Concat(NavEmisije))
            stavka.Osvezi();
    }

    public async Task PokreniAsync()
    {
        OtvoriAlbume();
        await Task.Run(Katalog.Ucitaj);
        NemaFoldera = Baza.Folderi().Count == 0;
        Skener.PratiFoldere();
        _ = Skener.SkenirajAsync();
        Dan.Osvezi();
        await Dan.NastaviAkoTreba();
    }

    public void OtvoriSastaviDan() => OtvoriDan(TabDana.Priprema);

    private static int MestoUMeniju(TabDana tab) => tab switch
    {
        TabDana.Dan => 0,
        TabDana.Priprema => 1,
        _ => 2,
    };

    public void OtvoriDan(TabDana tab)
    {
        Aktiviraj(NavProgram[MestoUMeniju(tab)]);
        PrikaziTabove = false;
        if (tab != TabDana.Dan) Dan.Osvezi();
        Dan.Tab = tab;
        Strana = Dan;
        _osveziStranu = null;
    }

    public void OtvoriFoldere()
    {
        Aktiviraj(NavMuzika[1]);
        PrikaziTabove = false;
        Strana = new FolderiStrana(Baza.Folderi().Prepend(Dan.FolderSema), this) { Naslov = "Folderi", NazivReda = "Folder", MozeUklanjanje = false };
        _osveziStranu = null;
    }

    public void OtvoriEmitovano()
    {
        Aktiviraj(NavIzvestaj[0]);
        PrikaziTabove = false;
        Strana = new EmitovanoStrana { Naslov = "Emitovano" };
        _osveziStranu = null;
    }

    private void NaPromenuKataloga()
    {
        _osveziStranu?.Invoke();
        NedavniPromenjeni?.Invoke();
    }

    public void PrikaziPoruku(string tekst)
    {
        Poruka = tekst;
        _skrivanjePoruke.Stop();
        _skrivanjePoruke.Start();
    }

    partial void OnUpitChanged(string value)
    {
        _odlozenaPretraga.Stop();
        _odlozenaPretraga.Start();
    }

    partial void OnPrikazChanged(PrikazBiblioteke value)
    {
        Podesavanja.Prikaz = value.ToString();
        if (_aktivnaPilula != null && _aktivnaPilula == PiluleBiblioteke[0]) OtvoriAlbume();
    }

    private void Aktiviraj(NavStavka? stavka, NavStavka? pilula = null)
    {
        if (_aktivnaNav != null) _aktivnaNav.Aktivna = false;
        _aktivnaNav = stavka;
        if (stavka != null) stavka.Aktivna = true;

        if (_aktivnaPilula != null) _aktivnaPilula.Aktivna = false;
        _aktivnaPilula = pilula;
        if (pilula != null) pilula.Aktivna = true;
        Pilule = pilula == null ? null : PiluleBiblioteke.Contains(pilula) ? PiluleBiblioteke : PiluleStatistike;
    }

    private void AktivirajBiblioteku(int pilula) => Aktiviraj(NavMuzika[0], PiluleBiblioteke[pilula]);

    private void AktivirajStatistiku(int pilula) => Aktiviraj(NavIzvestaj[1], PiluleStatistike[pilula]);

    private void OznaciTekucuNaStrani()
    {
        switch (Strana)
        {
            case PesmeStrana p: p.OznaciTekucu(Plejer.Tekuca); break;
            case EmisijaStrana e: e.OznaciTekucu(Plejer.Tekuca); break;
        }
    }

    public AlbumKartica Kartica(Album album)
    {
        if (_kartice.TryGetValue(album.Id, out var kartica))
        {
            if (kartica.Album != album) kartica.Osvezi(album);
            return kartica;
        }
        kartica = new AlbumKartica(album, Omoti, a => _ui.BeginInvoke(a));
        _kartice[album.Id] = kartica;
        return kartica;
    }

    // ---- library pages ----

    private static int PoIzvodjacu(Album a, Album b)
    {
        int r = Srpski.Compare(a.Izvodjac, b.Izvodjac, CompareOptions.IgnoreCase);
        if (r == 0) r = a.Godina.CompareTo(b.Godina);
        return r != 0 ? r : Srpski.Compare(a.Naziv, b.Naziv, CompareOptions.IgnoreCase);
    }

    private static string PrvoSlovo(string ime)
    {
        ime = ime.TrimStart();
        if (ime.Length == 0 || !char.IsLetter(ime[0])) return "#";
        if (ime.StartsWith("Dž", StringComparison.OrdinalIgnoreCase)) return "Dž";
        return char.ToUpper(ime[0], CultureInfo.InvariantCulture).ToString();
    }

    public void OtvoriAlbume()
    {
        AktivirajBiblioteku(0);
        PrikaziTabove = true;

        if (Prikaz == PrikazBiblioteke.Lista)
        {
            var strana = Strana is PesmeStrana { NazivReda: "Lista", Nazad: null } s && s is not StatistikaStrana
                ? s
                : new PesmeStrana { Naslov = "", NazivReda = "Lista" };
            Strana = strana;
            _osveziStranu = () => _ = FiltrirajListu(strana);
            _osveziStranu();
            return;
        }

        // Only the album wall is kept: the other walls have no title either, but hold something else.
        var zid = Strana == _zidAlbuma && _zidAlbuma != null ? _zidAlbuma : _zidAlbuma = new ZidStrana(Kartica);
        Strana = zid;
        var trenutni = zid;
        _osveziStranu = () => _ = FiltrirajZid(trenutni);
        _osveziStranu();
    }

    private CancellationToken NoviFilter()
    {
        _filter?.Cancel();
        _filter = new CancellationTokenSource();
        return _filter.Token;
    }

    private async Task FiltrirajZid(ZidStrana zid)
    {
        var token = NoviFilter();
        var upit = Upit;
        var prikaz = Prikaz;
        var grupe = await Task.Run(() =>
        {
            var albumi = Katalog.Filtriraj(new Pretraga(upit));
            return GrupisiAlbume(albumi, prikaz);
        });
        if (token.IsCancellationRequested) return;

        zid.Postavi(grupe);
        zid.Prazno = grupe.Count > 0 ? null
            : Katalog.Albumi.Count == 0 ? (Baza.Folderi().Count == 0 ? "Dodaj folder sa muzikom da bi biblioteka oživela." : "Skeniram muziku...")
            : "Ništa nije pronađeno.";
        NemaFoldera = Baza.Folderi().Count == 0;
    }

    private static List<GrupaAlbuma> GrupisiAlbume(List<Album> albumi, PrikazBiblioteke prikaz)
    {
        switch (prikaz)
        {
            case PrikazBiblioteke.AS:
                albumi.Sort(PoIzvodjacu);
                return albumi.GroupBy(a => PrvoSlovo(a.Izvodjac))
                    .Select(g => new GrupaAlbuma(g.Key, g.ToList()))
                    .ToList();

            case PrikazBiblioteke.Godina:
                albumi.Sort(PoIzvodjacu);
                return albumi.GroupBy(a => a.Godina)
                    .OrderByDescending(g => g.Key)
                    .Select(g => new GrupaAlbuma(g.Key > 0 ? g.Key.ToString() : "Bez godine", g.ToList()))
                    .ToList();

            default:
                albumi.Sort(PoIzvodjacu);
                return albumi.Count == 0 ? [] : [new GrupaAlbuma(null, albumi)];
        }
    }

    private async Task FiltrirajListu(PesmeStrana strana)
    {
        var token = NoviFilter();
        var upit = Upit;
        var redovi = await Task.Run(() =>
        {
            var q = new Pretraga(upit);
            var albumi = Katalog.Albumi.ToList();
            albumi.Sort(PoIzvodjacu);
            return Poredak.PoFolderuINumeraciji(
                albumi.SelectMany(a => a.Pesme).Where(q.Prazna ? _ => true : q.Odgovara))
                .Select(p => new PesmaRed(p)).ToList();
        });
        if (token.IsCancellationRequested) return;
        Poredak.Numerisi(redovi, static (r, broj) => r.Rb = broj);
        strana.Stavke = redovi;
        strana.Podnaslov = Rec.Pesama(redovi.Count);
        strana.Prazno = redovi.Count == 0 ? "Ništa nije pronađeno." : null;
        strana.OznaciTekucu(Plejer.Tekuca);
    }

    private void OtvoriIzvodjace()
    {
        AktivirajBiblioteku(1);
        PrikaziTabove = false;
        var strana = new IzvodjaciStrana();
        Strana = strana;
        _osveziStranu = () =>
        {
            strana.Stavke = Katalog.Albumi
                .GroupBy(a => a.Izvodjac, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.Create(new CultureInfo("sr-Latn-RS"), true))
                .Select(g => new IzvodjacStavka(g.Key, OpisAlbuma(g.Count()), g.ToList()))
                .ToList();
            strana.Podnaslov = $"{strana.Stavke.Count:N0} izvođača";
        };
        _osveziStranu();
    }

    private static string OpisAlbuma(int n) => n % 10 == 1 && n % 100 != 11 ? $"{n} album" : $"{n} albuma";

    public void OtvoriIzvodjaca(IzvodjacStavka izvodjac)
    {
        var nazad = Strana;
        var zid = new ZidStrana(Kartica) { Naslov = izvodjac.Ime, Podnaslov = izvodjac.Opis, Nazad = nazad };
        var albumi = izvodjac.Albumi.ToList();
        albumi.Sort(PoIzvodjacu);
        zid.Postavi([new GrupaAlbuma(null, albumi)]);
        Strana = zid;
    }

    private void OtvoriNedavno()
    {
        AktivirajBiblioteku(2);
        PrikaziTabove = false;
        var zid = new ZidStrana(Kartica);
        Strana = zid;
        _osveziStranu = () =>
        {
            var slusano = Katalog.Albumi.Where(a => a.PoslednjePustanje != null)
                .OrderByDescending(a => a.PoslednjePustanje).Take(24).ToList();
            var dodato = Katalog.Albumi.OrderByDescending(a => a.Dodato).ThenByDescending(a => a.Id).Take(48).ToList();
            var grupe = new List<GrupaAlbuma>();
            if (slusano.Count > 0) grupe.Add(new GrupaAlbuma("Nedavno slušano", slusano));
            if (dodato.Count > 0) grupe.Add(new GrupaAlbuma("Nedavno dodato", dodato));
            zid.Postavi(grupe);
            zid.Prazno = grupe.Count == 0 ? "Još nema albuma." : null;
        };
        _osveziStranu();
    }

    private void OtvoriOmiljeno()
    {
        AktivirajBiblioteku(3);
        PrikaziTabove = false;
        var strana = new PesmeStrana { NazivReda = "Omiljeno" };
        Strana = strana;
        _osveziStranu = () =>
        {
            strana.Stavke = Katalog.Pesme.Where(p => p.Omiljeno).Select(p => new PesmaRed(p)).ToList();
            Poredak.Numerisi(strana.Stavke, static (r, broj) => r.Rb = broj);
            strana.Podnaslov = Rec.Pesama(strana.Stavke.Count);
            strana.Prazno = strana.Stavke.Count == 0 ? "Desni klik na pesmu i „Dodaj u omiljeno“." : null;
            strana.OznaciTekucu(Plejer.Tekuca);
        };
        _osveziStranu();
    }

    private void OtvoriNajslusanije()
    {
        AktivirajStatistiku(0);
        PrikaziTabove = false;
        var strana = new StatistikaStrana(UcitajNajslusanije) { NazivReda = "Najslušanije", PrikaziDodatak = true };
        Strana = strana;
        _osveziStranu = () => UcitajNajslusanije(strana);
        _osveziStranu();
    }

    private void UcitajNajslusanije(StatistikaStrana strana)
    {
        var sada = DateTime.Now;
        // The week starts on Monday here.
        int odPonedeljka = ((int)sada.DayOfWeek + 6) % 7;
        var od = strana.Mesec ? new DateTime(sada.Year, sada.Month, 1) : sada.Date.AddDays(-odPonedeljka);

        var redovi = new List<PesmaRed>();
        foreach (var (pesmaId, puta) in Baza.Najslusanije(od))
        {
            if (Katalog.NadjiPesmu(pesmaId) is not { } pesma) continue;
            redovi.Add(new PesmaRed(pesma, (redovi.Count + 1).ToString()) { Dodatak = puta == 1 ? "1 put" : $"{puta} puta" });
        }
        strana.Stavke = redovi;
        strana.Podnaslov = strana.Mesec ? $"od 1. {sada:MM.} ovog meseca" : $"od ponedeljka {od:dd.MM.}";
        strana.Prazno = redovi.Count == 0 ? "U ovom periodu još ništa nije slušano." : null;
        strana.OznaciTekucu(Plejer.Tekuca);
    }

    private void OtvoriNijePustano()
    {
        AktivirajStatistiku(1);
        PrikaziTabove = false;
        var zid = new ZidStrana(Kartica);
        Strana = zid;
        _osveziStranu = () =>
        {
            var granica = DateTime.Now.AddYears(-1);
            var davno = Katalog.Albumi.Where(a => a.PoslednjePustanje < granica).OrderBy(a => a.PoslednjePustanje).ToList();
            var nikad = Katalog.Albumi.Where(a => a.PoslednjePustanje == null).ToList();
            nikad.Sort(PoIzvodjacu);
            var grupe = new List<GrupaAlbuma>();
            if (davno.Count > 0) grupe.Add(new GrupaAlbuma("Puštano pre više od godinu dana", davno));
            if (nikad.Count > 0) grupe.Add(new GrupaAlbuma("Nikad puštano", nikad));
            zid.Postavi(grupe);
            zid.Podnaslov = OpisAlbuma(davno.Count + nikad.Count);
            zid.Prazno = grupe.Count == 0 ? "Sve je puštano u poslednjih godinu dana." : null;
        };
        _osveziStranu();
    }

    public void OtvoriAlbum(Album album)
    {
        var nazad = Strana;
        var strana = new PesmeStrana
        {
            Naslov = album.Naziv,
            Podnaslov = PodnaslovAlbuma(album),
            Album = album,
            Kartica = Kartica(album),
            NazivReda = album.Naziv,
            Nazad = nazad,
            Stavke = album.Pesme.Select(p => new PesmaRed(p, p.Broj > 0 ? p.Broj.ToString() : "")).ToList(),
        };
        PrikaziTabove = false;
        Strana = strana;
        OznaciTekucuNaStrani();
    }

    private static string PodnaslovAlbuma(Album a)
    {
        var delovi = new List<string> { a.Izvodjac };
        if (a.Godina > 0) delovi.Add(a.Godina.ToString());
        delovi.Add(Rec.Pesama(a.Pesme.Count));
        delovi.Add(Podaci.Vreme.Dugo(a.Trajanje));
        return string.Join(" · ", delovi);
    }

    [RelayCommand]
    private void Nazad()
    {
        if (Strana?.Nazad is not { } prethodna) return;
        Strana = prethodna;
        PrikaziTabove = _aktivnaPilula == PiluleBiblioteke[0] && prethodna.Nazad == null;
        OznaciTekucuNaStrani();
    }

    // ---- playing ----

    /// <param name="odPesme">The song the user picked. Without one, shuffle starts anywhere in the album.</param>
    public void PustiAlbum(Album album, int? odPesme = null)
    {
        if (!Dan.SmePrekid()) return;
        int od = odPesme ?? (Plejer.Mesanje ? Random.Shared.Next(album.Pesme.Count) : 0);
        Plejer.Red.Postavi(album.Pesme, od, album.Naziv, jeAlbum: true);
        _ = Plejer.PustiTekucu();
    }

    /// <summary>
    /// Play with an empty queue: the list on screen from the picked song (or the first one), the album or the show
    /// that is open, or the first album on the wall.
    /// </summary>
    private bool PustiSaEkrana()
    {
        switch (Strana)
        {
            case PesmeStrana { Stavke.Count: > 0 } strana:
                var pesme = strana.Stavke.Select(s => s.Pesma).ToList();
                int od = Math.Max(0, Izabrana.Izabrana is { } izabrana ? pesme.FindIndex(p => p.Id == izabrana.Id) : 0);
                if (strana.Album is { } album && album.Pesme.Contains(pesme[od])) PustiAlbum(album, album.Pesme.IndexOf(pesme[od]));
                else PustiPesme(pesme, od, strana.NazivReda);
                return true;

            case EmisijaStrana { Stavke.Count: > 0 } emisija:
                int odEmisije = Math.Max(0, Izabrana.Izabrana is { } i ? emisija.Stavke.ToList().FindIndex(s => s.Pesma.Id == i.Id) : 0);
                PustiEmisiju(emisija.Emisija, odEmisije);
                return true;

            case ZidStrana { PrviAlbum: { } prvi }:
                PustiAlbum(prvi);
                return true;
            // A day starts with its own button; Play there keeps doing what it did.
            case DanStrana:
                return false;
        }

        PrikaziPoruku("Red je prazan. Izaberi album ili pesmu, pa Pusti.");
        return true;
    }

    public void PustiPesme(IReadOnlyList<Pesma> pesme, int od, string naziv)
    {
        if (!Dan.SmePrekid()) return;
        Plejer.Red.Postavi(pesme, od, naziv);
        _ = Plejer.PustiTekucu();
    }

    public void PustiEmisiju(Emisija emisija, int od = 0)
    {
        if (!Dan.SmePrekid()) return;
        var pesme = PesmeEmisije(emisija);
        if (pesme.Count == 0)
        {
            PrikaziPoruku($"Emisija „{emisija.Ime}“ je prazna. Desni klik na album ili pesmu i „Dodaj u emisiju“.");
            return;
        }
        Plejer.Red.Postavi(pesme, od, emisija.Ime, emisija);
        _ = Plejer.PustiTekucu();
    }

    public void DodajURed(IReadOnlyList<Pesma> pesme)
    {
        bool prazan = Plejer.Red.Ukupno == 0;
        if (prazan)
        {
            PustiPesme(pesme, 0, "Red");
            return;
        }
        Plejer.Red.DodajNaKraj(pesme);
        Plejer.PripremiSledecu();
        Plejer.OsveziSledi();
        PrikaziPoruku(Rec.T("Dodato na kraj reda: {0}.", Opis(pesme)));
    }

    private static string Opis(IReadOnlyList<Pesma> pesme) =>
        pesme.Count == 1 ? $"„{pesme[0].Naslov}“" : Rec.Pesama(pesme.Count);

    public void DodajSledece(IReadOnlyList<Pesma> pesme)
    {
        if (Plejer.Red.Ukupno == 0)
        {
            PustiPesme(pesme, 0, "Red");
            return;
        }
        Plejer.Red.DodajSledece(pesme);
        Plejer.PripremiSledecu();
        Plejer.OsveziSledi();
        PrikaziPoruku($"Sledi posle ove pesme: {Opis(pesme)}.");
    }

    public void UkloniAlbum(Album album)
    {
        Plejer.UkloniAlbumIzReda(album);
        Baza.IskljuciAlbum(album.Id);
        if (Strana is PesmeStrana { Album: not null } strana && strana.Album.Id == album.Id) Nazad();
        Katalog.Ucitaj();
        PrikaziPoruku($"Album „{album.Naziv}“ je uklonjen iz biblioteke. Vraćaš ga u Podešavanjima.");
    }

    public void UkloniPesme(IReadOnlyList<Pesma> pesme)
    {
        var uklonjene = pesme.Select(p => p.Id).ToHashSet();
        Plejer.UkloniPesmeIzReda(uklonjene);
        Baza.IskljuciPesme(uklonjene);
        if (Strana is PesmeStrana strana)
        {
            strana.Stavke = strana.Stavke.Where(r => !uklonjene.Contains(r.Pesma.Id)).ToList();
            if (strana.Album != null && strana.Stavke.Count == 0) Nazad();
        }
        Katalog.Ucitaj();
        PrikaziPoruku($"Iz spiska je uklonjeno: {Rec.Pesama(uklonjene.Count)}. Vraćaš ih u Podešavanjima.");
    }

    public void VratiAlbume()
    {
        Baza.VratiIskljucene();
        Katalog.Ucitaj();
    }

    public void PromeniOmiljeno(IReadOnlyList<Pesma> pesme)
    {
        bool novo = !pesme.All(p => p.Omiljeno);
        foreach (var p in pesme)
        {
            p.Omiljeno = novo;
            Baza.PostaviOmiljeno(p.Id, novo);
        }
        if (_aktivnaPilula == PiluleBiblioteke[3]) _osveziStranu?.Invoke();
        else if (Strana is PesmeStrana strana)
        {
            var izmenjene = pesme.Select(p => p.Id).ToHashSet();
            foreach (var red in strana.Stavke.Where(r => izmenjene.Contains(r.Pesma.Id)))
                red.Omiljeno = novo;
        }
        PrikaziPoruku(novo ? "Dodato u omiljeno." : "Uklonjeno iz omiljenog.");
    }

    public static void OtvoriFolder(Pesma pesma)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{pesma.Putanja}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Dnevnik.Greska("Otvaranje foldera", ex);
        }
    }

    // ---- shows ----

    private void OsveziNavEmisije()
    {
        NavEmisije.Clear();
        foreach (var e in Baza.Emisije())
            NavEmisije.Add(new NavStavka(e.Ime, "", () => OtvoriEmisiju(e), e.Boja) { Emisija = e });
        NemaEmisija = NavEmisije.Count == 0;
        _emisijeStrana.Prazno = NemaEmisija ? "Još nema emisija. Napravi prvu dugmetom Nova emisija." : null;
    }

    public void OtvoriEmisije()
    {
        AktivirajBiblioteku(4);
        PrikaziTabove = false;
        Strana = _emisijeStrana;
        _osveziStranu = null;
    }

    public List<Pesma> PesmeEmisije(Emisija e) =>
        Baza.PesmeEmisije(e.Id).Select(Katalog.NadjiPesmu).OfType<Pesma>().ToList();

    public void OtvoriEmisiju(Emisija emisija)
    {
        AktivirajBiblioteku(4);
        PrikaziTabove = false;
        var strana = new EmisijaStrana(emisija) { Naslov = emisija.Ime, Nazad = _emisijeStrana };
        Strana = strana;
        _osveziStranu = () => UcitajEmisiju(strana);
        _osveziStranu();
    }

    private void UcitajEmisiju(EmisijaStrana strana)
    {
        var emisija = strana.Emisija;
        var pesme = PesmeEmisije(emisija);

        strana.Stavke.Clear();
        for (int i = 0; i < pesme.Count; i++)
            strana.Stavke.Add(new PesmaRed(pesme[i], (i + 1).ToString()));

        OsveziUpozorenja(strana);
        OsveziPodnaslovEmisije(strana);
        strana.OznaciTekucu(Plejer.Tekuca);

        strana.Istorija = Baza.IstorijaEmisije(emisija.Id)
            .Select(h => (Pustanje: h, Pesma: Katalog.NadjiPesmu(h.PesmaId)))
            .Where(x => x.Pesma != null)
            .Select(x => new RedIstorije(x.Pustanje.Vreme.ToString("dd.MM.yyyy. HH:mm"), x.Pesma!.Naslov, x.Pesma.Izvodjac))
            .ToList();
    }

    private void OsveziPodnaslovEmisije(EmisijaStrana strana)
    {
        double ukupno = KrajEmisije.Preostalo(0, 0, strana.Stavke.Select(s => s.Pesma.Trajanje), 0);
        double sPrelazima = strana.Stavke.Count == 0 ? 0
            : KrajEmisije.Preostalo(strana.Stavke[0].Pesma.Trajanje, strana.Stavke[0].Pesma.Trajanje,
                strana.Stavke.Skip(1).Select(s => s.Pesma.Trajanje), Podesavanja.Prelaz);
        strana.Podnaslov = strana.Stavke.Count == 0
            ? "Prazna emisija"
            : $"{Rec.Pesama(strana.Stavke.Count)} · {Podaci.Vreme.Dugo(ukupno)} · sa prelazima {Podaci.Vreme.Dugo(sPrelazima)}";
    }

    public string? UpozorenjeZa(DateTime pustano)
    {
        int nedelja = (int)((DateTime.Now - pustano).TotalDays / 7);
        var kada = nedelja switch
        {
            0 => "ove nedelje",
            1 => "pre nedelju dana",
            _ => $"pre {nedelja} nedelja",
        };
        return $"Puštano u ovoj emisiji {kada} ({pustano:dd.MM.yyyy.})";
    }

    private void OsveziUpozorenja(EmisijaStrana strana)
    {
        var granica = DateTime.Now.AddDays(-7 * Podesavanja.UpozorenjeNedelja);
        var skoro = Baza.PoslednjeUEmisiji(strana.Emisija.Id, granica);
        foreach (var s in strana.Stavke)
            s.Upozorenje = skoro.TryGetValue(s.Pesma.Id, out var kada) ? UpozorenjeZa(kada) : null;
    }

    public void DodajUEmisiju(Emisija emisija, IReadOnlyList<Pesma> pesme)
    {
        var ids = Baza.PesmeEmisije(emisija.Id);
        ids.AddRange(pesme.Select(p => p.Id));
        Baza.SacuvajPesmeEmisije(emisija.Id, ids);

        var granica = DateTime.Now.AddDays(-7 * Podesavanja.UpozorenjeNedelja);
        var skoro = Baza.PoslednjeUEmisiji(emisija.Id, granica);
        var ponovljene = pesme.Where(p => skoro.ContainsKey(p.Id)).ToList();

        if (ponovljene.Count == 1)
            PrikaziPoruku($"Pazi: „{ponovljene[0].Naslov}“ je u emisiji {emisija.Ime} puštana {skoro[ponovljene[0].Id]:dd.MM.yyyy.}");
        else if (ponovljene.Count > 1)
            PrikaziPoruku($"Pazi: {Rec.Pesama(ponovljene.Count)} je puštano u emisiji {emisija.Ime} u poslednjih {Podesavanja.UpozorenjeNedelja} nedelja.");
        else
            PrikaziPoruku($"Dodato u emisiju {emisija.Ime}: {Rec.Pesama(pesme.Count)}.");

        NakonIzmeneEmisije(emisija);
    }

    /// <summary>Saves the order shown on the page, after a drag or a removal.</summary>
    public void SacuvajEmisiju(EmisijaStrana strana)
    {
        Baza.SacuvajPesmeEmisije(strana.Emisija.Id, strana.Stavke.Select(s => s.Pesma.Id).ToList());
        for (int i = 0; i < strana.Stavke.Count; i++) strana.Stavke[i].Rb = (i + 1).ToString();
        NakonIzmeneEmisije(strana.Emisija);
    }

    private void NakonIzmeneEmisije(Emisija emisija)
    {
        if (Plejer.Red.Emisija?.Id == emisija.Id)
        {
            Plejer.Red.OsveziEmisiju(PesmeEmisije(emisija));
            Plejer.PripremiSledecu();
        }
        if (Strana is EmisijaStrana strana && strana.Emisija.Id == emisija.Id) UcitajEmisiju(strana);
    }

    public void NovaEmisija(string ime, string boja)
    {
        var e = Baza.NovaEmisija(ime, boja);
        OsveziNavEmisije();
        OtvoriEmisiju(e);
    }

    /// <summary>Keeps the queue as a new show, in the order it plays.</summary>
    public void SacuvajRedKaoEmisiju(string ime, string boja)
    {
        var e = Baza.NovaEmisija(ime, boja);
        var pesme = Plejer.Red.SvePesme.ToList();
        Baza.SacuvajPesmeEmisije(e.Id, pesme.Select(p => p.Id).ToList());
        OsveziNavEmisije();
        PrikaziPoruku($"Napravljena emisija {ime}: {Rec.Pesama(pesme.Count)}.");
    }

    public void IzmeniEmisiju(Emisija emisija, string ime, string boja)
    {
        emisija.Ime = ime;
        emisija.Boja = boja;
        Baza.IzmeniEmisiju(emisija);
        OsveziNavEmisije();
        (Strana as EmisijaStrana)?.OsveziNaslov();
    }

    public void ObrisiEmisiju(Emisija emisija)
    {
        Baza.ObrisiEmisiju(emisija.Id);
        OsveziNavEmisije();
        OtvoriEmisije();
    }

    // ---- folders ----

    public void DodajFolder(string putanja)
    {
        Baza.DodajFolder(putanja);
        NemaFoldera = false;
        Skener.PratiFoldere();
        _ = Skener.SkenirajAsync();
    }

    /// <summary>
    /// Adds what the user picked or dropped, and puts it in the queue: it starts playing if nothing plays.
    /// A folder also joins the library and stays watched. Single songs are added as they are.
    /// </summary>
    public async Task DodajMuzikuAsync(IReadOnlyList<string> putanje)
    {
        putanje = putanje.Select(Path.GetFullPath).ToList();
        var folderi = putanje.Where(Directory.Exists).ToList();
        var fajlovi = putanje.Where(p => File.Exists(p) && Tagovi.JeMuzika(p)).ToList();
        if (folderi.Count == 0 && fajlovi.Count == 0)
        {
            PrikaziPoruku("Tu nema muzike. Plejer pušta MP3, FLAC, OGG, OPUS, M4A i WAV.");
            return;
        }

        try
        {
            if (folderi.Count > 0)
            {
                PrikaziPoruku("Čitam muziku. Pesme stižu u red čim se čitanje završi.");
                foreach (var f in folderi) Baza.DodajFolder(f);
                Baza.UkljuciPutanje(folderi);
                NemaFoldera = false;
                Skener.PratiFoldere();
                await Skener.SkenirajAsync();
                Katalog.Ucitaj();
            }
            if (fajlovi.Count > 0) await Skener.DodajFajloveAsync(fajlovi);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var tacni = fajlovi.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prefiksi = folderi.Select(f => f.TrimEnd('\\') + "\\").ToList();
        // Folder, then the number from the file name: a numbered folder keeps its running order
        // instead of arriving in alphabet (user, 7. 11. 2026).
        var pesme = Poredak.PoFolderuINumeraciji(
            Katalog.Albumi.SelectMany(a => a.Pesme)
                .Where(p => tacni.Contains(p.Putanja) || prefiksi.Exists(f => p.Putanja.StartsWith(f, StringComparison.OrdinalIgnoreCase))));

        if (pesme.Count == 0)
        {
            PrikaziPoruku("U izabranom folderu nema muzike.");
            return;
        }

        // A whole album on an empty queue plays as an album, without a crossfade.
        var album = pesme[0].Album;
        if (Plejer.Red.Ukupno == 0 && pesme.All(p => p.Album.Id == album.Id) && pesme.Count == album.Pesme.Count)
            PustiAlbum(album);
        else
            DodajURed(pesme);
    }

    public void UkloniFolder(string putanja)
    {
        Baza.UkloniFolder(putanja);
        Skener.PratiFoldere();
        Katalog.Ucitaj();
        NemaFoldera = Baza.Folderi().Count == 0;
    }

    public void Dispose()
    {
        Podesavanja.Sacuvaj();
        Skener.Dispose();
        Plejer.Dispose();
        Baza.Dispose();
    }
}
