using System.Text.Json;

namespace Bronxamp.Podaci;

public sealed record ZapamcenaEmisija(string Vreme, string? Jingl);

public sealed class Podesavanja
{
    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    public double Prelaz { get; set; } = 3;
    public bool PrelazUAlbumima { get; set; }
    public int UpozorenjeNedelja { get; set; } = 8;

    /// <summary>Stored by name: Windows renumbers devices when one is plugged in or out.</summary>
    public string? Izlaz { get; set; }
    public string? Slusalice { get; set; }

    /// <summary>The shows that early versions created on their own have been cleaned up.</summary>
    public bool PocetneEmisijeUklonjene { get; set; }
    public float Jacina { get; set; } = 1f;
    public bool Mesanje { get; set; }
    public Ponavljanje Ponavljanje { get; set; }
    public string Prikaz { get; set; } = "Omoti";

    /// <summary>"sr" or "en". The key of the text, not the text itself: see <see cref="Prikaz.Rec"/>.</summary>
    public string Jezik { get; set; } = "sr";

    /// <summary>Limiter on the mixer: keeps peaks below full scale so the stream cannot clip.</summary>
    public bool Limiter { get; set; }

    /// <summary>Play every song at the same loudness (measured at scan time, see <see cref="Podaci.Baza"/>).</summary>
    public bool Normalizuj { get; set; }

    /// <summary>Target loudness in LUFS. −14 is the usual choice for a stream.</summary>
    public double CiljLufs { get; set; } = -14;

    /// <summary>If a song is already within this many dB of the target, it is left alone.</summary>
    public double PragDb { get; set; } = 1;

    public string FolderSema { get; set; } = IzvorDana.PodrazumevanaSema;
    public string FolderJinglovi { get; set; } = IzvorDana.PodrazumevaniJinglovi;
    public double Tolerancija { get; set; } = 5;

    /// <summary>Where the stream goes. The address and user are public, the mount is not shown to anyone else.</summary>
    public string StreamServer { get; set; } = "link.zeno.fm";
    public int StreamPort { get; set; } = 80;
    public string StreamMount { get; set; } = "";
    public string StreamKorisnik { get; set; } = "source";

    /// <summary>Encrypted for this Windows user, see <see cref="Sistem.Zastita"/>.</summary>
    public string StreamLozinka { get; set; } = "";
    public string StreamFormat { get; set; } = "MP3";
    public int StreamKbps { get; set; } = 128;

    /// <summary>Probe idu na lokalni Icecast (127.0.0.1:8000); pravi Zeno se ne dira dok je uključeno.</summary>
    public bool StreamProbno { get; set; } = true;

    /// <summary>The opening of the day. Empty until the people pick it.</summary>
    public string? PocetakSpica { get; set; }
    public string? PocetakJinglHita { get; set; }
    public string? PocetakJingl { get; set; }

    /// <summary>By file name, so a jingle keeps its label if the folder moves.</summary>
    public Dictionary<string, VrstaJingla> OznakeJinglova { get; set; } = [];
    public Dictionary<string, ZapamcenaEmisija> Emisije { get; set; } = [];

    public static IReadOnlyList<string> PodrazumevaneKolone { get; } = ["Izvodjac", "Naslov", "Trajanje"];

    /// <summary>Columns of the song list, in their order on screen.</summary>
    public List<string> KoloneSpiska { get; set; } = [.. PodrazumevaneKolone];
    public Dictionary<string, double> SirineKolona { get; set; } = [];

    public double? Levo { get; set; }
    public double? Gore { get; set; }
    public double Sirina { get; set; } = 1440;
    public double Visina { get; set; } = 860;
    public bool Uvecan { get; set; }

    public static Podesavanja Ucitaj()
    {
        try
        {
            if (File.Exists(Putanje.Podesavanja))
                return JsonSerializer.Deserialize<Podesavanja>(File.ReadAllText(Putanje.Podesavanja)) ?? new Podesavanja();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Dnevnik.Greska("Učitavanje podešavanja", ex);
        }
        return new Podesavanja();
    }

    public void Sacuvaj()
    {
        try
        {
            // Through a temporary file, so a crash during the write cannot leave a broken file behind.
            var privremeni = Putanje.Podesavanja + ".novo";
            File.WriteAllText(privremeni, JsonSerializer.Serialize(this, Format));
            File.Move(privremeni, Putanje.Podesavanja, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                       or ArgumentException or NotSupportedException)
        {
            Dnevnik.Greska("Čuvanje podešavanja", ex);
        }
    }
}
