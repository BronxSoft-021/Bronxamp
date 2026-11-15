namespace Bronxamp.Podaci;

/// <summary>The whole library in memory. Search over 60,000 songs takes milliseconds this way.</summary>
public sealed class Katalog
{
    private static readonly HashSet<string> OznakeRaznih = new(StringComparer.OrdinalIgnoreCase)
    {
        "Various Artists", "Various", "VA", "V.A.", "V/A", "Razni izvođači", "Razni izvodjaci", "Razni",
    };

    private readonly Baza _baza;

    private volatile IReadOnlyList<Album> _albumi = [];
    private volatile IReadOnlyDictionary<long, Pesma> _pesme = new Dictionary<long, Pesma>();
    private volatile IReadOnlyList<Pesma> _spisakPesama = [];

    public Katalog(Baza baza) => _baza = baza;

    public IReadOnlyList<Album> Albumi => _albumi;
    public IReadOnlyList<Pesma> Pesme => _spisakPesama;

    public Pesma? NadjiPesmu(long id) => _pesme.GetValueOrDefault(id);
    public Album? NadjiAlbum(long id) => _albumi.FirstOrDefault(a => a.Id == id);

    public event Action? Promenjen;

    public void Ucitaj()
    {
        var (albumi, pesme) = _baza.UcitajSve();

        // Album-artist is worked out here, once all of the album's songs are known.
        foreach (var a in albumi)
        {
            a.Pesme.Sort(PoRedu);
            if (OznakeRaznih.Contains(a.Izvodjac))
            {
                a.Izvodjac = Tagovi.RazniIzvodjaci;
            }
            else if (string.IsNullOrEmpty(a.Izvodjac))
            {
                var imena = a.Pesme.Select(p => p.Izvodjac).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();
                bool razni = imena.Count > 2 || (imena.Count == 2 && a.Pesme.Count > 2);
                a.Izvodjac = razni ? Tagovi.RazniIzvodjaci : imena[0];
                foreach (var p in a.Pesme) p.ZaPretragu = Pretraga.TekstZaPretragu(p, a);
            }
        }

        _albumi = albumi;
        _spisakPesama = pesme;
        _pesme = pesme.ToDictionary(p => p.Id);
        Promenjen?.Invoke();
    }

    private static int PoRedu(Pesma a, Pesma b)
    {
        int r = a.Disk.CompareTo(b.Disk);
        if (r == 0) r = a.Broj.CompareTo(b.Broj);
        return r != 0 ? r : string.Compare(a.Putanja, b.Putanja, StringComparison.OrdinalIgnoreCase);
    }

    public List<Album> Filtriraj(Pretraga upit)
    {
        if (upit.Prazna) return [.. _albumi];
        return _albumi.Where(a => a.Pesme.Exists(upit.Odgovara)).ToList();
    }

    public List<Pesma> FiltrirajPesme(Pretraga upit) =>
        upit.Prazna ? [.. _spisakPesama] : _spisakPesama.Where(upit.Odgovara).ToList();
}
