using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bronxamp.Podaci;

public enum VrstaStavke { Pesma, Jingl, JinglHit, HitDana, Spica, JinglEmisije, Emisija }

public enum VrstaJingla { Opsti, HitDana, Spica, Emisija, Retko, NeKoristi }

public sealed record FajlDana(string Putanja, string Izvodjac, string Naslov, double Trajanje);

public sealed class StavkaDana
{
    public VrstaStavke Vrsta { get; set; }
    public FajlDana Fajl { get; set; } = null!;
    public DateTime Pocetak { get; set; }

    /// <summary>Set on the first item of whatever has a fixed time: a hit of the day or a show.</summary>
    public DateTime? Zadato { get; set; }

    /// <summary>
    /// A show or block: its time is a guide, it may start up to half a minute off. Only the hit of the day is
    /// held closely to the clock.
    /// </summary>
    public bool Okvirno { get; set; }

    /// <summary>What the fixed time is called on screen: the hit of the day, or the name of the show or block.</summary>
    public string? NazivZadatog { get; set; }

    public bool Punjenje => Vrsta is VrstaStavke.Pesma or VrstaStavke.Jingl;
}

/// <param name="Blok">
/// Songs with a start time. Jingles go between them and the hit of the day breaks in, as anywhere else in the
/// day. Without it the show is one whole that nothing touches.
/// </param>
public sealed record EmisijaDana(string Naziv, TimeSpan Vreme, IReadOnlyList<FajlDana> Fajlovi, FajlDana? Jingl, bool Blok = false);

public sealed class UlazDana
{
    public DateTime Pocetak { get; init; }
    public DateTime Kraj { get; init; }

    /// <summary>When the hit of the day repeats. The opening block has its own place at <see cref="Pocetak"/>.</summary>
    public IReadOnlyList<DateTime> TerminiHita { get; init; } = [];

    /// <summary>The block starts this much before the slot, so the hit itself lands about on the hour.</summary>
    public double PreHita { get; init; } = 15;

    /// <summary>The opening is the people's choice, never drawn at random.</summary>
    public FajlDana? Spica { get; init; }
    public FajlDana? PocetniJinglHita { get; init; }
    public FajlDana? PocetniJingl { get; init; }

    public FajlDana? HitDana { get; init; }
    public IReadOnlyList<FajlDana> JingloviHita { get; init; } = [];
    public IReadOnlyList<FajlDana> Jinglovi { get; init; } = [];
    public IReadOnlyList<FajlDana> Pesme { get; init; } = [];
    public IReadOnlyList<EmisijaDana> Emisije { get; init; } = [];
    public double Prelaz { get; init; }
    public double Tolerancija { get; init; } = 5;
}

public sealed class SlozenDan
{
    public DateTime Pocetak { get; set; }
    public DateTime Kraj { get; set; }
    public double Prelaz { get; set; }
    public double Tolerancija { get; set; } = 5;
    public List<StavkaDana> Stavke { get; set; } = [];
    public List<string> Problemi { get; set; } = [];

    /// <summary>Worth knowing, but nothing to fix: songs left over from a full block.</summary>
    public List<string> Napomene { get; set; } = [];

    /// <summary>Titles that did not fit, by the name of their block.</summary>
    public Dictionary<string, List<string>> Izostavljeno { get; set; } = [];

    public int Neiskorisceno { get; set; }

    /// <summary>The day was started and not stopped: after a crash the program carries on from the clock.</summary>
    public bool Pokrenut { get; set; }

    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    public static string Putanja(string folder, DateTime datum) => Path.Combine(folder, $"{datum:yyyy-MM-dd}.json");

    public void Sacuvaj(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var fajl = Putanja(folder, Pocetak);
            var privremeni = fajl + ".novo";
            File.WriteAllText(privremeni, JsonSerializer.Serialize(this, Format));
            File.Move(privremeni, fajl, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dnevnik.Greska("Čuvanje dana", ex);
        }
    }

    public static void Obrisi(string folder, DateTime datum)
    {
        try
        {
            File.Delete(Putanja(folder, datum));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dnevnik.Greska("Brisanje dana", ex);
        }
    }

    public static SlozenDan? Ucitaj(string folder, DateTime datum)
    {
        try
        {
            var fajl = Putanja(folder, datum);
            return File.Exists(fajl) ? JsonSerializer.Deserialize<SlozenDan>(File.ReadAllText(fajl)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Dnevnik.Greska("Čitanje dana", ex);
            return null;
        }
    }
}

/// <summary>Builds a day: the people give songs, the hit and the shows, this places everything around the clock.</summary>
public sealed class SlaganjeDana
{
    private const double PragUklapanja = 720;
    private const int KandidataZaUklapanje = 48;
    private const int NajviseUKombinaciji = 4;
    private const int PesamaNaJingl = 3;

    /// <summary>How long the last songs of the day may run past midnight.</summary>
    private const double MinutaPosleKraja = 15;

    /// <summary>A show starting this much off its guide time is worth a word to the people.</summary>
    private const double MinutaKasnjenjaEmisije = 5;

    /// <summary>Seconds of accuracy a shorter gap has to win, so three songs stay the rule.</summary>
    private const double KaznaZaKraciRazmak = 20;

    private readonly UlazDana _u;
    private readonly Random _slucajno;
    private readonly List<StavkaDana> _stavke = [];
    private readonly List<string> _problemi = [];
    private readonly List<string> _napomene = [];
    private readonly Dictionary<string, List<string>> _izostavljeno = [];
    private readonly List<FajlDana> _pesme;
    private readonly List<FajlDana> _jinglovi;
    private readonly List<FajlDana> _jingloviHita;
    private readonly List<EmisijaDana> _blokovi;
    private EmisijaDana? _tekuciBlok;
    private DateTime? _zadatoSledece;
    private string? _nazivSledeceg;
    private DateTime _kraj;
    private int _odJingla;
    private bool _prijavljenManjak;

    private SlaganjeDana(UlazDana u, int seme)
    {
        _u = u;
        _slucajno = new Random(seme);
        _pesme = [.. u.Pesme];
        _kraj = u.Pocetak;
        _blokovi = u.Emisije.Where(e => e.Blok).OrderBy(e => e.Vreme).ToList();

        // A jingle plays once a day, so what the opening took is out for the rest of it.
        var uzeti = new[] { u.Spica, u.PocetniJinglHita, u.PocetniJingl }.Where(f => f != null).Select(f => f!.Putanja).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _jinglovi = u.Jinglovi.Where(j => !uzeti.Contains(j.Putanja)).ToList();
        _jingloviHita = u.JingloviHita.Where(j => !uzeti.Contains(j.Putanja)).ToList();
    }

    public static SlozenDan Slozi(UlazDana ulaz, int? seme = null)
    {
        var s = new SlaganjeDana(ulaz, seme ?? Environment.TickCount);
        s.Radi();
        return new SlozenDan
        {
            Pocetak = ulaz.Pocetak,
            Kraj = ulaz.Kraj,
            Prelaz = ulaz.Prelaz,
            Tolerancija = ulaz.Tolerancija,
            Stavke = s._stavke,
            Problemi = s._problemi,
            Napomene = s._napomene,
            Izostavljeno = s._izostavljeno,
            Neiskorisceno = s._pesme.Count,
        };
    }

    private sealed record Dogadjaj(DateTime Vreme, EmisijaDana? Emisija);

    private void Radi()
    {
        if (_u.Spica == null) _problemi.Add("Izaberi uvodnu špicu (dole, u delu Napredno).");
        if (_u.PocetniJinglHita == null) _problemi.Add("Izaberi jingl za hit dana na početku dana (dole, u delu Napredno).");
        if (_u.PocetniJingl == null) _problemi.Add("Izaberi jingl posle hita na početku dana (dole, u delu Napredno).");
        if (_u.HitDana == null) _problemi.Add("Nije izabran hit dana.");
        if (_u.Jinglovi.Count == 0) _problemi.Add("Nema jinglova za ubacivanje.");

        if (_u.Spica != null) Dodaj(VrstaStavke.Spica, _u.Spica, _u.Pocetak);
        if (_u.HitDana != null)
        {
            if (_u.PocetniJinglHita != null) Dodaj(VrstaStavke.JinglHit, _u.PocetniJinglHita);
            Dodaj(VrstaStavke.HitDana, _u.HitDana);
            if (_u.PocetniJingl != null) Dodaj(VrstaStavke.Jingl, _u.PocetniJingl);
        }

        var dogadjaji = _u.TerminiHita.Select(t => new Dogadjaj(t.AddSeconds(-_u.PreHita), null))
            .Concat(_u.Emisije.Select(e => new Dogadjaj(_u.Pocetak.Date + e.Vreme, e)))
            .OrderBy(d => d.Vreme)
            .ToList();

        DateTime? hitPosleEmisije = null;
        foreach (var d in dogadjaji)
        {
            if (d.Emisija == null && _u.HitDana == null) continue;

            if (d.Emisija != null)
            {
                // The time of a show is a guide, kept to within half a minute. Right after a hit nothing is
                // squeezed in: a hit runs five minutes or seven, and the show simply follows it.
                if ((d.Vreme - _kraj).TotalSeconds > TokDana.PragRupeOkvirno) Popuni(d.Vreme, TokDana.OkvirnoSekundi);
                if (d.Vreme < _kraj.AddMinutes(-MinutaKasnjenjaEmisije))
                    _problemi.Add($"„{d.Emisija.Naziv}“ kreće tek u {_kraj:HH:mm}, a ne oko {d.Vreme:HH:mm}: preklapa se sa onim što ide pre.");
                if (d.Emisija.Blok) PocniBlok(d.Emisija, d.Vreme);
                else DodajEmisiju(d.Emisija, d.Vreme);
                continue;
            }

            if (d.Vreme < _kraj.AddSeconds(-_u.Tolerancija))
            {
                // The hit falls inside a show. It goes right after the show, and only once however long the show is.
                if (hitPosleEmisije == _kraj) continue;
                _problemi.Add($"Hit dana u {d.Vreme.AddSeconds(_u.PreHita):HH:mm} pada u emisiju, pa ide posle nje, u {_kraj:HH:mm}.");
                DodajHitBlok(null);
                hitPosleEmisije = _kraj;
                continue;
            }

            Popuni(d.Vreme, _u.Tolerancija);
            DodajHitBlok(d.Vreme);
        }

        ZavrsiDan();
        foreach (var e in _u.Emisije.Where(e => e.Fajlovi.Count == 0))
            _problemi.Add($"Emisija „{e.Naziv}“ nema nijedan fajl.");
    }

    private void Dodaj(VrstaStavke vrsta, FajlDana fajl, DateTime? zadato = null)
    {
        if (_stavke.Count > 0)
            _kraj = _kraj.AddSeconds(-TokDana.Preklop(_stavke[^1].Fajl.Trajanje, fajl.Trajanje, _u.Prelaz));
        _stavke.Add(new StavkaDana
        {
            Vrsta = vrsta,
            Fajl = fajl,
            Pocetak = _kraj,
            Zadato = zadato ?? _zadatoSledece,
            NazivZadatog = _nazivSledeceg,
            Okvirno = _zadatoSledece != null,
        });
        _zadatoSledece = null;
        _nazivSledeceg = null;
        _kraj = _kraj.AddSeconds(fajl.Trajanje);
    }

    private FajlDana? Uzmi(List<FajlDana> kesa)
    {
        if (kesa.Count == 0) return null;
        int i = _slucajno.Next(kesa.Count);
        var f = kesa[i];
        kesa.RemoveAt(i);
        return f;
    }

    private void ManjakJinglova()
    {
        if (_prijavljenManjak) return;
        _prijavljenManjak = true;
        _problemi.Add($"Ponestalo je jinglova posle {_kraj:HH:mm}: isti jingl ne sme dva puta u danu.");
    }

    private void DodajJingl(DateTime? zadato = null)
    {
        if (Uzmi(_jinglovi) is { } f) Dodaj(VrstaStavke.Jingl, f, zadato);
        else ManjakJinglova();
        _odJingla = 0;
    }

    private void DodajJinglHita()
    {
        if (Uzmi(_jingloviHita) is { } f) Dodaj(VrstaStavke.JinglHit, f);
        else _problemi.Add($"Nema dovoljno jinglova za hit dana, onaj u {_kraj:HH:mm} ide bez njega.");
    }

    private void DodajHitBlok(DateTime? zadato)
    {
        int pre = _stavke.Count;
        DodajJingl();
        DodajJinglHita();
        Dodaj(VrstaStavke.HitDana, _u.HitDana!);
        DodajJingl();
        _stavke[pre].Zadato = zadato;
        _stavke[pre].NazivZadatog = "Hit dana";
    }

    private void DodajEmisiju(EmisijaDana e, DateTime vreme)
    {
        int pre = _stavke.Count;
        if (e.Jingl != null) Dodaj(VrstaStavke.JinglEmisije, e.Jingl);
        foreach (var f in e.Fajlovi) Dodaj(VrstaStavke.Emisija, f);
        if (_stavke.Count > pre)
        {
            _stavke[pre].Zadato = vreme;
            _stavke[pre].NazivZadatog = e.Naziv;
            _stavke[pre].Okvirno = true;
        }
        _odJingla = 0;
    }

    /// <summary>A block starts: its songs go first. If it was already drawn on to fill a gap, it just carries on.</summary>
    private void PocniBlok(EmisijaDana e, DateTime vreme)
    {
        if (_tekuciBlok != e)
        {
            IzostaviOstatak(vreme);
            _pesme.InsertRange(0, e.Fajlovi);
            _blokovi.Remove(e);
            _tekuciBlok = e;
        }

        // Songs run on from before, so the count of three runs on too, unless a jingle opens the block.
        _zadatoSledece = vreme;
        _nazivSledeceg = e.Naziv;
        if (e.Jingl != null)
        {
            Dodaj(VrstaStavke.JinglEmisije, e.Jingl);
            _odJingla = 0;
        }
        else if (_odJingla >= PesamaNaJingl)
        {
            DodajJingl();
        }
    }

    private void DodajPesmu()
    {
        var p = _pesme[0];
        _pesme.RemoveAt(0);
        Dodaj(VrstaStavke.Pesma, p);
        if (++_odJingla >= PesamaNaJingl) DodajJingl();
    }

    /// <summary>The day ends about midnight. Songs left of the last block may still go, for a little while.</summary>
    private void ZavrsiDan()
    {
        var granica = _u.Kraj.AddMinutes(MinutaPosleKraja);
        while (_pesme.Count > 0 && _kraj < granica) DodajPesmu();
        IzostaviOstatak(_kraj);

        double fali = (_u.Kraj - _kraj).TotalSeconds;
        if (fali > _u.Tolerancija)
            _problemi.Add($"Fali pesama za {Vreme.Dugo(fali)} do {_u.Kraj:HH:mm}.");
    }

    /// <summary>What did not fit before the next block drops out, and the people get to see what it was.</summary>
    private void IzostaviOstatak(DateTime doKad)
    {
        if (_tekuciBlok is not { } blok) return;
        var ostale = _pesme.Where(blok.Fajlovi.Contains).ToList();
        if (ostale.Count == 0) return;

        foreach (var p in ostale) _pesme.Remove(p);
        var imena = string.Join(", ", ostale.Take(5).Select(p => p.Naslov)) + (ostale.Count > 5 ? "..." : "");
        // A full block is not a fault: the day is full. The people see what stayed out, and nothing turns yellow.
        _napomene.Add($"Blok „{blok.Naziv}“ ne staje do {doKad:HH:mm}, izostavljeno je {Vreme.BrojPesama(ostale.Count)}: {imena}");
        if (!_izostavljeno.TryGetValue(blok.Naziv, out var spisak)) _izostavljeno[blok.Naziv] = spisak = [];
        spisak.AddRange(ostale.Select(p => p.Izvodjac.Length > 0 ? $"{p.Izvodjac} - {p.Naslov}" : p.Naslov));
    }

    /// <summary>Songs and jingles up to a fixed time. The last stretch is fitted, so that time is met closely.</summary>
    private void Popuni(DateTime doKad, double tolerancija)
    {
        while (_pesme.Count > 0)
        {
            double ostalo = (doKad - _kraj).TotalSeconds;
            if (ostalo <= tolerancija) return;
            if (ostalo <= PragUklapanja)
            {
                Uklopi(doKad, tolerancija);
                if (_pesme.Count > 0) return;
                continue;
            }

            DodajPesmu();
        }

        Rupa(doKad, tolerancija);
    }

    /// <summary>
    /// Out of songs before a time: the people fill that themselves, the next block is not touched. The plan keeps
    /// the hole, so one shortage is one warning and everything after it stays on its time.
    /// </summary>
    private void Rupa(DateTime doKad, double prag)
    {
        double fali = (doKad - _kraj).TotalSeconds;
        if (fali <= prag) return;
        _problemi.Add($"Fali pesama za {Vreme.Dugo(fali)} do {doKad:HH:mm}.");
        _kraj = doKad;
    }

    /// <summary>How many jingles a run of songs needs. One due right before the fixed item is left out: that block brings its own.</summary>
    private int JinglovaZa(int pesama, int razmak) => (_odJingla + pesama - 1) / razmak;

    /// <summary>
    /// The last stretch before a fixed time. Songs may swap places here, but only within what is still waiting in
    /// this block, so nothing sticks out; a block never borrows from another. Jingles of the right length take up
    /// the rest: every three songs if that works, every two when it would not fit otherwise.
    /// </summary>
    private void Uklopi(DateTime doKad, double tolerancija)
    {
        double ostalo = (doKad - _kraj).TotalSeconds;
        var kandidati = _pesme.Take(KandidataZaUklapanje).ToList();
        var jinglovi = _jinglovi.Select(j => j.Trajanje - _u.Prelaz).OrderBy(x => x).ToArray();

        var najbolja = new List<int>();
        int razmak = PesamaNaJingl;
        double najmanje = double.MaxValue;
        var izbor = new List<int>();

        double Greska(double suma, int r)
        {
            int potrebno = izbor.Count == 0 ? 0 : Math.Min(JinglovaZa(izbor.Count, r), jinglovi.Length);
            double zaJinglove = ostalo - suma;

            // What this many jingles can cover, shortest to longest. The rhythm decides how many there are.
            double najmanjeJingla = jinglovi.Take(potrebno).Sum();
            double najviseJingla = jinglovi.TakeLast(potrebno).Sum();
            return zaJinglove < najmanjeJingla ? najmanjeJingla - zaJinglove : zaJinglove > najviseJingla ? zaJinglove - najviseJingla : 0;
        }

        void Probaj(int od, double suma)
        {
            // Every place a song jumps costs a little, so the people's order stays wherever it can.
            double skok = izbor.Select((k, i) => k - i).Sum() * 0.01;
            foreach (var (r, kazna) in new[] { (PesamaNaJingl, 0.0), (PesamaNaJingl - 1, KaznaZaKraciRazmak) })
            {
                double greska = Greska(suma, r) + skok + kazna;
                if (greska < najmanje)
                {
                    najmanje = greska;
                    najbolja = [.. izbor];
                    razmak = r;
                }
            }

            if (izbor.Count == NajviseUKombinaciji) return;
            for (int i = od; i < kandidati.Count; i++)
            {
                double trajanje = kandidati[i].Trajanje - _u.Prelaz;
                if (suma + trajanje > ostalo + tolerancija) continue;
                izbor.Add(i);
                Probaj(i + 1, suma + trajanje);
                izbor.RemoveAt(izbor.Count - 1);
            }
        }
        Probaj(0, 0);

        var izabrane = najbolja.Select(i => kandidati[i]).ToList();
        foreach (var p in izabrane) _pesme.Remove(p);

        int preostaloJinglova = JinglovaZa(izabrane.Count, razmak);
        for (int i = 0; i < izabrane.Count; i++)
        {
            Dodaj(VrstaStavke.Pesma, izabrane[i]);
            if (++_odJingla < razmak || i == izabrane.Count - 1) continue;

            // Each jingle takes its share of the time still left over the songs still to come.
            double zaJinglove = (doKad - _kraj).TotalSeconds - izabrane.Skip(i + 1).Sum(p => p.Trajanje - _u.Prelaz);
            DodajNajbliziJingl(zaJinglove / Math.Max(1, preostaloJinglova));
            preostaloJinglova--;
        }

    }

    private static double NajbliziJingl(double[] trajanja, double razlika)
    {
        if (trajanja.Length == 0) return 0;
        int i = Array.BinarySearch(trajanja, razlika);
        if (i >= 0) return trajanja[i];
        i = ~i;
        if (i == 0) return trajanja[0];
        if (i == trajanja.Length) return trajanja[^1];
        return razlika - trajanja[i - 1] <= trajanja[i] - razlika ? trajanja[i - 1] : trajanja[i];
    }

    private void DodajNajbliziJingl(double cilj)
    {
        _odJingla = 0;
        if (_jinglovi.Count == 0)
        {
            ManjakJinglova();
            return;
        }
        var f = _jinglovi.OrderBy(j => Math.Abs(j.Trajanje - _u.Prelaz - cilj)).First();
        _jinglovi.Remove(f);
        Dodaj(VrstaStavke.Jingl, f);
    }
}

/// <summary>What happens while a day plays. Pure logic, so a whole day can be run on a fake clock.</summary>
public static class TokDana
{
    /// <summary>How far from its time a block or show may start. The hit of the day is held closer.</summary>
    public const double OkvirnoSekundi = 30;

    /// <summary>Time this short before a block or show is not filled with anything, the block just starts.</summary>
    public const double PragRupeOkvirno = 180;

    /// <summary>The same overlap the sound engine uses; see <see cref="KrajEmisije"/>.</summary>
    public static double Preklop(double prethodna, double sledeca, double prelaz) => Math.Min(prelaz, Math.Min(prethodna, sledeca) / 2);

    /// <summary>Start times from one item on, given when that item really started.</summary>
    public static void Preracunaj(IList<StavkaDana> stavke, int od, DateTime pocetak, double prelaz, double tolerancija = 5)
    {
        var t = pocetak;
        for (int i = od; i < stavke.Count; i++)
        {
            if (i > od) t = t.AddSeconds(-Preklop(stavke[i - 1].Fajl.Trajanje, stavke[i].Fajl.Trajanje, prelaz));

            // Songs ran out before this time: the plan keeps the hole, so it can be seen and filled.
            if (i > od && stavke[i].Zadato is { } z && (z - t).TotalSeconds > (stavke[i].Okvirno ? PragRupeOkvirno : tolerancija)) t = z;
            stavke[i].Pocetak = t;
            t = t.AddSeconds(stavke[i].Fajl.Trajanje);
        }
    }

    /// <summary>The item that should be playing now, and how far into it. Null outside the day.</summary>
    public static (int Indeks, double Pomak)? ZaVreme(IReadOnlyList<StavkaDana> stavke, DateTime sada)
    {
        for (int i = stavke.Count - 1; i >= 0; i--)
        {
            if (stavke[i].Pocetak > sada) continue;
            double pomak = (sada - stavke[i].Pocetak).TotalSeconds;
            return pomak < stavke[i].Fajl.Trajanje ? (i, pomak) : null;
        }
        return null;
    }

    /// <summary>
    /// Which songs and jingles to drop after the current item, so the next fixed time is met as closely as
    /// possible; highest index first. Nothing is ever cut: the current item always plays to its end.
    /// </summary>
    public static List<int> ZaPreskakanje(IReadOnlyList<StavkaDana> stavke, int tekuca, DateTime krajTekuce, double prelaz, double tolerancija)
    {
        var izbaci = new List<int>();
        int cilj = -1;
        for (int i = tekuca + 1; i < stavke.Count; i++)
        {
            if (stavke[i].Zadato != null && !stavke[i].Okvirno)
            {
                cilj = i;
                break;
            }
            if (!stavke[i].Punjenje) return izbaci;
        }
        if (cilj <= tekuca + 1) return izbaci;

        var zadato = stavke[cilj].Zadato!.Value;
        int zadrzati = 0;
        double najmanje = Math.Abs((krajTekuce - zadato).TotalSeconds);
        var kraj = krajTekuce;
        var krajZadrzanih = krajTekuce;
        for (int i = tekuca + 1; i < cilj; i++)
        {
            kraj = kraj.AddSeconds(stavke[i].Fajl.Trajanje - Preklop(stavke[i - 1].Fajl.Trajanje, stavke[i].Fajl.Trajanje, prelaz));
            double greska = Math.Abs((kraj - zadato).TotalSeconds);
            if (greska < najmanje)
            {
                najmanje = greska;
                zadrzati = i - tekuca;
                krajZadrzanih = kraj;
            }
        }
        for (int i = cilj - 1; i > tekuca + zadrzati; i--) izbaci.Add(i);

        // Still late: a kept jingle of about that length can go, which a whole song is too coarse for.
        double kasni = (krajZadrzanih - zadato).TotalSeconds;
        if (kasni > tolerancija)
        {
            int jingl = Enumerable.Range(tekuca + 1, zadrzati)
                .Where(i => stavke[i].Vrsta == VrstaStavke.Jingl)
                .OrderBy(i => Math.Abs(stavke[i].Fajl.Trajanje - kasni))
                .DefaultIfEmpty(-1)
                .First();
            if (jingl > 0 && Math.Abs(stavke[jingl].Fajl.Trajanje - kasni) < kasni) izbaci.Add(jingl);
        }
        return izbaci;
    }
}

public static partial class IzvorDana
{
    public const string PodrazumevanaSema = @"D:\Shema dana UG radija";
    public const string PodrazumevaniJinglovi = @"D:\Jinglovi";

    /// <summary>Show jingles live among the rest and must not be played at random, so they are spotted by name.</summary>
    private static readonly string[] Emisije =
    [
        "etnopoganija", "koncertacija", "metal - novieti", "metal-noviteti", "preslusavanje", "napred-u-proslost",
        "rebel-rebel", "bluesmania", "fokus", "ciklonizacija", "muzika ispod radara", "albumi sa 5 zvezdica",
        "pustinja", "pustite decu", "pet minuta slave", "tribute", "enter the void",
    ];

    public static VrstaJingla PredlogZa(string imeFajla)
    {
        var ime = Path.GetFileNameWithoutExtension(imeFajla).ToLowerInvariant();
        if (ime.StartsWith("commercial") || ime.StartsWith("reklame") || ime.StartsWith("ekološki") || ime == "lakunoc") return VrstaJingla.Retko;
        if (ime.Contains("pocetak programa")) return VrstaJingla.Spica;
        if (ime.Contains("hit dana") || ime.Contains("hit of the day")) return VrstaJingla.HitDana;
        if (ime.StartsWith("intro") || Emisije.Any(ime.Contains)) return VrstaJingla.Emisija;
        return VrstaJingla.Opsti;
    }

    /// <summary>"MP3 Stereo 192 kbps 44,1 kHz", or nothing if the file cannot be read.</summary>
    public static string Kvalitet(string putanja)
    {
        try
        {
            using var f = TagLib.File.Create(putanja);
            var p = f.Properties;
            string kanali = p.AudioChannels switch { 1 => "Mono", 2 => "Stereo", _ => $"{p.AudioChannels} kanala" };
            return $"{Path.GetExtension(putanja).TrimStart('.').ToUpperInvariant()} {kanali} {p.AudioBitrate} kbps {p.AudioSampleRate / 1000.0:0.#} kHz";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TagLib.CorruptFileException or TagLib.UnsupportedFormatException or ArgumentException or NullReferenceException)
        {
            return "";
        }
    }

    public static FajlDana Procitaj(string putanja)
    {
        var s = Tagovi.Procitaj(new FileInfo(putanja), false);
        return new FajlDana(putanja, s.Izvodjac, s.Naslov, s.Trajanje);
    }

    /// <summary>Only the top of the folder: its subfolders hold what is out of use.</summary>
    public static List<string> FajloviJinglova(string folder) =>
        Directory.Exists(folder) ? Directory.EnumerateFiles(folder).Where(Tagovi.JeMuzika).OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase).ToList() : [];

    public static List<string> Fajlovi(string folder) =>
        Directory.EnumerateFiles(folder).Where(Tagovi.JeMuzika).OrderBy(BrojNaPocetku).ThenBy(f => f, StringComparer.CurrentCultureIgnoreCase).ToList();

    private static int BrojNaPocetku(string putanja)
    {
        var m = BrojRegex().Match(Path.GetFileName(putanja));
        return m.Success && int.TryParse(m.Value, out int broj) ? broj : int.MaxValue;
    }

    /// <summary>
    /// "20.00 Koncertacija", "20:00 Koncertacija" or "20h Koncertacija": the time is in the name, so nobody has to
    /// type it. Null when the name does not start with a time.
    /// </summary>
    public static (TimeSpan Vreme, string Naziv)? VremeIzImena(string ime)
    {
        var m = VremeRegex().Match(ime.Trim());
        if (!m.Success) return null;
        int sati = int.Parse(m.Groups[1].Value);
        int minuta = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
        if (sati > 23 || minuta > 59) return null;
        return (new TimeSpan(sati, minuta, 0), m.Groups[3].Value.Trim());
    }

    [GeneratedRegex(@"^(\d{1,2})(?:[.:](\d{2})h?|h)\s+(.+)$")]
    private static partial Regex VremeRegex();

    /// <summary>"05-11-2026 Reggae Tunes" is the show for that day.</summary>
    public static (DateTime Datum, string Naziv)? DatumFoldera(string ime)
    {
        var m = DatumRegex().Match(ime);
        if (!m.Success) return null;
        return DateTime.TryParseExact(m.Groups[1].Value, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out var datum)
            ? (datum, m.Groups[2].Value.Trim())
            : null;
    }

    [GeneratedRegex(@"^\d+")]
    private static partial Regex BrojRegex();

    [GeneratedRegex(@"^(\d{2}-\d{2}-\d{4})\s+(.+)$")]
    private static partial Regex DatumRegex();
}

public enum VrstaDela { Pocetak, Hit, Blok, Emisija, Pesme }

/// <summary>One line of the day by the hours: items <see cref="Od"/> up to (not including) <see cref="Do"/>.</summary>
public sealed record DeoDana(int Od, int Do, string Naziv, VrstaDela Vrsta, bool Nastavak);

/// <summary>
/// Cuts a built day into what a person thinks in: the opening, each hit, each block or show, and the songs that
/// carry on after a hit. The list itself stays as it is; this only groups it for the summary.
/// </summary>
public static class PodelaDana
{
    public const string PesmeBezBloka = "Pesme iz plejlista";

    public static List<DeoDana> Podeli(IReadOnlyList<StavkaDana> s)
    {
        var delovi = new List<DeoDana>();
        var videna = new HashSet<string>();
        string imeBloka = PesmeBezBloka;
        int od = 0;
        string naziv = "";
        var vrsta = VrstaDela.Pesme;
        bool nastavak = false, videoHit = false, posleHita = false, imaEmisiju = false;

        void Pocni(int i, string ime, VrstaDela v)
        {
            if (i > od) delovi.Add(new DeoDana(od, i, naziv, vrsta, nastavak));
            od = i;
            vrsta = v;
            nastavak = v is VrstaDela.Blok or VrstaDela.Pesme && !videna.Add(ime);
            naziv = ime;
            videoHit = posleHita = imaEmisiju = false;
        }

        for (int i = 0; i < s.Count; i++)
        {
            var x = s[i];
            bool hit = x.Zadato != null && x.NazivZadatog == "Hit dana";
            if (i == 0) Pocni(0, x.Vrsta == VrstaStavke.Spica ? "Početak dana" : x.NazivZadatog ?? imeBloka,
                x.Vrsta == VrstaStavke.Spica ? VrstaDela.Pocetak : x.Zadato != null ? VrstaDela.Blok : VrstaDela.Pesme);
            else if (hit) Pocni(i, "Hit dana", VrstaDela.Hit);
            else if (x.Zadato != null) Pocni(i, x.NazivZadatog ?? "Emisija", VrstaDela.Blok);
            else if (vrsta is VrstaDela.Hit or VrstaDela.Pocetak)
            {
                // The hit block is jingle, jingle of the hit, the hit and one jingle after it; then the songs carry on.
                if (x.Vrsta == VrstaStavke.Jingl && videoHit && !posleHita) posleHita = true;
                else if (x.Vrsta is VrstaStavke.Pesma or VrstaStavke.Emisija or VrstaStavke.Jingl && (videoHit || x.Vrsta != VrstaStavke.Jingl))
                    Pocni(i, imeBloka, imeBloka == PesmeBezBloka ? VrstaDela.Pesme : VrstaDela.Blok);
            }
            else if (imaEmisiju && x.Vrsta == VrstaStavke.Pesma)
            {
                Pocni(i, imeBloka, imeBloka == PesmeBezBloka ? VrstaDela.Pesme : VrstaDela.Blok);
            }

            if (x.Vrsta == VrstaStavke.HitDana) videoHit = true;
            if (x.Vrsta == VrstaStavke.Emisija && vrsta == VrstaDela.Blok)
            {
                // A show is one whole; songs after it belong to the block that was on before.
                vrsta = VrstaDela.Emisija;
                nastavak = false;
                imaEmisiju = true;
            }
            if (x.Vrsta == VrstaStavke.Pesma && vrsta == VrstaDela.Blok) imeBloka = naziv;
        }
        if (s.Count > od) delovi.Add(new DeoDana(od, s.Count, naziv, vrsta, nastavak));
        return delovi;
    }
}
