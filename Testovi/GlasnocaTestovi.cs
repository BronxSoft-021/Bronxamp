using Bronxamp.Podaci;
using Bronxamp.Zvuk;
using static Bronxamp.Testovi.Spisak;

namespace Bronxamp.Testovi;

/// <summary>
/// Loudness: measuring a quiet and a loud file, the gain that brings each to the target, and that playing with
/// a gain changes only how loud the song is — never how long it lasts, so the day keeps its clock.
/// </summary>
public static class GlasnocaTestovi
{
    private static Podesavanja Podesi(double cilj = -14) => new() { Normalizuj = true, CiljLufs = cilj, PragDb = 1 };

    /// <summary>A sine of amplitude 0.5 measures about −9 dBFS RMS and its peak about −6 dBFS.</summary>
    public static Task Merenje()
    {
        using var p = new ZvukPlejer(dekodiranje: true);
        Tvrdi(p.Pokreni(0), "BASS nije pokrenut");
        var f = NoviFolder();
        var tiho = ZvukTestovi.NapisiWav(f, "tiho.wav", 440, 0, 44100 * 2, 0.05f);
        var glasno = ZvukTestovi.NapisiWav(f, "glasno.wav", 440, 0, 44100 * 2, 0.5f);

        var tihoIzmereno = Glasnoca.Izmeri(tiho);
        var glasnoIzmereno = Glasnoca.Izmeri(glasno);
        Tvrdi(tihoIzmereno != null, "tihi fajl se ne otvara");
        Tvrdi(glasnoIzmereno != null, "glasni fajl se ne otvara");
        var t = tihoIzmereno!.Value;
        var g = glasnoIzmereno!.Value;

        Tvrdi(Math.Abs(t.JacinaDb - -29.03) < 0.5, $"tiho izmereno {t.JacinaDb:0.00} dB, očekivano −29,03");
        Tvrdi(Math.Abs(g.JacinaDb - -9.03) < 0.5, $"glasno izmereno {g.JacinaDb:0.00} dB, očekivano −9,03");
        Tvrdi(Math.Abs(t.VrhDb - -26.02) < 0.5, $"vrh tihog {t.VrhDb:0.00} dB, očekivano −26,02");
        Tvrdi(Math.Abs(g.VrhDb - -6.02) < 0.5, $"vrh glasnog {g.VrhDb:0.00} dB, očekivano −6,02");

        // Ten times the amplitude is exactly 20 dB: the measurement must see the difference, because the gain
        // is worked out from it.
        Tvrdi(Math.Abs(g.JacinaDb - t.JacinaDb - 20) < 0.3, $"razlika {g.JacinaDb - t.JacinaDb:0.00} dB, očekivano 20");
        Console.WriteLine($"      tiho {t.JacinaDb:0.00} LUFS / vrh {t.VrhDb:0.00} dBFS, glasno {g.JacinaDb:0.00} / {g.VrhDb:0.00}");
        return Task.CompletedTask;
    }

    /// <summary>The gain itself: to the target, zero when close enough, held back by a peak already near the top.</summary>
    public static Task Pojacanje()
    {
        var p = Podesi();
        Tvrdi(Math.Abs(Glasnoca.PojacanjeDb(-20, -20, p) - 6) < 0.001, "tihom treba +6 dB");
        Tvrdi(Math.Abs(Glasnoca.PojacanjeDb(-9, -12, p) - -5) < 0.001, "glasnom treba −5 dB");
        Tvrdi(Math.Abs(Glasnoca.PojacanjeDb(-26, -0.5, p) - -0.5) < 0.001, "vrh drži pojačanje na −0,5 dB");
        Tvrdi(Glasnoca.PojacanjeDb(-13.5, -10, p) == 0, "unutar praga se ne dira");
        Tvrdi(Glasnoca.PojacanjeDb(null, null, p) == 0, "neizmerena pesma se ne dira");
        Tvrdi(Glasnoca.PojacanjeDb(-26, -26, new Podesavanja()) == 0, "isključena normalizacija ne dira ništa");
        Tvrdi(Math.Abs(Glasnoca.PojacanjeDb(-59, -59, p) - 20) < 0.001, "najveće pojačanje je +20 dB");

        // A ReplayGain tag is defined against −18 LUFS: a gain of −6,5 dB means the song sits at −11,5.
        var izTaga = Glasnoca.IzReplayGain(-6.5, 0.5);
        Tvrdi(izTaga != null, "tag nije pročitan");
        Tvrdi(Math.Abs(izTaga!.Value.JacinaDb - -11.5) < 0.001, $"iz taga {izTaga.Value.JacinaDb:0.00}, očekivano −11,50");
        Tvrdi(izTaga.Value.VrhDb is { } vrhTaga && Math.Abs(vrhTaga - -6.02) < 0.01, "vrh iz taga");
        Tvrdi(Glasnoca.IzReplayGain(double.NaN, double.NaN) == null, "fajl bez taga nema glasnoću");
        return Task.CompletedTask;
    }

    /// <summary>
    /// The gain really reaches the mixer, it rises over the first 50 ms instead of jumping, and the song is
    /// still exactly as long as the file: that is what keeps a planned day on its clock.
    /// </summary>
    public static async Task Pustanje()
    {
        var f = NoviFolder();
        var a = ZvukTestovi.NapisiWav(f, "a.wav", 440, 0, 44100 * 2, 0.5f);

        using var p = new ZvukPlejer(dekodiranje: true);
        Tvrdi(p.Pokreni(0), "BASS nije pokrenut");
        Tvrdi(await p.PustiAsync(new ZvukStavka(a, null, -6)), "nije pušteno");
        await Task.Delay(300);

        var uzorci = ZvukTestovi.Procitaj(p.Mikser, 44100 * 3 / 2);
        int sempala = uzorci.Length / 2;

        // The decoder's own length is what the day is planned with: a gain must not stretch or cut it.
        Tvrdi(Math.Abs(p.Trajanje - 2.0) < 0.01, $"dužina pesme se promenila: {p.Trajanje:0.000} s");
        Tvrdi(sempala >= 44100 * 3 / 2 - 2, $"mikser je dao samo {sempala} sempala");

        double vrh = 0, uvod = 0;
        for (int i = 0; i < sempala; i++)
        {
            double v = Math.Abs(uzorci[i * 2]);
            if (v > vrh) vrh = v;
            if (i < 44100 * 5 / 1000 && v > uvod) uvod = v;
        }

        double ocekivano = 0.5 * Math.Pow(10, -6 / 20.0);
        Tvrdi(Math.Abs(vrh - ocekivano) < 0.01, $"vrh {vrh:F3}, očekivano {ocekivano:F3}");
        Tvrdi(uvod < ocekivano * 0.35, $"uvod nije tih: {uvod:F3} u prvih 5 ms");
        Console.WriteLine($"      {sempala} sempala, vrh {vrh:F3} (očekivano {ocekivano:F3}), prvih 5 ms najviše {uvod:F3}");
    }

    /// <summary>Measuring in the background writes the values onto the song, and a later scan does not lose them.</summary>
    public static async Task Biblioteka()
    {
        var koren = NoviFolder();
        using var baza = new Baza(Path.Combine(koren, "t.db"));
        var katalog = new Katalog(baza);
        using var skener = new Skener(baza, katalog);
        var muzika = Path.Combine(koren, "muzika");
        Directory.CreateDirectory(muzika);
        var fajl = ZvukTestovi.NapisiWav(muzika, "a.wav", 440, 0, 44100 * 2, 0.5f);

        using var p = new ZvukPlejer(dekodiranje: true);
        Tvrdi(p.Pokreni(0), "BASS nije pokrenut");

        baza.DodajFolder(muzika);
        await skener.SkenirajAsync();
        var pre = katalog.Pesme.Single();
        Tvrdi(pre.JacinaDb == null && pre.VrhDb == null, "glasnoća je izmišljena pre merenja");

        var napredak = new List<NapredakMerenja>();
        skener.NapredakMerenja += napredak.Add;
        int izmereno = await skener.IzmeriAsync(sve: true);
        Tvrdi(izmereno == 1, $"izmereno {izmereno}");
        Tvrdi(napredak.Count > 0 && napredak[^1].Gotovo && napredak[^1].Obradjeno == 1, "progres merenja");

        var posle = katalog.Pesme.Single();
        Tvrdi(posle.JacinaDb is { } jacina && Math.Abs(jacina - -9.03) < 0.5, $"upisana glasnoća {posle.JacinaDb:0.00}");
        Tvrdi(posle.VrhDb is not null, "vrh nije upisan");
        Tvrdi(Math.Abs(posle.Trajanje - pre.Trajanje) < 1e-9, "merenje je promenilo dužinu pesme");

        // The file is rewritten and read again: what was measured must survive, since the tags carry no ReplayGain.
        ZvukTestovi.NapisiWav(muzika, "a.wav", 440, 0, 44100, 0.5f);
        await skener.SkenirajAsync();
        var ponovo = katalog.Pesme.Single();
        Tvrdi(Math.Abs(ponovo.Trajanje - 1) < 0.05, $"dužina posle izmene {ponovo.Trajanje:0.00} s");
        Tvrdi(ponovo.JacinaDb is { } posleSkena && Math.Abs(posleSkena - -9.03) < 0.5, "skeniranje je obrisalo glasnoću");
        Tvrdi(File.Exists(fajl), "fajl je nestao");
    }

    /// <summary>A library made before this version has no loudness columns, and must get them without losing anything.</summary>
    public static async Task NadogradnjaGlasnoce()
    {
        var koren = NoviFolder();
        var putanja = Path.Combine(koren, "t.db");
        var muzika = Path.Combine(koren, "muzika");
        Directory.CreateDirectory(muzika);
        ZvukTestovi.NapisiWav(muzika, "a.wav", 440, 0, 44100, 0.5f);

        long id;
        using (var baza = new Baza(putanja))
        {
            var katalog = new Katalog(baza);
            using var skener = new Skener(baza, katalog);
            baza.DodajFolder(muzika);
            await skener.SkenirajAsync();
            id = katalog.Pesme[0].Id;
            baza.PostaviOmiljeno(id, true);
        }

        using (var veza = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={putanja};Pooling=False"))
        {
            veza.Open();
            using var k = veza.CreateCommand();
            k.CommandText = "ALTER TABLE pesme DROP COLUMN jacina_db; ALTER TABLE pesme DROP COLUMN vrh_db;";
            k.ExecuteNonQuery();
        }

        using (var baza = new Baza(putanja))
        {
            var katalog = new Katalog(baza);
            katalog.Ucitaj();
            var pesma = katalog.Pesme.Single();
            Tvrdi(pesma.Id == id && pesma.Omiljeno, "nadogradnja je izgubila pesmu ili omiljeno");
            Tvrdi(pesma.JacinaDb == null && pesma.VrhDb == null, "nove kolone nisu prazne");
            Tvrdi(baza.PesmeZaMerenje().Count == 1, "pesma bez glasnoće nije na spisku za merenje");
        }
    }}
