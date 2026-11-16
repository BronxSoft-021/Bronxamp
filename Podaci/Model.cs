namespace Bronxamp.Podaci;

public sealed class Pesma
{
    public long Id { get; set; }
    public string Putanja { get; set; } = "";
    public long Velicina { get; set; }
    public long Izmenjeno { get; set; }
    public string Naslov { get; set; } = "";
    public string Izvodjac { get; set; } = "";
    public long AlbumId { get; set; }
    public int Godina { get; set; }
    public string Zanr { get; set; } = "";
    public int Broj { get; set; }
    public int Disk { get; set; }
    public double Trajanje { get; set; }
    public bool Omiljeno { get; set; }

    /// <summary>In kbps; 0 until the file has been read by this version.</summary>
    public int Bitrate { get; set; }
    public int Frekvencija { get; set; }

    /// <summary>
    /// Measured loudness in LUFS and the highest sample in dBFS, from the ReplayGain tag or from decoding the
    /// file (see <see cref="Zvuk.Glasnoca"/>). Null until the song is measured.
    /// </summary>
    public double? JacinaDb { get; set; }
    public double? VrhDb { get; set; }

    public int Pustano { get; set; }
    public DateTime? PoslednjePustanje { get; set; }

    /// <summary>Set when the library is loaded.</summary>
    public Album Album { get; set; } = null!;

    /// <summary>All searchable fields, folded by <see cref="Pretraga.Skini"/>.</summary>
    public string ZaPretragu { get; set; } = "";

    public string TrajanjeTekst => Vreme.Kratko(Trajanje);
}

public sealed class Album
{
    public long Id { get; set; }
    public string Kljuc { get; set; } = "";
    public string Naziv { get; set; } = "";
    public string Izvodjac { get; set; } = "";
    public int Godina { get; set; }
    public string Folder { get; set; } = "";
    public DateTime Dodato { get; set; }
    public List<Pesma> Pesme { get; } = [];

    public DateTime? PoslednjePustanje { get; set; }

    public double Trajanje => Pesme.Sum(p => p.Trajanje);
    public string Zanr => Pesme.FirstOrDefault(p => p.Zanr.Length > 0)?.Zanr ?? "";
}

public sealed class Emisija
{
    public long Id { get; set; }
    public string Ime { get; set; } = "";
    public string Boja { get; set; } = "#E07B39";
    public int Redosled { get; set; }
}

public sealed record Emitovanje(long PesmaId, DateTime Vreme);

public sealed record Slusanost(Pesma Pesma, int Puta);

public static class Vreme
{
    /// <summary>m:ss, or h:mm:ss from one hour up (mock-up: 07:12 and 1:10:09).</summary>
    public static string Kratko(double sekunde)
    {
        if (double.IsNaN(sekunde) || sekunde < 0) sekunde = 0;
        var t = TimeSpan.FromSeconds(Math.Round(sekunde));
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}";
    }

    /// <summary>Always h:mm:ss, for totals.</summary>
    public static string Dugo(double sekunde)
    {
        var t = TimeSpan.FromSeconds(Math.Round(Math.Max(0, sekunde)));
        return $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";
    }

    public static string BrojPesama(int n)
    {
        int d = n % 10, s = n % 100;
        if (d == 1 && s != 11) return $"{n} pesma";
        if (d is >= 2 and <= 4 && s is < 12 or > 14) return $"{n} pesme";
        return $"{n} pesama";
    }
}
