using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Bronxamp.Podaci;
using Bronxamp.Prikaz;
using Bronxamp.Sistem;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bronxamp.Vm;

public enum TabDana { Priprema, Dan, Jinglovi }

public enum PrikazDana { Sve, Pesme, Zadato }

public sealed record OstatakRed(string Vreme, string Naziv);

// The drop-down lists show ToString in their closed state.
public sealed record IzborDana(DateTime Datum, string Naziv)
{
    public override string ToString() => Naziv;
}

public sealed record IzborFajla(string? Putanja, string Naziv)
{
    public override string ToString() => Naziv;
}

/// <summary>
/// A folder of songs without a time (it fills the day in order), or a file at the top of the schedule folder that
/// is not the hit. Typing a time makes it a block or a show at that time.
/// </summary>
public sealed partial class PlejlistaRed(string putanja, Action izmenjeno, Action<PlejlistaRed> dobiloVreme) : ObservableObject
{
    public string Folder { get; } = putanja;
    public bool JeFajl { get; } = File.Exists(putanja);
    public string Naziv { get; } = File.Exists(putanja) ? Path.GetFileNameWithoutExtension(putanja) : Path.GetFileName(putanja);
    public int Pesama { get; } = File.Exists(putanja) ? 1 : IzvorDana.Fajlovi(putanja).Count;

    [ObservableProperty] private string _opis = "";
    [ObservableProperty] private bool _ukljucena = true;
    [ObservableProperty] private string _vreme = "";

    partial void OnUkljucenaChanged(bool value) => izmenjeno();

    partial void OnVremeChanged(string value)
    {
        if (DanStrana.ProcitajVreme(value) != null) dobiloVreme(this);
    }
}

public sealed partial class EmisijaRed(string naziv, string izvor) : ObservableObject
{
    public string Naziv { get; } = naziv;

    /// <summary>A file, or a folder of songs.</summary>
    public string Izvor { get; } = izvor;

    /// <summary>A folder is a block of songs that takes jingles and the hit. A single file is not touched.</summary>
    public bool JeFolder { get; } = Directory.Exists(izvor);

    /// <summary>The time was in the folder's name, so nobody typed it.</summary>
    public bool VremeIzImena { get; init; }

    public Action? Izmenjeno { get; set; }

    [ObservableProperty] private string _vreme = "";
    [ObservableProperty] private IzborFajla? _jingl;
    [ObservableProperty] private string _opis = "";

    partial void OnVremeChanged(string value)
    {
        // Half a time ("20:", "2") is not worth building a day for; the whole one is.
        if (DanStrana.ProcitajVreme(value) != null) Izmenjeno?.Invoke();
    }

    partial void OnJinglChanged(IzborFajla? value) => Izmenjeno?.Invoke();
}

/// <summary>One line of the day by the hours. A click opens its songs.</summary>
public sealed partial class BlokDana(string vreme, string naziv, VrstaDela vrsta, string opis) : ObservableObject
{
    public string Vreme { get; } = vreme;
    public string Naziv { get; } = naziv;
    public VrstaDela Vrsta { get; } = vrsta;
    public string Opis { get; } = opis;
    public bool JeHit => Vrsta is VrstaDela.Hit or VrstaDela.Pocetak;
    public bool JeEmisija => Vrsta == VrstaDela.Emisija;

    /// <summary>The last line of the day: nothing to open there.</summary>
    public bool JeKraj { get; init; }

    public string? Upozorenje { get; init; }
    public bool ImaUpozorenje => Upozorenje != null;
    public string? Visak { get; init; }
    public IReadOnlyList<DanRed> Stavke { get; init; } = [];
    public IReadOnlyList<string> Izostavljene { get; init; } = [];
    public bool ImaIzostavljenih => Izostavljene.Count > 0;

    [ObservableProperty] private bool _otvoren;
}

public sealed partial class JinglRed(string putanja, VrstaJingla vrsta, Action<JinglRed> izmenjeno) : ObservableObject
{
    public string Putanja { get; } = putanja;
    public string Naziv { get; } = Path.GetFileNameWithoutExtension(putanja);

    [ObservableProperty] private VrstaJingla _vrsta = vrsta;

    partial void OnVrstaChanged(VrstaJingla value) => izmenjeno(this);
}

public sealed partial class DanRed(StavkaDana stavka) : ObservableObject
{
    public StavkaDana Stavka { get; } = stavka;

    public string Izvodjac => Stavka.Fajl.Izvodjac;
    public string Naslov => Stavka.Fajl.Naslov;
    public string Ceo => Izvodjac.Length > 0 ? $"{Izvodjac} - {Naslov}" : Naslov;
    public string Trajanje => Vreme.Kratko(Stavka.Fajl.Trajanje);
    public string Zadato => Stavka.Zadato?.ToString("HH:mm:ss") ?? "";
    public VrstaStavke Vrsta => Stavka.Vrsta;
    public string VrstaNaziv => DanStrana.NazivVrste(Stavka.Vrsta);
    public bool Istaknuta => Stavka.Vrsta is VrstaStavke.HitDana or VrstaStavke.Emisija or VrstaStavke.Spica;
    public bool JeJingl => Stavka.Vrsta is VrstaStavke.Jingl or VrstaStavke.JinglHit or VrstaStavke.JinglEmisije;

    [ObservableProperty] private string _pocetak = "";
    [ObservableProperty] private string _odstupanje = "";
    [ObservableProperty] private bool _vanGranice;
    [ObservableProperty] private bool _tekuca;
    [ObservableProperty] private bool _gotova;

    public void Osvezi(double tolerancija)
    {
        Pocetak = Stavka.Pocetak.ToString("HH:mm:ss");
        if (Stavka.Zadato is not { } zadato)
        {
            Odstupanje = "";
            VanGranice = false;
            return;
        }

        double sekundi = Math.Round((Stavka.Pocetak - zadato).TotalSeconds);
        var t = TimeSpan.FromSeconds(Math.Abs(sekundi));
        Odstupanje = $"{(sekundi < 0 ? "-" : "+")}{(int)t.TotalMinutes:00}:{t.Seconds:00}";
        VanGranice = Math.Abs(sekundi) > (Stavka.Okvirno ? TokDana.PragRupeOkvirno : tolerancija);
    }
}

/// <summary>"Sastavi dan": the people say what goes on air, the program builds the day and plays it by the clock.</summary>
public sealed partial class DanStrana : Strana
{
    private const int PrviTermin = 11;
    private const int PoslednjiTermin = 23;
    private static readonly TimeSpan PocetakPrograma = new(8, 59, 45);
    private static readonly string[] Dani = ["nedelja", "ponedeljak", "utorak", "sreda", "četvrtak", "petak", "subota"];

    private readonly PlejerVm _plejer;
    private readonly Podesavanja _podesavanja;
    private readonly DispatcherTimer _sat;
    private readonly ConcurrentDictionary<string, (long Izmenjeno, FajlDana Fajl)> _procitano = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Pesma, StavkaDana> _uRedu = [];
    private readonly Dictionary<StavkaDana, Pesma> _pesmeDana = [];
    private readonly BudanRacunar _budan = new();

    private bool _vracaPocetak;
    private SlozenDan? _dan;
    private int _tekuca = -1;
    private DateTime _pustenoU;

    /// <summary>Tests run a day on their own clock.</summary>
    public Func<DateTime> Sada { get; set; } = () => DateTime.Now;

    /// <summary>Asks whether to stop the running day. The view shows the question.</summary>
    public Func<bool>? PitajPrekid { get; set; }

    public event Action<string>? Poruka;

    public ObservableCollection<IzborDana> Datumi { get; } = [];
    public ObservableCollection<IzborFajla> Hitovi { get; } = [];
    public ObservableCollection<PlejlistaRed> Plejliste { get; } = [];

    /// <summary>Files at the top of the schedule folder besides the hit: usually a show in one file, waiting for its time.</summary>
    public ObservableCollection<PlejlistaRed> OstaliFajlovi { get; } = [];
    public ObservableCollection<EmisijaRed> Emisije { get; } = [];
    public ObservableCollection<IzborFajla> JingloviEmisija { get; } = [];
    public ObservableCollection<IzborFajla> SviJinglovi { get; } = [];
    public ObservableCollection<JinglRed> Jinglovi { get; } = [];
    public ObservableCollection<DanRed> Stavke { get; } = [];
    public ObservableCollection<string> Problemi { get; } = [];
    public ObservableCollection<OstatakRed> Ostatak { get; } = [];
    public ObservableCollection<BlokDana> Blokovi { get; } = [];

    /// <summary>The list on screen follows the song that is playing.</summary>
    public event Action<DanRed>? TekucaPromenjena;

    private static readonly Brush BojaMirna = Zamrznuto(0x2B, 0x2B, 0x33);
    private static readonly Brush BojaCeka = Zamrznuto(0x6B, 0x52, 0x14);
    private static readonly Brush BojaTraje = Zamrznuto(0x7A, 0x4A, 0x12);

    private static Brush Zamrznuto(byte r, byte g, byte b)
    {
        var cetka = new SolidColorBrush(Color.FromRgb(r, g, b));
        cetka.Freeze();
        return cetka;
    }

    public Brush BojaStanja => Aktivan ? BojaTraje : Ceka ? BojaCeka : BojaMirna;
    public string NatpisStanja => Aktivan ? Rec.T("Dan svira") : Ceka ? Rec.T("Čeka") : Rec.T("Dan nije pokrenut");
    public bool DanTraje => Aktivan || Ceka;

    /// <summary>The right panel shows the day while it runs, and a built day while nothing else is queued.</summary>
    public bool PrikaziOstatak => DanTraje || (Ostatak.Count > 0 && !_plejer.ImaRed);

    public void NaPromenuReda() => OnPropertyChanged(nameof(PrikaziOstatak));

    public static IReadOnlyList<VrstaJingla> VrsteJinglova { get; } = Enum.GetValues<VrstaJingla>();

    [ObservableProperty] private TabDana _tab;
    [ObservableProperty] private IzborDana? _izabranDan;
    [ObservableProperty] private IzborFajla? _izabranHit;
    [ObservableProperty] private IzborFajla? _spica;
    [ObservableProperty] private IzborFajla? _pocetniJinglHita;
    [ObservableProperty] private IzborFajla? _pocetniJingl;
    [ObservableProperty] private string _folderSema = "";
    [ObservableProperty] private string _folderJinglovi = "";
    [ObservableProperty] private string _tolerancijaTekst = "";
    [ObservableProperty] private bool _radi;
    [ObservableProperty] private bool _aktivan;
    [ObservableProperty] private bool _ceka;
    [ObservableProperty] private string _stanje = Rec.T("Dan nije složen.");
    [ObservableProperty] private string _opisDana = "";
    [ObservableProperty] private string _opisJinglova = "";
    [ObservableProperty] private string _opisStanja = "";
    [ObservableProperty] private string? _karticaZadato;
    [ObservableProperty] private string _karticaZadatoVreme = "";
    [ObservableProperty] private string _krajPrograma = "00:00:00";
    [ObservableProperty] private PrikazDana _prikaz;
    [ObservableProperty] private bool _naprednoOtvoreno;

    /// <summary>"ok", "paznja" or "prazno": the colour of the light on top of the page.</summary>
    [ObservableProperty] private string _semafor = "prazno";
    [ObservableProperty] private string _naslovSemafora = Rec.T("Dan još nije složen.");

    /// <summary>The hit was found by the program, not picked by the people.</summary>
    [ObservableProperty] private bool _hitNadjenSam;
    [ObservableProperty] private string _opisHita = "";

    private readonly DispatcherTimer _odlozenoSlaganje;
    private bool _tiho;
    private bool _josJednom;

    public bool NaPripremi => Tab == TabDana.Priprema;
    public bool NaDanu => Tab == TabDana.Dan;
    public bool NaJinglovima => Tab == TabDana.Jinglovi;
    public bool MozeIzmena => !Aktivan && !Ceka;
    public bool MozePokretanje => _dan != null && !Aktivan && !Ceka;
    public bool MozeZaustavljanje => Aktivan || Ceka;
    public bool MozeNazad => IzabranDan != null && Datumi.IndexOf(IzabranDan) > 0;
    public bool MozeNapred => IzabranDan != null && Datumi.IndexOf(IzabranDan) < Datumi.Count - 1;

    [RelayCommand]
    private void PrethodniDan()
    {
        if (MozeNazad) IzabranDan = Datumi[Datumi.IndexOf(IzabranDan!) - 1];
    }

    [RelayCommand]
    private void SledeciDan()
    {
        if (MozeNapred) IzabranDan = Datumi[Datumi.IndexOf(IzabranDan!) + 1];
    }

    partial void OnPrikazChanged(PrikazDana value) => CollectionViewSource.GetDefaultView(Stavke).Refresh();

    private bool ProdjeFilter(object o) => o is not DanRed r || Prikaz switch
    {
        PrikazDana.Pesme => r.Vrsta is VrstaStavke.Pesma or VrstaStavke.HitDana or VrstaStavke.Emisija,
        PrikazDana.Zadato => r.Stavka.Zadato != null,
        _ => true,
    };

    partial void OnTabChanged(TabDana value)
    {
        Naslov = value switch
        {
            TabDana.Dan => "U etru",
            TabDana.Jinglovi => "Jinglovi",
            _ => "Sastavi dan",
        };
        OnPropertyChanged(nameof(NaPripremi));
        OnPropertyChanged(nameof(NaDanu));
        OnPropertyChanged(nameof(NaJinglovima));
    }

    partial void OnAktivanChanged(bool value) => OsveziDugmad();

    partial void OnRadiChanged(bool value)
    {
        if (_dan == null) OsveziSemafor();
    }

    partial void OnCekaChanged(bool value) => OsveziDugmad();

    private void OsveziDugmad()
    {
        OnPropertyChanged(nameof(BojaStanja));
        OnPropertyChanged(nameof(NatpisStanja));
        OnPropertyChanged(nameof(DanTraje));
        OnPropertyChanged(nameof(PrikaziOstatak));
        OnPropertyChanged(nameof(MozeIzmena));
        OnPropertyChanged(nameof(MozePokretanje));
        OnPropertyChanged(nameof(MozeZaustavljanje));
    }

    public DanStrana(PlejerVm plejer, Podesavanja podesavanja, Dispatcher ui)
    {
        _plejer = plejer;
        _podesavanja = podesavanja;
        Naslov = "Sastavi dan";
        CollectionViewSource.GetDefaultView(Stavke).Filter = ProdjeFilter;
        _folderSema = podesavanja.FolderSema;
        _folderJinglovi = podesavanja.FolderJinglovi;
        _tolerancijaTekst = podesavanja.Tolerancija.ToString("0");

        _plejer.Poruka += p =>
        {
            if (Aktivan) UpisiUDnevnik($"GREŠKA  {p}");
        };

        _sat = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromMilliseconds(200) };
        _sat.Tick += (_, _) => Otkucaj();

        // The day builds itself a moment after the last change, so there is no "build" button to forget.
        _odlozenoSlaganje = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromMilliseconds(700) };
        _odlozenoSlaganje.Tick += (_, _) =>
        {
            _odlozenoSlaganje.Stop();
            _ = Slozi();
        };
    }

    /// <summary>Something the people changed: build the day again, once things settle.</summary>
    private void Izmenjeno()
    {
        if (_tiho || !MozeIzmena) return;
        _odlozenoSlaganje.Stop();
        _odlozenoSlaganje.Start();
    }

    public static TimeSpan? ProcitajVreme(string tekst)
    {
        var m = System.Text.RegularExpressions.Regex.Match(tekst.Trim(), @"^(\d{1,2})(?:[:.](\d{2})|h)$");
        if (!m.Success) return null;
        int sati = int.Parse(m.Groups[1].Value), minuta = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
        return sati <= 23 && minuta <= 59 ? new TimeSpan(sati, minuta, 0) : null;
    }

    public static string NazivVrste(VrstaStavke v) => v switch
    {
        VrstaStavke.Pesma => "Pesma",
        VrstaStavke.Jingl => "Jingl",
        VrstaStavke.JinglHit => "Jingl hita",
        VrstaStavke.HitDana => "HIT DANA",
        VrstaStavke.Spica => "Špica",
        VrstaStavke.JinglEmisije => "Jingl emisije",
        _ => "Emisija",
    };

    public static string NazivVrste(VrstaJingla v) => v switch
    {
        VrstaJingla.Opsti => "Nasumičan",
        VrstaJingla.HitDana => "Hit dana",
        VrstaJingla.Spica => "Špica",
        VrstaJingla.Emisija => "Emisija",
        VrstaJingla.Retko => "Ručno",
        _ => "Ne koristi se",
    };

    // ---- preparing ----

    /// <summary>Reads the folders again. They change outside the program, so this runs every time the page opens.</summary>
    public void Osvezi()
    {
        try
        {
            Procitaj();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dnevnik.Greska("Čitanje foldera dana", ex);
            Stanje = Rec.T("Folder sa šemom ili jinglovima nije dostupan.");
        }
    }

    private void Procitaj()
    {
        _tiho = true;
        try
        {
            var danas = Sada().Date;
            if (Datumi.Count == 0 || Datumi[0].Datum != danas)
            {
                Datumi.Clear();
                for (int i = 0; i < 14; i++)
                {
                    var d = danas.AddDays(i);
                    Datumi.Add(new IzborDana(d, $"{Dani[(int)d.DayOfWeek]} {d:dd.MM.yyyy.}"));
                }
                IzabranDan = Datumi[0];
            }

            OsveziJinglove();
            OsveziSemu();
        }
        finally
        {
            _tiho = false;
        }

        // A day already built (maybe changed by hand) stays as it is; otherwise it is built right away.
        if (_dan == null && MozeIzmena) Izmenjeno();
    }

    private void OsveziSemu()
    {
        var bioHit = IzabranHit?.Putanja;
        Hitovi.Clear();
        var bile = Plejliste.ToDictionary(p => p.Folder, p => p.Ukljucena, StringComparer.OrdinalIgnoreCase);
        Plejliste.Clear();
        if (!Directory.Exists(FolderSema))
        {
            Stanje = $"Folder „{FolderSema}“ nije dostupan.";
            return;
        }

        // A file whose name starts with a time is a show at that time, and so is one that got a time before;
        // the rest may be the hit.
        foreach (var f in IzvorDana.Fajlovi(FolderSema).Where(f => Emisije.All(e => e.Izvor != f)))
        {
            var ime = Path.GetFileNameWithoutExtension(f);
            if (IzvorDana.VremeIzImena(ime) != null || _podesavanja.Emisije.ContainsKey(ime)) DodajEmisiju(f);
            else Hitovi.Add(new IzborFajla(f, ime));
        }

        // Exactly one file at the top of the folder is the hit of the day; with more, the people say which.
        bool bioTiho = _tiho;
        _tiho = true;
        IzabranHit = Hitovi.FirstOrDefault(h => h.Putanja == bioHit) ?? (Hitovi.Count == 1 ? Hitovi[0] : null);
        _tiho = bioTiho;
        HitNadjenSam = IzabranHit != null && bioHit == null && Hitovi.Count == 1;

        var datum = IzabranDan?.Datum.Date;
        foreach (var folder in Directory.EnumerateDirectories(FolderSema).OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase))
        {
            var ime = Path.GetFileName(folder);
            if (IzvorDana.DatumFoldera(ime) is { } saDatumom)
            {
                if (saDatumom.Datum == datum && Emisije.All(e => e.Izvor != folder)) DodajEmisiju(folder);
                continue;
            }
            if (Emisije.Any(e => e.Izvor == folder)) continue;
            if (IzvorDana.VremeIzImena(ime) != null)
            {
                DodajEmisiju(folder);
                continue;
            }
            Plejliste.Add(new PlejlistaRed(folder, Izmenjeno, DobiloVreme) { Ukljucena = bile.GetValueOrDefault(folder, true) });
        }
        OsveziOstaleFajlove();
        _ = OsveziTrajanja();
    }

    /// <summary>Files at the top besides the hit, each with a box for its time.</summary>
    private void OsveziOstaleFajlove()
    {
        OstaliFajlovi.Clear();
        foreach (var h in Hitovi.Where(h => h != IzabranHit && h.Putanja != null))
            OstaliFajlovi.Add(new PlejlistaRed(h.Putanja!, Izmenjeno, DobiloVreme));
    }

    /// <summary>The schedule reads like the day: earlier times above. A row without a time stays at the bottom.</summary>
    private void PoVremenu(EmisijaRed red)
    {
        var vreme = ProcitajVreme(red.Vreme) ?? TimeSpan.MaxValue;
        int i = Emisije.IndexOf(red);
        int gde = Emisije.Where(e => e != red).Count(e => (ProcitajVreme(e.Vreme) ?? TimeSpan.MaxValue) <= vreme);
        if (i >= 0 && gde != i) Emisije.Move(i, gde);
    }

    /// <summary>A time was typed next to a folder or a file: from now on it is a block or a show at that time.</summary>
    private void DobiloVreme(PlejlistaRed red)
    {
        if (_tiho || !MozeIzmena || ProcitajVreme(red.Vreme) is not { } vreme) return;
        DodajEmisiju(red.Folder);
        if (Emisije.LastOrDefault(e => e.Izvor == red.Folder) is { } e)
        {
            e.Vreme = vreme.ToString(@"hh\:mm");
            PoVremenu(e);
        }
        OstaliFajlovi.Remove(red);
        _ = OsveziTrajanja();
    }

    /// <summary>
    /// How long each folder and file runs: measured when the day was built, read from the tags until then.
    /// It runs in the background, the folders may be large.
    /// </summary>
    private async Task OsveziTrajanja()
    {
        var redovi = Plejliste.Select(p => (Red: (object)p, p.Folder, p.Pesama))
            .Concat(OstaliFajlovi.Select(p => (Red: (object)p, p.Folder, p.Pesama)))
            .Concat(Emisije.Select(e => (Red: (object)e, Folder: e.Izvor, Pesama: e.JeFolder ? -1 : 1)))
            .ToList();
        var trajanja = await Task.Run(() => redovi.Select(r =>
        {
            try
            {
                var fajlovi = Directory.Exists(r.Folder) ? IzvorDana.Fajlovi(r.Folder) : File.Exists(r.Folder) ? [r.Folder] : [];
                double ukupno = fajlovi.Sum(f => _procitano.TryGetValue(f, out var bio) ? bio.Fajl.Trajanje : IzvorDana.Procitaj(f).Trajanje);
                return (fajlovi.Count, ukupno);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return (0, 0.0);
            }
        }).ToList());

        for (int i = 0; i < redovi.Count; i++)
        {
            var (broj, ukupno) = trajanja[i];
            string opis = broj == 1 && !Directory.Exists(redovi[i].Folder) ? Vreme.Dugo(ukupno)
                : broj == 0 ? "prazan" : $"{Rec.Pesama(broj)} · {Vreme.Dugo(ukupno)}";
            switch (redovi[i].Red)
            {
                case PlejlistaRed p: p.Opis = opis; break;
                case EmisijaRed e: e.Opis = opis; break;
            }
        }
    }

    public void OsveziJinglove()
    {
        // The drop-downs are bound to these lists: clearing them would clear what the people picked, so unless
        // the folder really changed everything stays as it is.
        var fajlovi = IzvorDana.FajloviJinglova(FolderJinglovi);
        if (fajlovi.SequenceEqual(Jinglovi.Select(j => j.Putanja), StringComparer.OrdinalIgnoreCase) && Jinglovi.Count > 0) return;

        _vracaPocetak = true;
        var jinglEmisije = Emisije.ToDictionary(e => e, e => e.Jingl?.Putanja);
        Jinglovi.Clear();
        JingloviEmisija.Clear();
        JingloviEmisija.Add(new IzborFajla(null, "(bez jingla)"));
        SviJinglovi.Clear();
        int novih = 0;
        foreach (var f in fajlovi)
        {
            var ime = Path.GetFileName(f);
            if (!_podesavanja.OznakeJinglova.TryGetValue(ime, out var vrsta))
            {
                vrsta = IzvorDana.PredlogZa(ime);
                _podesavanja.OznakeJinglova[ime] = vrsta;
                novih++;
            }
            var red = new JinglRed(f, vrsta, PromenjenaOznaka);
            Jinglovi.Add(red);
            SviJinglovi.Add(new IzborFajla(f, red.Naziv));
            if (vrsta == VrstaJingla.Emisija) JingloviEmisija.Add(new IzborFajla(f, red.Naziv));
        }

        // What the people picked for the opening, and nothing else: the program does not choose it for them.
        foreach (var (e, jingl) in jinglEmisije)
            e.Jingl = JingloviEmisija.FirstOrDefault(j => j.Putanja == jingl) ?? JingloviEmisija[0];
        Spica = SviJinglovi.FirstOrDefault(j => j.Putanja == _podesavanja.PocetakSpica);
        PocetniJinglHita = SviJinglovi.FirstOrDefault(j => j.Putanja == _podesavanja.PocetakJinglHita);
        PocetniJingl = SviJinglovi.FirstOrDefault(j => j.Putanja == _podesavanja.PocetakJingl);
        _vracaPocetak = false;

        if (novih > 0) _podesavanja.Sacuvaj();
        var poVrsti = Jinglovi.GroupBy(j => j.Vrsta).ToDictionary(g => g.Key, g => g.Count());
        OpisJinglova = string.Join(" · ", VrsteJinglova.Where(poVrsti.ContainsKey).Select(v => $"{NazivVrste(v)}: {poVrsti[v]}"));
        if (novih > 0 && novih < Jinglovi.Count) Poruka?.Invoke(Rec.T("Novih jinglova: {0}. Proveri njihove oznake u delu Jinglovi.", novih));
    }

    partial void OnSpicaChanged(IzborFajla? value) => ZapamtiPocetak();

    partial void OnPocetniJinglHitaChanged(IzborFajla? value) => ZapamtiPocetak();

    partial void OnPocetniJinglChanged(IzborFajla? value) => ZapamtiPocetak();

    private void ZapamtiPocetak()
    {
        if (_vracaPocetak) return;
        Izmenjeno();
        _podesavanja.PocetakSpica = Spica?.Putanja;
        _podesavanja.PocetakJinglHita = PocetniJinglHita?.Putanja;
        _podesavanja.PocetakJingl = PocetniJingl?.Putanja;
        _podesavanja.Sacuvaj();
    }

    private void PromenjenaOznaka(JinglRed red)
    {
        _podesavanja.OznakeJinglova[Path.GetFileName(red.Putanja)] = red.Vrsta;
        _podesavanja.Sacuvaj();
        var poVrsti = Jinglovi.GroupBy(j => j.Vrsta).ToDictionary(g => g.Key, g => g.Count());
        OpisJinglova = string.Join(" · ", VrsteJinglova.Where(poVrsti.ContainsKey).Select(v => $"{NazivVrste(v)}: {poVrsti[v]}"));
    }

    partial void OnIzabranDanChanged(IzborDana? value)
    {
        OnPropertyChanged(nameof(MozeNazad));
        OnPropertyChanged(nameof(MozeNapred));
        if (value == null) return;
        bool bioTiho = _tiho;
        _tiho = true;
        foreach (var e in Emisije.Where(e => IzvorDana.DatumFoldera(Path.GetFileName(e.Izvor)) is { } d && d.Datum != value.Datum).ToList())
            Emisije.Remove(e);
        OsveziSemu();
        _tiho = bioTiho;

        if (!Aktivan && !Ceka && SlozenDan.Ucitaj(Putanje.Dani, value.Datum) is { } sacuvan) Prikazi(sacuvan);
        else if (!Aktivan && !Ceka)
        {
            Prikazi(null);
            if (!_tiho) Izmenjeno();
        }
    }

    partial void OnIzabranHitChanged(IzborFajla? value)
    {
        OsveziOstaleFajlove();
        if (!_tiho) HitNadjenSam = false;
        OpisHita = value?.Putanja is { } p && File.Exists(p) ? Path.GetFileName(Path.GetDirectoryName(p)) is { } f ? $"iz foldera {f}" : "" : "";
        Izmenjeno();
    }

    public void DodajEmisiju(string izvor)
    {
        var ime = Path.GetFileName(izvor);
        var naziv = IzvorDana.DatumFoldera(ime)?.Naziv ?? (File.Exists(izvor) ? Path.GetFileNameWithoutExtension(izvor) : ime);

        // "20.00 Koncertacija": the time in the name wins over anything remembered.
        var izImena = IzvorDana.VremeIzImena(naziv);
        if (izImena != null) naziv = izImena.Value.Naziv;
        var red = new EmisijaRed(naziv, izvor) { Jingl = JingloviEmisija.FirstOrDefault(), VremeIzImena = izImena != null };

        // A show comes back under the same name, usually at the same time and with the same jingle.
        if (_podesavanja.Emisije.TryGetValue(naziv, out var zapamceno))
        {
            red.Vreme = zapamceno.Vreme;
            red.Jingl = JingloviEmisija.FirstOrDefault(j => j.Putanja == zapamceno.Jingl) ?? red.Jingl;
        }
        if (izImena != null) red.Vreme = izImena.Value.Vreme.ToString(@"hh\:mm");
        red.Izmenjeno = Izmenjeno;

        Emisije.Add(red);
        PoVremenu(red);
        Izmenjeno();
        if (Plejliste.FirstOrDefault(p => p.Folder == izvor) is { } plejlista) Plejliste.Remove(plejlista);

        // A file at the top of the folder is either a show or the hit, so taking it as a show settles both.
        if (Hitovi.FirstOrDefault(h => h.Putanja == izvor) is { } fajl)
        {
            Hitovi.Remove(fajl);
            if (IzabranHit == fajl) IzabranHit = null;
            if (IzabranHit == null && Hitovi.Count == 1) IzabranHit = Hitovi[0];
        }
    }

    /// <summary>Back to an empty sheet for the chosen day. The opening stays, it hardly ever changes.</summary>
    public void Ocisti()
    {
        if (!MozeIzmena || IzabranDan is not { } dan) return;
        _odlozenoSlaganje.Stop();
        _tiho = true;
        try
        {
            SlozenDan.Obrisi(Putanje.Dani, dan.Datum);
            Emisije.Clear();
            IzabranHit = null;
            Prikazi(null);
            Plejliste.Clear();
            OsveziSemu();
        }
        finally
        {
            _tiho = false;
        }
        Tab = TabDana.Priprema;
    }

    public void UkloniEmisiju(EmisijaRed red)
    {
        Emisije.Remove(red);
        if (!red.JeFolder && _podesavanja.Emisije.Remove(red.Naziv)) _podesavanja.Sacuvaj();
        bool bioTiho = _tiho;
        _tiho = true;
        OsveziSemu();
        _tiho = bioTiho;
        Izmenjeno();
    }

    public void PomeriPlejlistu(PlejlistaRed red, int za)
    {
        int i = Plejliste.IndexOf(red);
        int gde = i + za;
        if (i >= 0 && gde >= 0 && gde < Plejliste.Count)
        {
            Plejliste.Move(i, gde);
            Izmenjeno();
        }
    }


    public void PostaviFolderSema(string folder)
    {
        FolderSema = folder;
        _podesavanja.FolderSema = folder;
        _podesavanja.Sacuvaj();
        Plejliste.Clear();
        OsveziSemu();
    }

    public void PostaviFolderJinglovi(string folder)
    {
        FolderJinglovi = folder;
        _podesavanja.FolderJinglovi = folder;
        _podesavanja.Sacuvaj();
        OsveziJinglove();
    }

    private FajlDana Fajl(string putanja)
    {
        long izmenjeno = File.GetLastWriteTimeUtc(putanja).Ticks;
        if (_procitano.TryGetValue(putanja, out var bio) && bio.Izmenjeno == izmenjeno) return bio.Fajl;
        var fajl = IzvorDana.Procitaj(putanja);
        double stvarno = _plejer.Zvuk.Izmeri(putanja);
        if (stvarno > 0) fajl = fajl with { Trajanje = stvarno };
        _procitano[putanja] = (izmenjeno, fajl);
        return fajl;
    }

    /// <summary>Jingles carry no useful tags; their file name says what they are.</summary>
    private static FajlDana Bez(FajlDana f)
    {
        var ime = Path.GetFileNameWithoutExtension(f.Putanja);
        return f with { Izvodjac = "", Naslov = ime.StartsWith("Jingle - ") ? ime["Jingle - ".Length..] : ime };
    }

    private FajlDana Jingl(string putanja) => Bez(Fajl(putanja));

    private List<FajlDana> Fajlovi(IEnumerable<string> putanje)
    {
        var spisak = putanje.ToList();
        var rezultat = new FajlDana[spisak.Count];
        Parallel.For(0, spisak.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i => rezultat[i] = Fajl(spisak[i]));
        return [.. rezultat];
    }

    private Task? _slaganje;

    /// <summary>Builds the day. Asked again while it builds, it builds once more at the end, and both callers wait for that.</summary>
    [RelayCommand]
    public Task Slozi()
    {
        if (IzabranDan == null || !MozeIzmena) return Task.CompletedTask;
        _odlozenoSlaganje.Stop();
        if (_slaganje is { IsCompleted: false })
        {
            _josJednom = true;
            return _slaganje;
        }
        return _slaganje = SloziDokTreba();
    }

    private async Task SloziDokTreba()
    {
        do
        {
            _josJednom = false;
            await SloziJednom();
        }
        while (_josJednom && MozeIzmena);
    }

    private async Task SloziJednom()
    {
        if (IzabranDan is not { } dan || !MozeIzmena) return;
        Radi = true;
        Stanje = Rec.T("Slažem dan...");
        try
        {
            if (double.TryParse(TolerancijaTekst, out double tolerancija) && tolerancija is >= 0 and <= 60)
                _podesavanja.Tolerancija = tolerancija;
            TolerancijaTekst = _podesavanja.Tolerancija.ToString("0");

            var neispravne = new List<string>();
            var emisije = Emisije.ToList();
            var plejliste = Plejliste.Where(p => p.Ukljucena).Select(p => p.Folder).ToList();
            var jinglovi = Jinglovi.ToList();
            var hit = IzabranHit?.Putanja;
            var (spica, jinglHita, jingl) = (Spica?.Putanja, PocetniJinglHita?.Putanja, PocetniJingl?.Putanja);

            var ulaz = await Task.Run(() =>
            {
                var izabrani = new List<EmisijaDana>();
                foreach (var e in emisije)
                {
                    if (ProcitajVreme(e.Vreme) is not { } vreme)
                    {
                        neispravne.Add($"Upiši vreme za emisiju „{e.Naziv}“ (na primer 20:00).");
                        continue;
                    }
                    var fajlovi = Directory.Exists(e.Izvor) ? Fajlovi(IzvorDana.Fajlovi(e.Izvor)) : File.Exists(e.Izvor) ? [Fajl(e.Izvor)] : [];
                    izabrani.Add(new EmisijaDana(e.Naziv, vreme, fajlovi, e.Jingl?.Putanja is { } j ? Jingl(j) : null, e.JeFolder));
                }

                List<FajlDana> Jingle(VrstaJingla v) => Fajlovi(jinglovi.Where(j => j.Vrsta == v).Select(j => j.Putanja)).Select(Bez).ToList();
                var pocetak = dan.Datum + PocetakPrograma;
                return new UlazDana
                {
                    Pocetak = pocetak,
                    Kraj = dan.Datum.AddDays(1),
                    TerminiHita = Enumerable.Range(0, (PoslednjiTermin - PrviTermin) / 2 + 1).Select(i => dan.Datum.AddHours(PrviTermin + 2 * i)).ToList(),
                    Spica = spica != null ? Jingl(spica) : null,
                    PocetniJinglHita = jinglHita != null ? Jingl(jinglHita) : null,
                    PocetniJingl = jingl != null ? Jingl(jingl) : null,
                    HitDana = hit != null ? Fajl(hit) : null,
                    JingloviHita = Jingle(VrstaJingla.HitDana),
                    Jinglovi = Jingle(VrstaJingla.Opsti),
                    Pesme = plejliste.SelectMany(f => Fajlovi(IzvorDana.Fajlovi(f))).ToList(),
                    Emisije = izabrani,
                    Tolerancija = _podesavanja.Tolerancija,
                };
            });

            foreach (var e in emisije.Where(e => ProcitajVreme(e.Vreme) != null))
                _podesavanja.Emisije[e.Naziv] = new ZapamcenaEmisija(e.Vreme, e.Jingl?.Putanja);
            _podesavanja.Sacuvaj();

            // The same day draws the same jingles, so changing one thing does not reshuffle the whole list.
            var slozen = SlaganjeDana.Slozi(ulaz, dan.Datum.Year * 1000 + dan.Datum.DayOfYear);
            slozen.Problemi.InsertRange(0, neispravne);
            if (IzabranDan?.Datum != dan.Datum || !MozeIzmena) return;
            slozen.Sacuvaj(Putanje.Dani);
            Prikazi(slozen);
            _ = OsveziTrajanja();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dnevnik.Greska("Slaganje dana", ex);
            Stanje = Rec.T("Dan nije složen: neki fajl nije dostupan.");
        }
        finally
        {
            Radi = false;
        }
    }

    /// <summary>Shows a day that is already built. Tests use it to play a short day of their own.</summary>
    public void Prikazi(SlozenDan? dan)
    {
        _dan = dan;
        Stavke.Clear();
        Problemi.Clear();
        _tekuca = -1;
        OsveziDugmad();
        if (dan == null)
        {
            Ostatak.Clear();
            KarticaZadato = null;
            OnPropertyChanged(nameof(PrikaziOstatak));
            OpisDana = "";
            Stanje = Rec.T("Dan nije složen.");
            Blokovi.Clear();
            OsveziSemafor();
            return;
        }

        foreach (var s in dan.Stavke) Stavke.Add(new DanRed(s));
        OsveziProbleme();
        OsveziRedove();
        OsveziOstatak();
        OsveziBlokove();
        OnPropertyChanged(nameof(PrikaziOstatak));
        Stanje = Problemi.Count == 0 ? "Dan je složen. Pogledaj listu i pokreni ga." : "Dan je složen, ali proveri upozorenja.";
    }

    private void OsveziRedove()
    {
        if (_dan == null) return;
        foreach (var r in Stavke) r.Osvezi(_dan.Tolerancija);

        var s = _dan.Stavke;
        double pesama = s.Count(x => x.Vrsta == VrstaStavke.Pesma);
        string kraj = s.Count > 0 ? s[^1].Pocetak.AddSeconds(s[^1].Fajl.Trajanje).ToString("HH:mm:ss") : "";
        var ostalo = _dan.Neiskorisceno > 0 ? Rec.T(" · ostalo neiskorišćeno: {0}", Rec.Pesama(_dan.Neiskorisceno)) : "";
        OpisDana = Rec.T("{0} · {1} · kraj u {2}{3}", Rec.Pesama((int)pesama), Rec.Jinglova(s.Count(x => x.Vrsta == VrstaStavke.Jingl)), kraj, ostalo);
    }

    /// <summary>What already played stays as it was, and so does the opening of the day.</summary>
    private bool Slobodan(int i) => _dan != null && i > 0 && i < Stavke.Count && (!Aktivan || i > _tekuca);

    public void Pomeri(DanRed red, int za) => Premesti(red, Stavke.IndexOf(red) + za);

    public void Premesti(DanRed red, int gde)
    {
        int i = Stavke.IndexOf(red);
        if (i == gde || !Slobodan(i) || !Slobodan(gde)) return;
        Stavke.Move(i, gde);
        var s = _dan!.Stavke[i];
        _dan.Stavke.RemoveAt(i);
        _dan.Stavke.Insert(gde, s);
        PosleIzmene();
    }

    public void Ukloni(DanRed red)
    {
        if (!Slobodan(Stavke.IndexOf(red))) return;
        _dan!.Stavke.Remove(red.Stavka);
        Stavke.Remove(red);
        PosleIzmene();
    }

    /// <summary>Removes several rows at once, as Ctrl+A and Delete in a classic player do. What already played
    /// and the song on air stay; the opening of the day stays too.</summary>
    public void UkloniVise(IEnumerable<DanRed> redovi)
    {
        if (_dan == null) return;
        bool bilo = false;
        foreach (var red in redovi.ToList())
        {
            if (!Slobodan(Stavke.IndexOf(red))) continue;
            _dan.Stavke.Remove(red.Stavka);
            Stavke.Remove(red);
            bilo = true;
        }
        if (bilo) PosleIzmene();
    }

    /// <summary>Classic "clear playlist": empties the list in U etru. While the day plays, what already played and
    /// the song on air stay, because the program must not go quiet; otherwise the whole day goes and the sheet
    /// stays empty.</summary>
    public void UkloniSve()
    {
        if (_dan == null) return;
        if (Aktivan)
        {
            UkloniVise(Stavke.ToList());
            return;
        }
        SlozenDan.Obrisi(Putanje.Dani, _dan.Pocetak.Date);
        Prikazi(null);
    }

    /// <summary>Puts something the people picked right after the given row.</summary>
    public void Ubaci(DanRed posle, FajlDana fajl, VrstaStavke vrsta)
    {
        if (_dan == null) return;
        int gde = Stavke.IndexOf(posle) + 1;
        if (gde <= 0) gde = Stavke.Count;
        if (Aktivan && gde <= _tekuca) gde = _tekuca + 1;

        var stavka = new StavkaDana { Vrsta = vrsta, Fajl = fajl };
        _dan.Stavke.Insert(gde, stavka);
        Stavke.Insert(gde, new DanRed(stavka));
        PosleIzmene();
    }

    public FajlDana ProcitajPesmu(string putanja) => Fajl(putanja);

    public FajlDana ProcitajJingl(string putanja) => Jingl(putanja);

    public void Preslusaj(DanRed red) => _plejer.Preslusaj(UPesmu(red.Stavka));

    private void PosleIzmene()
    {
        if (Aktivan && _tekuca >= 0)
        {
            _plejer.Red.OsveziEmisiju(_dan!.Stavke.Skip(_tekuca).Select(PesmaZa).ToList());
            _plejer.PripremiSledecu();
            TokDana.Preracunaj(_dan.Stavke, _tekuca, _pustenoU, _dan.Prelaz, _dan.Tolerancija);
        }
        else
        {
            TokDana.Preracunaj(_dan!.Stavke, 0, _dan.Pocetak, _dan.Prelaz, _dan.Tolerancija);
        }
        _dan.Sacuvaj(Putanje.Dani);
        OsveziRedove();
        OsveziProbleme();
        OsveziOstatak();
        OsveziBlokove();
    }

    /// <summary>The day by the hours: one line per block, with what is short and what did not fit.</summary>
    private void OsveziBlokove()
    {
        var otvoreni = Blokovi.Where(b => b.Otvoren).Select(b => b.Vreme + b.Naziv).ToHashSet();
        Blokovi.Clear();
        if (_dan == null || _dan.Stavke.Count == 0) return;

        var s = _dan.Stavke;
        var delovi = PodelaDana.Podeli(s);
        var poslednjiDeoBloka = delovi.Select((d, i) => (d, i)).Where(x => x.d.Vrsta == VrstaDela.Blok)
            .GroupBy(x => x.d.Naziv).ToDictionary(g => g.Key, g => g.Last().i);

        for (int k = 0; k < delovi.Count; k++)
        {
            var d = delovi[k];
            var deo = s.Skip(d.Od).Take(d.Do - d.Od).ToList();
            var kraj = deo[^1].Pocetak.AddSeconds(deo[^1].Fajl.Trajanje);
            var sledeci = k + 1 < delovi.Count ? s[delovi[k + 1].Od].Pocetak : _dan.Kraj;
            double rupa = (sledeci - kraj).TotalSeconds;

            int pesama = deo.Count(x => x.Vrsta == VrstaStavke.Pesma);
            int jinglova = deo.Count(x => x.Vrsta == VrstaStavke.Jingl);
            double trajanje = deo.Sum(x => x.Fajl.Trajanje);
            string opis = d.Vrsta switch
            {
                VrstaDela.Hit or VrstaDela.Pocetak when pesama == 0 => $"{Math.Max(1, (int)Math.Round(trajanje / 60))} min",
                VrstaDela.Emisija => Vreme.Kratko(trajanje),
                _ => (jinglova > 0 ? $"{Rec.Pesama(pesama)} · {Rec.Jinglova(jinglova)}" : Rec.Pesama(pesama))
                     + $" · {Vreme.Dugo(trajanje)}",
            };

            List<string> izostavljene = [];
            if (d.Vrsta == VrstaDela.Blok && poslednjiDeoBloka.GetValueOrDefault(d.Naziv) == k)
                izostavljene = _dan.Izostavljeno.GetValueOrDefault(d.Naziv) ?? [];

            var vreme = s[d.Od].Pocetak.ToString(d.Vrsta is VrstaDela.Pocetak or VrstaDela.Hit ? "HH:mm:ss" : "HH:mm");
            var naziv = d.Nastavak ? $"{d.Naziv}, nastavak" : d.Naziv;
            var blok = new BlokDana(vreme, naziv, d.Vrsta, opis)
            {
                Stavke = Stavke.Skip(d.Od).Take(d.Do - d.Od).ToList(),
                Upozorenje = rupa > _dan.Tolerancija ? $"fali {Vreme.Dugo(rupa)}" : null,
                Visak = izostavljene.Count > 0 ? $"{izostavljene.Count} ne staje" : null,
                Izostavljene = izostavljene,
            };
            blok.Otvoren = otvoreni.Contains(blok.Vreme + blok.Naziv);
            Blokovi.Add(blok);
        }

        var poslednja = s[^1];
        var krajDana = poslednja.Pocetak.AddSeconds(poslednja.Fajl.Trajanje);
        Blokovi.Add(new BlokDana(krajDana.ToString("HH:mm"), "Kraj programa, kreće Auto DJ", VrstaDela.Pesme, "") { JeKraj = true });
    }

    /// <summary>Green when there is nothing to fix, yellow with how many things there are.</summary>
    private void OsveziSemafor()
    {
        if (_dan == null)
        {
            Semafor = "prazno";
            NaslovSemafora = Radi ? Rec.T("Slažem dan...") : Rec.T("Dan još nije složen.");
            return;
        }
        int n = Problemi.Count;
        Semafor = n == 0 ? "ok" : "paznja";
        if (Problemi.Any(p => p.Contains("Napredno"))) NaprednoOtvoreno = true;
        NaslovSemafora = n switch
        {
            0 => "Sve je spremno",
            1 => "Jedna stvar treba da se sredi",
            < 5 => $"{n} stvari treba da se srede",
            _ => $"{n} stvari treba da se sredi",
        };
    }

    /// <summary>Holes are worked out from the list, so a shortage disappears the moment it is filled.</summary>
    private void OsveziProbleme()
    {
        if (_dan == null) return;
        var s = _dan.Stavke;
        var novi = _dan.Problemi.Where(p => !p.StartsWith("Fali pesama")).ToList();
        for (int i = 1; i < s.Count; i++)
        {
            double rupa = (s[i].Pocetak - s[i - 1].Pocetak).TotalSeconds - s[i - 1].Fajl.Trajanje;
            if (rupa > _dan.Tolerancija) novi.Add($"Fali pesama za {Vreme.Dugo(rupa)} do {s[i].Pocetak:HH:mm}.");
        }
        if (s.Count > 0)
        {
            double doKraja = (_dan.Kraj - s[^1].Pocetak).TotalSeconds - s[^1].Fajl.Trajanje;
            if (doKraja > _dan.Tolerancija) novi.Add($"Fali pesama za {Vreme.Dugo(doKraja)} do {_dan.Kraj:HH:mm}.");
        }
        _dan.Problemi = novi;
        Problemi.Clear();
        foreach (var p in novi) Problemi.Add(p);
        OsveziSemafor();
    }

    // ---- on air ----

    [RelayCommand]
    public async Task Pokreni()
    {
        if (_dan == null || _dan.Stavke.Count == 0 || Aktivan || Ceka) return;
        var sada = Sada();
        if (sada >= _dan.Kraj)
        {
            Stanje = Rec.T("Taj dan je prošao.");
            return;
        }

        _dan.Pokrenut = true;
        _dan.Sacuvaj(Putanje.Dani);
        _budan.Ukljuci();
        _sat.Start();

        OsveziOstatak();
        if (sada < _dan.Pocetak)
        {
            Ceka = true;
            Stanje = $"Čeka se {_dan.Pocetak:dd.MM. u HH:mm:ss}.";
            return;
        }

        // Started late, or back after a crash: whatever should be on air right now.
        var mesto = TokDana.ZaVreme(_dan.Stavke, sada);
        int od = mesto?.Indeks ?? _dan.Stavke.FindIndex(s => s.Pocetak > sada);
        if (od < 0)
        {
            Zavrsi("Za danas više nema šta da svira.");
            return;
        }
        await PustiOd(od, mesto?.Pomak ?? 0);
    }

    /// <summary>After a crash the file still says the day was running, and the clock says where it got to.</summary>
    public async Task NastaviAkoTreba()
    {
        var sada = Sada();
        if (SlozenDan.Ucitaj(Putanje.Dani, sada.Date) is not { Pokrenut: true } dan || sada >= dan.Kraj) return;
        Prikazi(dan);
        IzabranDan = Datumi.FirstOrDefault(d => d.Datum == dan.Pocetak.Date) ?? IzabranDan;
        Tab = TabDana.Dan;
        UpisiUDnevnik("NASTAVAK  program je ponovo pokrenut");
        Poruka?.Invoke("Dan je nastavljen od mesta na kome treba da bude po satu.");
        await Pokreni();
    }

    private async Task PustiOd(int od, double pomak)
    {
        Ceka = false;
        Aktivan = true;
        _uRedu.Clear();
        _pesmeDana.Clear();

        var pesme = _dan!.Stavke.Skip(od).Select(PesmaZa).ToList();

        await _plejer.PustiDan(pesme, $"Dan {_dan.Pocetak:dd.MM.}", pomak);
    }

    private Pesma PesmaZa(StavkaDana s)
    {
        if (_pesmeDana.TryGetValue(s, out var bila)) return bila;
        var p = UPesmu(s);
        _uRedu[p] = s;
        _pesmeDana[s] = p;
        return p;
    }

    private static long Oznaka(string putanja)
    {
        ulong h = 14695981039346656037;
        foreach (char c in putanja.ToLowerInvariant()) h = (h ^ c) * 1099511628211;
        return -(long)(h & 0x3FFFFFFFFFFFFFFF) - 1;
    }

    /// <summary>The player works with songs. Items of a day are not in the library, so their ids are negative.</summary>
    private static Pesma UPesmu(StavkaDana s) => Privremena(s.Fajl, NazivVrste(s.Vrsta));

    public static Pesma Privremena(FajlDana f, string naziv)
    {
        long id = Oznaka(f.Putanja);
        var album = new Album
        {
            Id = id,
            Naziv = naziv,
            Izvodjac = f.Izvodjac,
            Folder = Path.GetDirectoryName(f.Putanja) ?? "",
        };
        var pesma = new Pesma
        {
            Id = id,
            Putanja = f.Putanja,
            Naslov = f.Naslov,
            Izvodjac = f.Izvodjac,
            Trajanje = f.Trajanje,
            AlbumId = id,
            Album = album,
        };
        album.Pesme.Add(pesma);
        return pesma;
    }

    /// <summary>A new song just started. This is where the clock gets its say about what comes next.</summary>
    public void NaTekucu(Pesma? pesma)
    {
        if (!Aktivan || _dan == null || pesma == null || !_uRedu.TryGetValue(pesma, out var stavka)) return;

        int i = _dan.Stavke.IndexOf(stavka);
        if (i < 0) return;

        var sada = Sada();
        double vecSvira = _tekuca < 0 ? Math.Max(0, _plejer.Pozicija) : 0;
        _pustenoU = sada.AddSeconds(-vecSvira);
        for (int k = 0; k < Stavke.Count; k++)
        {
            Stavke[k].Tekuca = k == i;
            Stavke[k].Gotova = k < i;
        }
        _tekuca = i;

        TokDana.Preracunaj(_dan.Stavke, i, _pustenoU, _dan.Prelaz, _dan.Tolerancija);
        var zabelezeno = stavka.Zadato is { } z ? $"  [zadato {z:HH:mm:ss}, {(sada - z).TotalSeconds:+0;-0;0} s]" : "";
        UpisiUDnevnik($"{NazivVrste(stavka.Vrsta),-13} {Opis(stavka)}{zabelezeno}");

        var kraj = _pustenoU.AddSeconds(stavka.Fajl.Trajanje);
        var izbaci = TokDana.ZaPreskakanje(_dan.Stavke, i, kraj, _dan.Prelaz, _dan.Tolerancija);
        if (izbaci.Count > 0)
        {
            foreach (int k in izbaci)
            {
                var s = _dan.Stavke[k];
                UpisiUDnevnik($"PRESKOČENO  {Opis(s)} (zbog vremena)");
                _dan.Stavke.RemoveAt(k);
                Stavke.RemoveAt(k);
            }
            _plejer.UkloniIzReda(izbaci.Select(k => new SlediStavka(pesma, "", "", false, k - i - 1)).ToList());
            TokDana.Preracunaj(_dan.Stavke, i, _pustenoU, _dan.Prelaz);
        }

        OsveziRedove();
        OsveziOstatak();
        Stanje = $"Svira: {Opis(stavka)}";
        if (Stavke.Count > i) TekucaPromenjena?.Invoke(Stavke[i]);
    }

    private static string Opis(StavkaDana s) => s.Fajl.Izvodjac.Length > 0 ? $"{s.Fajl.Izvodjac} - {s.Fajl.Naslov}" : s.Fajl.Naslov;

    /// <summary>The song was moved by hand, so everything after it starts at a different time.</summary>
    public void NaPremotavanje()
    {
        if (!Aktivan || _dan == null || _tekuca < 0 || _tekuca >= _dan.Stavke.Count) return;

        _pustenoU = Sada().AddSeconds(-_plejer.Pozicija);
        TokDana.Preracunaj(_dan.Stavke, _tekuca, _pustenoU, _dan.Prelaz);
        OsveziRedove();
        OsveziOstatak();
    }

    /// <summary>Fixed times still ahead, for the right panel and the card up top.</summary>
    private void OsveziOstatak()
    {
        if (_dan == null) return;
        var sada = Sada();
        var ostalo = _dan.Stavke.Skip(Math.Max(0, _tekuca + 1)).Where(s => s.Zadato != null).ToList();

        var redovi = ostalo.Select(s => new OstatakRed(s.Zadato!.Value.ToString("HH:mm"), s.NazivZadatog ?? Opis(s))).ToList();
        if (!redovi.SequenceEqual(Ostatak))
        {
            Ostatak.Clear();
            foreach (var r in redovi) Ostatak.Add(r);
        }

        var s0 = _dan.Stavke.Count > 0 ? _dan.Stavke[^1] : null;
        KrajPrograma = s0?.Pocetak.AddSeconds(s0.Fajl.Trajanje).ToString("HH:mm:ss") ?? "00:00:00";

        if (ostalo.FirstOrDefault() is not { } sledeca)
        {
            KarticaZadato = null;
            return;
        }

        double sekundi = Math.Round((sledeca.Pocetak - sledeca.Zadato!.Value).TotalSeconds);
        var za = sledeca.Zadato.Value - sada;
        string doKad = za.TotalHours >= 1 ? $"{(int)za.TotalHours}:{za.Minutes:00}:{za.Seconds:00}" : $"{Math.Max(0, za.Minutes):00}:{Math.Max(0, za.Seconds):00}";
        KarticaZadato = sledeca.NazivZadatog ?? Opis(sledeca);
        KarticaZadatoVreme = $"{KarticaZadato} za {doKad}";
    }

    private void Otkucaj()
    {
        if (_dan == null) return;
        var sada = Sada();

        if (Ceka)
        {
            var ostalo = _dan.Pocetak - sada;
            OpisStanja = $"dan kreće {_dan.Pocetak:dd.MM. u HH:mm:ss} · još {(int)Math.Max(0, ostalo.TotalHours)}:{Math.Max(0, ostalo.Minutes):00}:{Math.Max(0, ostalo.Seconds):00}";
            _plejer.ZamenaSledi = _dan.Stavke.Count > 0 ? (Opis(_dan.Stavke[0]), $"Prvo ide u {_dan.Pocetak:HH:mm:ss}") : null;
        }
        else if (Aktivan)
        {
            _plejer.ZamenaSledi = null;
            OpisStanja = $"od {_dan.Pocetak:HH:mm:ss} · kraj u {KrajPrograma}";
            OsveziOstatak();
        }

        if (Ceka && sada >= _dan.Pocetak)
        {
            _ = PustiOd(0, 0);
            return;
        }
        if (!Aktivan || !_plejer.Stoji || _plejer.UcitavaSe) return;

        // The list ran out, or the player gave up after several bad files. The day has to go on regardless.
        int sledeca = _tekuca + 1;
        if (sledeca < _dan.Stavke.Count && sada < _dan.Kraj)
        {
            UpisiUDnevnik("NASTAVAK  plejer je stao, ide sledeća stavka");
            _tekuca = sledeca - 1;
            _ = PustiOd(sledeca, 0);
        }
        else
        {
            Zavrsi("Dan je završen.");
        }
    }

    private void Zavrsi(string poruka)
    {
        _sat.Stop();
        _budan.Iskljuci();
        Ceka = false;
        Aktivan = false;
        OpisStanja = "";
        KarticaZadato = null;
        Ostatak.Clear();
        OnPropertyChanged(nameof(PrikaziOstatak));
        _plejer.ZamenaSledi = null;
        _plejer.Otkljucaj();
        if (_dan != null)
        {
            _dan.Pokrenut = false;
            _dan.Sacuvaj(Putanje.Dani);
        }
        foreach (var r in Stavke) r.Tekuca = false;
        Stanje = poruka;
        UpisiUDnevnik($"KRAJ  {poruka}");
    }

    [RelayCommand]
    public void Zaustavi()
    {
        if (!Aktivan && !Ceka) return;
        bool bioAktivan = Aktivan;
        Zavrsi("Dan je zaustavljen.");
        if (bioAktivan) _plejer.IsprazniRed();
    }

    /// <summary>Something else wants the player. Replacing the queue would quietly take the day off the air.</summary>
    public bool SmePrekid()
    {
        if (!Aktivan) return true;
        if (PitajPrekid?.Invoke() == false) return false;
        Zavrsi("Dan je zaustavljen.");
        return true;
    }

    private void UpisiUDnevnik(string tekst)
    {
        try
        {
            Directory.CreateDirectory(Putanje.Emitovano);
            var sada = Sada();
            File.AppendAllText(Path.Combine(Putanje.Emitovano, $"{sada:yyyy-MM-dd}.txt"), $"{sada:HH:mm:ss}  {tekst}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dnevnik.Greska("Dnevnik emitovanog", ex);
        }
    }
}
