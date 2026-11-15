using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bronxamp.Podaci;

/// <summary>
/// One-box search. Every word must be found somewhere in the song's fields. Case, diacritics and script are
/// ignored ("sta", "šta", "шта" are the same). "90-e" means 1990-1999. A short word also matches a name by
/// its word beginnings, so "ekv" finds Ekatarina Velika.
/// </summary>
public sealed partial class Pretraga
{
    private readonly record struct Rec(string Tekst, int OdGodine, int DoGodine, bool MozeSkracenica);

    private readonly List<Rec> _reci = [];
    private readonly Dictionary<string, bool[]> _skracenice = [];

    public bool Prazna => _reci.Count == 0;

    public Pretraga(string? upit)
    {
        foreach (var deo in (upit ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var rec = Skini(deo);
            if (rec.Length == 0) continue;

            var (od, @do) = Godine(rec);
            _reci.Add(new Rec(rec, od, @do, rec.Length is >= 2 and <= 8 && rec.All(char.IsLetter)));
        }
    }

    public bool Odgovara(Pesma p)
    {
        for (int i = 0; i < _reci.Count; i++)
        {
            var r = _reci[i];
            if (r.OdGodine > 0 && p.Godina >= r.OdGodine && p.Godina <= r.DoGodine) continue;
            if (p.ZaPretragu.Contains(r.Tekst, StringComparison.Ordinal)) continue;
            if (r.MozeSkracenica && (Skracenica(i, p.Izvodjac) || Skracenica(i, p.Album?.Izvodjac ?? ""))) continue;
            return false;
        }
        return true;
    }

    private bool Skracenica(int indeks, string ime)
    {
        if (!_skracenice.TryGetValue(ime, out var rezultati))
        {
            rezultati = new bool[_reci.Count];
            var reci = Skini(ime).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < _reci.Count; i++)
                rezultati[i] = _reci[i].MozeSkracenica && reci.Length >= 2 && Pocetci(_reci[i].Tekst, 0, reci, 0);
            _skracenice[ime] = rezultati;
        }
        return rezultati[indeks];
    }

    /// <summary>Can the word be cut into beginnings of consecutive name words, using every word?</summary>
    private static bool Pocetci(string rec, int odakle, string[] reci, int i)
    {
        if (i == reci.Length) return odakle == rec.Length;
        int preostalo = reci.Length - i - 1;
        int najvise = Math.Min(reci[i].Length, rec.Length - odakle - preostalo);
        for (int n = najvise; n >= 1; n--)
            if (string.CompareOrdinal(rec, odakle, reci[i], 0, n) == 0 && Pocetci(rec, odakle + n, reci, i + 1))
                return true;
        return false;
    }

    private static (int Od, int Do) Godine(string rec)
    {
        var m = Decenija().Match(rec);
        if (m.Success)
        {
            int broj = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            int od = m.Groups[1].Length == 4 ? broj : broj < 30 ? 2000 + broj : 1900 + broj;
            return (od, od + 9);
        }
        if (rec.Length == 4 && int.TryParse(rec, out int g) && g is >= 1900 and <= 2099)
            return (g, g);
        return (0, 0);
    }

    [GeneratedRegex(@"^(\d0|\d{3}0)-?(e|te|ih)$")]
    private static partial Regex Decenija();

    private static readonly Dictionary<char, string> Cirilica = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['ђ'] = "dj", ['е'] = "e", ['ж'] = "z",
        ['з'] = "z", ['и'] = "i", ['ј'] = "j", ['к'] = "k", ['л'] = "l", ['љ'] = "lj", ['м'] = "m", ['н'] = "n",
        ['њ'] = "nj", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['ћ'] = "c", ['у'] = "u",
        ['ф'] = "f", ['х'] = "h", ['ц'] = "c", ['ч'] = "c", ['џ'] = "dz", ['ш'] = "s",
    };

    /// <summary>Lower case, no diacritics, Latin script.</summary>
    public static string Skini(string tekst)
    {
        if (string.IsNullOrEmpty(tekst)) return "";

        var sb = new StringBuilder(tekst.Length + 4);
        foreach (var slovo in tekst.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (char.GetUnicodeCategory(slovo) == UnicodeCategory.NonSpacingMark) continue;
            if (slovo == 'đ') sb.Append("dj");
            else if (Cirilica.TryGetValue(slovo, out var latinica)) sb.Append(latinica);
            else sb.Append(slovo);
        }
        return sb.ToString();
    }

    /// <summary>Text the search runs on. "đ" is stored as both "dj" and "d", because people type either.</summary>
    public static string TekstZaPretragu(Pesma p, Album a)
    {
        var tekst = Skini($"{p.Naslov} {p.Izvodjac} {a.Naziv} {a.Izvodjac} {p.Zanr} {(p.Godina > 0 ? p.Godina : "")}");
        return tekst.Contains("dj", StringComparison.Ordinal) ? tekst + " " + tekst.Replace("dj", "d") : tekst;
    }
}
