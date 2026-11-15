using System.Text.RegularExpressions;

namespace Bronxamp.Podaci;

public sealed class SkeniranaPesma
{
    public string Putanja { get; init; } = "";
    public long Velicina { get; init; }
    public long Izmenjeno { get; init; }
    public string Naslov { get; init; } = "";
    public string Izvodjac { get; init; } = "";
    public string AlbumKljuc { get; set; } = "";
    public string AlbumNaziv { get; init; } = "";
    public string AlbumIzvodjac { get; set; } = "";
    public string Folder { get; init; } = "";
    public int Godina { get; init; }
    public string Zanr { get; init; } = "";
    public int Broj { get; init; }
    public int Disk { get; init; }
    public double Trajanje { get; init; }
    public int Bitrate { get; init; }
    public int Frekvencija { get; init; }
    public bool Nova { get; init; }
}

public static partial class Tagovi
{
    public const string RazniIzvodjaci = "Razni izvođači";
    public const string NepoznatIzvodjac = "Nepoznat izvođač";

    public static readonly HashSet<string> Ekstenzije = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".ogg", ".oga", ".opus", ".m4a", ".mp4", ".wav",
    };

    public static bool JeMuzika(string putanja) => Ekstenzije.Contains(Path.GetExtension(putanja));

    /// <summary>Reads one file. A broken tag still gives a usable song named after the file.</summary>
    public static SkeniranaPesma Procitaj(FileInfo fajl, bool nova)
    {
        string naslov = "", izvodjac = "", album = "", albumIzvodjac = "", zanr = "";
        int godina = 0, broj = 0, disk = 0, bitrate = 0, frekvencija = 0;
        double trajanje = 0;

        try
        {
            using var f = TagLib.File.Create(fajl.FullName);
            var t = f.Tag;
            naslov = Popravi(t.Title);
            izvodjac = Popravi(t.JoinedPerformers);
            album = Popravi(t.Album);
            albumIzvodjac = Popravi(t.JoinedAlbumArtists);
            zanr = Popravi(t.JoinedGenres);
            godina = (int)t.Year;
            broj = (int)t.Track;
            disk = (int)t.Disc;
            trajanje = f.Properties?.Duration.TotalSeconds ?? 0;
            bitrate = f.Properties?.AudioBitrate ?? 0;
            frekvencija = f.Properties?.AudioSampleRate ?? 0;
        }
        catch (Exception ex) when (ex is TagLib.CorruptFileException or TagLib.UnsupportedFormatException
                                       or IOException or UnauthorizedAccessException or ArgumentException
                                       or NullReferenceException or IndexOutOfRangeException or OverflowException)
        {
            Dnevnik.Upisi($"Tagovi \"{fajl.FullName}\": {ex.Message}");
        }

        var folder = fajl.DirectoryName ?? "";

        // Without tags the names are usually in the folder ("Izvođač - Album (2018)") and the file ("03 - Pesma").
        var (izvodjacFoldera, albumFoldera, godinaFoldera) = IzFoldera(Path.GetFileName(OsnovniFolder(folder)));
        if (naslov.Length == 0)
        {
            var (brojFajla, izvodjacFajla, naslovFajla) = IzImenaFajla(Path.GetFileNameWithoutExtension(fajl.Name));
            naslov = naslovFajla;
            if (broj == 0) broj = brojFajla;
            if (izvodjac.Length == 0) izvodjac = izvodjacFajla;
        }
        if (izvodjac.Length == 0) izvodjac = albumIzvodjac.Length > 0 ? albumIzvodjac : izvodjacFoldera;
        if (izvodjac.Length == 0) izvodjac = NepoznatIzvodjac;
        if (album.Length == 0) album = albumFoldera;
        if (godina < 1000 || godina > DateTime.Now.Year + 1) godina = godinaFoldera;

        return new SkeniranaPesma
        {
            Putanja = fajl.FullName,
            Velicina = fajl.Length,
            Izmenjeno = fajl.LastWriteTimeUtc.Ticks,
            Naslov = naslov,
            Izvodjac = izvodjac,
            AlbumNaziv = album,
            AlbumIzvodjac = albumIzvodjac,
            AlbumKljuc = KljucAlbuma(folder, album),
            Folder = OsnovniFolder(folder),
            Godina = godina,
            Zanr = zanr,
            Broj = broj,
            Disk = disk,
            Trajanje = trajanje,
            Bitrate = bitrate,
            Frekvencija = frekvencija,
            Nova = nova,
        };
    }

    private static string Ocisti(string? s) => (s ?? "").Replace('\0', ' ').Trim();

    private static string Popravi(string? s) => PopraviKodiranje(Ocisti(s));

    private const string ZapadnaSlova = "éêëàáâãäåçìíîïñòóôõöùúûüýÿßÉÀÁÂÄÅÇÍÑÓÔÖÚÜ";

    /// <summary>
    /// Old ex-YU tags hold Windows-1250 text in fields marked Latin-1, so "č" reads as "è" and "š" as a
    /// control character. A real West European title is recognized by its other accented letters and kept.
    /// </summary>
    public static string PopraviKodiranje(string tekst)
    {
        if (JeCirilicaKaoLatin1(tekst)) return IzWindows1251(tekst);
        if (tekst.AsSpan().IndexOfAny(ZapadnaSlova) >= 0) return tekst;

        return tekst
            .Replace('\u008A', 'Š').Replace('\u009A', 'š').Replace('\u008E', 'Ž').Replace('\u009E', 'ž')
            .Replace('È', 'Č').Replace('è', 'č').Replace('Æ', 'Ć').Replace('æ', 'ć').Replace('Ð', 'Đ').Replace('ð', 'đ');
    }

    /// <summary>
    /// Windows-1251 read as Latin-1 turns nearly every letter into À-ÿ ("Íà Ðàéîíå"). No real Latin text is
    /// that dense with accents, which is what tells them apart.
    /// </summary>
    private static bool JeCirilicaKaoLatin1(string tekst)
    {
        int slova = 0, visoka = 0;
        foreach (char c in tekst)
        {
            if (c > 0xFF) return false;
            if (!char.IsLetter(c)) continue;
            slova++;
            if (c >= 0xC0) visoka++;
        }
        return slova >= 4 && visoka * 10 >= slova * 6;
    }

    private static string IzWindows1251(string tekst)
    {
        var slova = tekst.ToCharArray();
        for (int i = 0; i < slova.Length; i++)
        {
            char c = slova[i];
            if (c >= 0xC0 && c <= 0xFF) slova[i] = (char)(0x0410 + c - 0xC0);
            else if (Cirilica1251.TryGetValue(c, out var slovo)) slova[i] = slovo;
        }
        return new string(slova);
    }

    /// <summary>The Serbian letters, which sit outside the main Cyrillic block of Windows-1251.</summary>
    private static readonly Dictionary<char, char> Cirilica1251 = new()
    {
        ['\u0080'] = 'Ђ', ['\u0090'] = 'ђ', ['\u008A'] = 'Љ', ['\u009A'] = 'љ', ['\u008C'] = 'Њ', ['\u009C'] = 'њ',
        ['\u008E'] = 'Ћ', ['\u009E'] = 'ћ', ['\u008F'] = 'Џ', ['\u009F'] = 'џ', ['£'] = 'Ј', ['¼'] = 'ј',
    };

    /// <summary>Understands "Izvođač - Album (2018)", with optional [notes]. Anything else is just the album name.</summary>
    public static (string Izvodjac, string Album, int Godina) IzFoldera(string ime)
    {
        var bezGodine = ime;
        int godina = 0;
        var g = GodinaUZagradi().Match(ime);
        if (g.Success)
        {
            godina = int.Parse(g.Groups[1].Value);
            bezGodine = ime.Remove(g.Index, g.Length).Trim();
        }

        int crta = bezGodine.IndexOf(" - ", StringComparison.Ordinal);
        if (crta <= 0) return ("", bezGodine, godina);

        var izvodjac = bezGodine[..crta].Trim();
        var album = bezGodine[(crta + 3)..].Trim();
        return izvodjac.Equals("Various Artists", StringComparison.OrdinalIgnoreCase) ? ("", album, godina) : (izvodjac, album, godina);
    }

    /// <summary>Understands "03 - Pesma", "03. Pesma", "03 Izvođač - Pesma" and "Izvođač - Pesma".</summary>
    public static (int Broj, string Izvodjac, string Naslov) IzImenaFajla(string ime)
    {
        int broj = 0;
        var m = BrojNaPocetku().Match(ime);
        if (m.Success)
        {
            broj = int.Parse(m.Groups[1].Value);
            ime = ime[m.Length..];
        }

        int crta = ime.IndexOf(" - ", StringComparison.Ordinal);
        return crta > 0 ? (broj, ime[..crta].Trim(), ime[(crta + 3)..].Trim()) : (broj, "", ime.Trim());
    }

    [GeneratedRegex(@"[\(\[]((?:19|20)\d\d)[\)\]]")]
    private static partial Regex GodinaUZagradi();

    [GeneratedRegex(@"^(\d{1,3})[\s._-]+(?:-\s+)?")]
    private static partial Regex BrojNaPocetku();

    /// <summary>
    /// Album identity is the folder plus the album name. Grouping by tags alone would split a compilation
    /// without an album-artist tag into one album per artist.
    /// </summary>
    public static string KljucAlbuma(string folder, string album) =>
        $"{OsnovniFolder(folder).ToLowerInvariant()}|{Pretraga.Skini(album)}";

    /// <summary>Treats "Album\CD1" and "Album\CD2" as one album.</summary>
    public static string OsnovniFolder(string folder)
    {
        var ime = Path.GetFileName(folder.TrimEnd('\\'));
        return DiskFolder().IsMatch(ime) ? Path.GetDirectoryName(folder.TrimEnd('\\')) ?? folder : folder;
    }

    [GeneratedRegex(@"^(cd|disc|disk|disk\s*no\.?|vol\.?)[\s._-]*\d{1,2}$", RegexOptions.IgnoreCase)]
    private static partial Regex DiskFolder();
}
