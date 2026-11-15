using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Bronxamp.Podaci;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bronxamp.Vm;

public abstract partial class Strana : ObservableObject
{
    [ObservableProperty] private string _naslov = "";
    [ObservableProperty] private string _podnaslov = "";

    /// <summary>Where the back arrow leads. Null hides the arrow.</summary>
    [ObservableProperty] private Strana? _nazad;
}

public sealed partial class AlbumKartica : ObservableObject
{
    private static readonly Dictionary<int, Brush> Prelivi = [];

    private readonly Omoti _omoti;
    private readonly Action<Action> _naUi;

    public Album Album { get; private set; }

    public string Naziv => Album.Naziv;
    public string Izvodjac => Album.Izvodjac;
    public string IzvodjacVelika => Album.Izvodjac.ToUpperInvariant();
    public Brush Preliv { get; }

    public AlbumKartica(Album album, Omoti omoti, Action<Action> naUi)
    {
        Album = album;
        _omoti = omoti;
        _naUi = naUi;
        Preliv = PrelivZa(album.Naziv + album.Izvodjac);
    }

    public void Osvezi(Album album)
    {
        Album = album;
        OnPropertyChanged(nameof(Naziv));
        OnPropertyChanged(nameof(Izvodjac));
        OnPropertyChanged(nameof(IzvodjacVelika));
    }

    /// <summary>Read by the tile when it appears. The image itself lives in the cover cache, not here.</summary>
    public ImageSource? Omot
    {
        get
        {
            var slika = _omoti.IzKesa(Album.Id);
            if (slika == null)
                _omoti.Zatrazi(Album, () => _naUi(() =>
                {
                    OnPropertyChanged(nameof(Omot));
                    OnPropertyChanged(nameof(OmotCetka));
                }));
            return slika;
        }
    }

    /// <summary>The cover as a brush, so the tile's rounded border can paint it without any clipping.</summary>
    public Brush? OmotCetka
    {
        get
        {
            if (Omot is not { } slika) return null;
            var cetka = new ImageBrush(slika) { Stretch = Stretch.UniformToFill };
            cetka.Freeze();
            return cetka;
        }
    }

    /// <summary>Stand-in for a missing cover: a gradient that is always the same for the same album.</summary>
    public static Brush PrelivZa(string tekst)
    {
        int nijansa = (int)((uint)StabilanHes(tekst) % 36) * 10;
        lock (Prelivi)
        {
            if (Prelivi.TryGetValue(nijansa, out var postojeci)) return postojeci;

            var preliv = new LinearGradientBrush(IzHsv(nijansa, 0.62, 0.72), IzHsv((nijansa + 30) % 360, 0.72, 0.28), new Point(0, 0), new Point(1, 1));
            preliv.Freeze();
            Prelivi[nijansa] = preliv;
            return preliv;
        }
    }

    private static int StabilanHes(string s)
    {
        int h = 17;
        foreach (char c in s) h = unchecked(h * 31 + c);
        return h;
    }

    private static Color IzHsv(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        var (r, g, b) = (h / 60) switch
        {
            < 1 => (c, x, 0.0),
            < 2 => (x, c, 0.0),
            < 3 => (0.0, c, x),
            < 4 => (0.0, x, c),
            < 5 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}

/// <summary>One row of the cover wall: a section title, or up to one screen-width of tiles.</summary>
public sealed class ZidRed
{
    public string? Zaglavlje { get; init; }
    public IReadOnlyList<AlbumKartica> Kartice { get; init; } = [];
    public int Kolona { get; init; }
    public double Velicina { get; init; }

    public Visibility ZaglavljeVidljivo => Zaglavlje == null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility KarticeVidljive => Zaglavlje != null ? Visibility.Collapsed : Visibility.Visible;

    public bool Isti(ZidRed drugi) =>
        Zaglavlje == drugi.Zaglavlje && Kolona == drugi.Kolona && Math.Abs(Velicina - drugi.Velicina) < 0.5 &&
        Kartice.Count == drugi.Kartice.Count && Kartice.SequenceEqual(drugi.Kartice);
}

public sealed record GrupaAlbuma(string? Zaglavlje, IReadOnlyList<Album> Albumi);

/// <summary>
/// The cover wall. WPF has no virtualizing wrap panel, so tiles are cut into rows here and the rows go in a
/// virtualized list. This is what keeps the wall smooth with thousands of albums.
/// </summary>
public sealed partial class ZidStrana : Strana
{
    private const double NajmanjaPlocica = 170;
    private const double Razmak = 18;

    private readonly Func<Album, AlbumKartica> _kartica;
    private IReadOnlyList<GrupaAlbuma> _grupe = [];
    private double _sirina = 1000;

    public ObservableCollection<ZidRed> Redovi { get; } = [];

    [ObservableProperty] private string? _prazno;

    public ZidStrana(Func<Album, AlbumKartica> kartica) => _kartica = kartica;

    /// <summary>The first album on the wall, in the order shown.</summary>
    public Album? PrviAlbum => _grupe.SelectMany(g => g.Albumi).FirstOrDefault();

    public void Postavi(IReadOnlyList<GrupaAlbuma> grupe)
    {
        _grupe = grupe;
        Napravi();
    }

    public void PromeniSirinu(double sirina)
    {
        if (sirina < 200 || Math.Abs(sirina - _sirina) < 1) return;
        _sirina = sirina;
        Napravi();
    }

    private void Napravi()
    {
        int kolona = Math.Max(2, (int)((_sirina + Razmak) / (NajmanjaPlocica + Razmak)));
        double velicina = Math.Floor((_sirina - (kolona - 1) * Razmak) / kolona);

        var novi = new List<ZidRed>();
        foreach (var grupa in _grupe)
        {
            if (grupa.Zaglavlje != null) novi.Add(new ZidRed { Zaglavlje = grupa.Zaglavlje });
            for (int i = 0; i < grupa.Albumi.Count; i += kolona)
                novi.Add(new ZidRed
                {
                    Kartice = grupa.Albumi.Skip(i).Take(kolona).Select(_kartica).ToList(),
                    Kolona = kolona,
                    Velicina = velicina,
                });
        }

        // Row by row, so a library refresh during a scan does not throw the view back to the top.
        for (int i = 0; i < novi.Count; i++)
        {
            if (i >= Redovi.Count) Redovi.Add(novi[i]);
            else if (!Redovi[i].Isti(novi[i])) Redovi[i] = novi[i];
        }
        while (Redovi.Count > novi.Count) Redovi.RemoveAt(Redovi.Count - 1);
    }
}

public sealed partial class PesmaRed(Pesma pesma, string rb = "") : ObservableObject
{
    public Pesma Pesma { get; } = pesma;
    public string Rb { get; set; } = rb;

    public string Naslov => Pesma.Naslov;
    public string Izvodjac => Pesma.Izvodjac;
    public string Album => Pesma.Album.Naziv;
    public string Godina => Pesma.Godina > 0 ? Pesma.Godina.ToString() : "";
    public string Trajanje => Pesma.TrajanjeTekst;

    public string Zanr => Pesma.Zanr;
    public string Broj => Pesma.Broj > 0 ? Pesma.Broj.ToString() : "";
    public string Pustano => Pesma.Pustano > 0 ? Pesma.Pustano.ToString() : "";
    public string Poslednji => Pesma.PoslednjePustanje?.ToString("dd.MM.yyyy.") ?? "";
    public string Dodato => Pesma.Album.Dodato.ToString("dd.MM.yyyy.");
    public string OmiljenoZnak => Pesma.Omiljeno ? "★" : "";
    public string Format => Path.GetExtension(Pesma.Putanja).TrimStart('.').ToUpperInvariant();
    public string Bitrate => Pesma.Bitrate > 0 ? $"{Pesma.Bitrate} kbps" : "";
    public string Frekvencija => Pesma.Frekvencija > 0 ? $"{Pesma.Frekvencija / 1000.0:0.#} kHz" : "";
    public string Velicina => $"{Pesma.Velicina / 1048576.0:0.0} MB";
    public string Fajl => Path.GetFileName(Pesma.Putanja);
    public string Folder => Path.GetDirectoryName(Pesma.Putanja) ?? "";

    [ObservableProperty] private bool _tekuca;
    [ObservableProperty] private string? _upozorenje;
    [ObservableProperty] private string? _dodatak;

    public bool Omiljeno
    {
        get => Pesma.Omiljeno;
        set
        {
            Pesma.Omiljeno = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OmiljenoZnak));
        }
    }
}

public partial class PesmeStrana : Strana
{
    [ObservableProperty] private IReadOnlyList<PesmaRed> _stavke = [];
    [ObservableProperty] private Album? _album;
    [ObservableProperty] private AlbumKartica? _kartica;
    [ObservableProperty] private string? _prazno;

    public bool PrikaziDodatak { get; init; }

    /// <summary>Removing means leaving the library, which files that are only being browsed are not in.</summary>
    public bool MozeUklanjanje { get; init; } = true;

    /// <summary>Name of the queue when a song from this page is played.</summary>
    public string NazivReda { get; init; } = "Lista";

    public void OznaciTekucu(Pesma? pesma)
    {
        // By id: the queue may hold objects from before the library was reloaded.
        foreach (var s in Stavke)
            s.Tekuca = s.Pesma.Id == pesma?.Id;
    }
}

public sealed partial class StatistikaStrana : PesmeStrana
{
    private readonly Action<StatistikaStrana> _ucitaj;

    [ObservableProperty] private bool _mesec;

    public StatistikaStrana(Action<StatistikaStrana> ucitaj) => _ucitaj = ucitaj;

    partial void OnMesecChanged(bool value) => _ucitaj(this);
}

/// <summary>The panel under the list: the song picked there, or the one playing when nothing is picked.</summary>
public sealed partial class IzabranaPesma(Omoti omoti, System.Windows.Threading.Dispatcher ui) : ObservableObject
{
    private Pesma? _pustena;
    private Pesma? _izabrana;

    [ObservableProperty] private string _naslov = "";
    [ObservableProperty] private string _putanja = "";
    [ObservableProperty] private string _kvalitet = "";
    [ObservableProperty] private string _pustano = "";
    [ObservableProperty] private ImageSource? _omot;
    [ObservableProperty] private Brush? _preliv;

    public void PustenaJe(Pesma? pesma)
    {
        _pustena = pesma;
        if (_izabrana == null) Prikazi(pesma);
    }

    /// <summary>The song picked with a click in a list, if any.</summary>
    public Pesma? Izabrana => _izabrana;

    public void Izaberi(Pesma? pesma)
    {
        _izabrana = pesma;
        Prikazi(pesma ?? _pustena);
    }

    private void Prikazi(Pesma? p)
    {
        if (p == null)
        {
            Naslov = Putanja = Kvalitet = Pustano = "";
            Omot = null;
            Preliv = null;
            return;
        }

        Naslov = p.Izvodjac.Length > 0 ? $"{p.Izvodjac} - {p.Naslov}" : p.Naslov;
        Putanja = p.Putanja;
        Pustano = p.Id > 0 ? $"Puštano: {p.Pustano}" : "";
        Kvalitet = "";

        var album = p.Album;
        Preliv = AlbumKartica.PrelivZa(album.Naziv + album.Izvodjac);
        Omot = omoti.IzKesa(album.Id);
        if (Omot == null)
            omoti.Zatrazi(album, () => ui.BeginInvoke(() =>
            {
                if (Putanja == p.Putanja) Omot = omoti.IzKesa(album.Id);
            }));

        var putanja = p.Putanja;
        _ = Task.Run(() =>
        {
            var opis = IzvorDana.Kvalitet(putanja);
            ui.BeginInvoke(() =>
            {
                if (Putanja == putanja) Kvalitet = opis;
            });
        });
    }
}

public sealed partial class FolderCvor(string putanja) : ObservableObject
{
    public string Putanja { get; } = putanja;
    public string Naziv { get; } = Path.GetFileName(putanja.TrimEnd('\\')) is { Length: > 0 } ime ? ime : putanja;

    private List<FolderCvor>? _deca;

    public IReadOnlyList<FolderCvor> Deca
    {
        get
        {
            if (_deca != null) return _deca;
            try
            {
                _deca = Directory.EnumerateDirectories(Putanja).OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase).Select(f => new FolderCvor(f)).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _deca = [];
            }
            return _deca;
        }
    }
}

/// <summary>The music as it lies on the disk: a folder tree, and the songs of the folder picked.</summary>
public sealed partial class FolderiStrana : PesmeStrana
{
    private readonly GlavniVm _vm;
    private CancellationTokenSource? _citanje;

    public IReadOnlyList<FolderCvor> Koreni { get; }

    public FolderiStrana(IEnumerable<string> koreni, GlavniVm vm)
    {
        _vm = vm;
        Koreni = koreni.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Select(k => new FolderCvor(k)).ToList();
        Prazno = "Izaberi folder levo.";
    }

    public async Task Otvori(FolderCvor cvor)
    {
        _citanje?.Cancel();
        var token = (_citanje = new CancellationTokenSource()).Token;
        Podnaslov = "Čitam folder...";
        var redovi = await Task.Run(() =>
        {
            try
            {
                var fajlovi = IzvorDana.Fajlovi(cvor.Putanja);
                var procitani = new FajlDana[fajlovi.Count];
                Parallel.For(0, fajlovi.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i => procitani[i] = IzvorDana.Procitaj(fajlovi[i]));
                return procitani.Select((f, i) => new PesmaRed(DanStrana.Privremena(f, cvor.Naziv), (i + 1).ToString())).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Dnevnik.Greska("Čitanje foldera", ex);
                return [];
            }
        });
        if (token.IsCancellationRequested) return;
        Stavke = redovi;
        Podnaslov = Vreme.BrojPesama(redovi.Count);
        Prazno = redovi.Count == 0 ? "U ovom folderu nema muzike." : null;
        OznaciTekucu(_vm.Plejer.Tekuca);
    }
}

public sealed record DanEmitovanja(string Fajl, string Naziv)
{
    public override string ToString() => Naziv;
}

/// <summary>What really went on air, one file per day.</summary>
public sealed partial class EmitovanoStrana : Strana
{
    public IReadOnlyList<DanEmitovanja> Dani { get; }

    [ObservableProperty] private DanEmitovanja? _izabran;
    [ObservableProperty] private IReadOnlyList<string> _linije = [];

    public EmitovanoStrana()
    {
        Dani = Directory.Exists(Putanje.Emitovano)
            ? Directory.EnumerateFiles(Putanje.Emitovano, "*.txt").OrderDescending().Select(f => new DanEmitovanja(f, Path.GetFileNameWithoutExtension(f))).ToList()
            : [];
        Izabran = Dani.FirstOrDefault();
    }

    partial void OnIzabranChanged(DanEmitovanja? value)
    {
        try
        {
            Linije = value != null ? File.ReadAllLines(value.Fajl) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dnevnik.Greska("Čitanje dnevnika", ex);
            Linije = [];
        }
    }
}

public sealed record IzvodjacStavka(string Ime, string Opis, IReadOnlyList<Album> Albumi);

public sealed partial class IzvodjaciStrana : Strana
{
    [ObservableProperty] private IReadOnlyList<IzvodjacStavka> _stavke = [];
}

/// <summary>The user's shows. They used to be in the left menu.</summary>
public sealed partial class EmisijeStrana(ObservableCollection<NavStavka> emisije) : Strana
{
    public ObservableCollection<NavStavka> Emisije { get; } = emisije;

    [ObservableProperty] private string? _prazno;
}

public sealed record RedIstorije(string Vreme, string Pesma, string Izvodjac);

public sealed partial class EmisijaStrana : Strana
{
    public Emisija Emisija { get; }

    public ObservableCollection<PesmaRed> Stavke { get; } = [];

    [ObservableProperty] private IReadOnlyList<RedIstorije> _istorija = [];
    [ObservableProperty] private bool _prikaziIstoriju;

    public EmisijaStrana(Emisija emisija) => Emisija = emisija;

    public string Boja => Emisija.Boja;

    public void OsveziNaslov()
    {
        Naslov = Emisija.Ime;
        OnPropertyChanged(nameof(Boja));
    }

    public void OznaciTekucu(Pesma? pesma)
    {
        foreach (var s in Stavke) s.Tekuca = s.Pesma.Id == pesma?.Id;
    }
}
