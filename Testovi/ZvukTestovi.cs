using System.Text;
using Bronxamp.Zvuk;
using ManagedBass;
using static Bronxamp.Testovi.Spisak;

namespace Bronxamp.Testovi;

public static class ZvukTestovi
{
    private const int Frekvencija = 44100;

    /// <summary>Writes a float WAV of a sine, continuing from the given sample so two files join in phase.</summary>
    internal static string NapisiWav(string folder, string ime, double hz, long odSempla, long sempala, float jacina = 0.5f)
    {
        var putanja = Path.Combine(folder, ime);
        using var w = new BinaryWriter(File.Create(putanja));
        int bajtova = (int)sempala * 2 * 4;
        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bajtova); w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)3); w.Write((short)2);
        w.Write(Frekvencija); w.Write(Frekvencija * 8); w.Write((short)8); w.Write((short)32);
        w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bajtova);
        for (long i = odSempla; i < odSempla + sempala; i++)
        {
            float v = jacina * (float)Math.Sin(2 * Math.PI * hz * i / Frekvencija);
            w.Write(v); w.Write(v);
        }
        return putanja;
    }

    /// <summary>A minute of tone, for the live checks of the stream (it must keep playing while the server comes and goes).</summary>
    internal static string Wav(string folder) => NapisiWav(folder, "ton.wav", 440, 0, Frekvencija * 60);

    internal static float[] Procitaj(int mikser, int sempala)
    {
        var sve = new List<float>(sempala * 2);
        var bafer = new float[8192];
        while (sve.Count < sempala * 2)
        {
            int n = Bass.ChannelGetData(mikser, bafer, bafer.Length * 4 | (int)DataFlags.Float);
            if (n <= 0) break;
            sve.AddRange(bafer.Take(n / 4));
        }
        return sve.ToArray();
    }

    public static async Task Gapless()
    {
        var f = NoviFolder();
        long deo = Frekvencija * 2 + 1234;      // deliberately not a multiple of any buffer size
        var a = NapisiWav(f, "a.wav", 440, 0, deo);
        var b = NapisiWav(f, "b.wav", 440, deo, deo);

        using var p = new ZvukPlejer(dekodiranje: true);
        Tvrdi(p.Pokreni(0), "BASS nije pokrenut");
        await p.PustiAsync(new ZvukStavka(a));
        p.PostaviSledecu(new ZvukStavka(b), 0);
        await Task.Delay(300);

        var uzorci = Procitaj(p.Mikser, (int)(deo * 2 + 1000));
        int sempala = uzorci.Length / 2;
        Tvrdi(Math.Abs(sempala - deo * 2) <= 2, $"ukupno {sempala} sempala, očekivano {deo * 2}");

        // A sine at 440 Hz moves at most amplitude*2*pi*f/fs per sample. Any gap or click exceeds that.
        double najvecaRazlika = 0.5 * 2 * Math.PI * 440 / Frekvencija * 1.05;
        double najveca = 0;
        for (int i = 1; i < sempala; i++)
            najveca = Math.Max(najveca, Math.Abs(uzorci[i * 2] - uzorci[(i - 1) * 2]));
        Tvrdi(najveca <= najvecaRazlika, $"skok u talasu {najveca:F4} (dozvoljeno {najvecaRazlika:F4})");
        Console.WriteLine($"      spoj bez skoka: {sempala} sempala, najveći korak {najveca:F4} od dozvoljenih {najvecaRazlika:F4}");
    }

    public static async Task Crossfade()
    {
        var f = NoviFolder();
        long deo = Frekvencija * 6;
        var a = NapisiWav(f, "a.wav", 300, 0, deo);
        var b = NapisiWav(f, "b.wav", 1000, 0, deo);

        using var p = new ZvukPlejer(dekodiranje: true);
        Tvrdi(p.Pokreni(0), "BASS nije pokrenut");
        await p.PustiAsync(new ZvukStavka(a));
        p.PostaviSledecu(new ZvukStavka(b), 2.0);
        await Task.Delay(300);

        var uzorci = Procitaj(p.Mikser, (int)(deo * 2));
        double sekundi = uzorci.Length / 2.0 / Frekvencija;
        Tvrdi(Math.Abs(sekundi - 10) < 0.1, $"ukupno {sekundi:F2} s, očekivano 10 s (6 + 6 - 2)");

        // RMS per 100 ms window: stays near the sine's 0.354 through the crossfade (two uncorrelated sines).
        int prozor = Frekvencija / 10;
        var rms = new List<double>();
        for (int s = 0; s + prozor < uzorci.Length / 2; s += prozor)
        {
            double z = 0;
            for (int i = s; i < s + prozor; i++) z += uzorci[i * 2] * uzorci[i * 2];
            rms.Add(Math.Sqrt(z / prozor));
        }
        double min = rms.Take(rms.Count - 1).Min(), max = rms.Max();
        Tvrdi(min > 0.30 && max < 0.42, $"jačina kroz prelaz {min:F3}..{max:F3}");
        Console.WriteLine($"      trajanje {sekundi:F2} s, jačina kroz prelaz {min:F3}..{max:F3}");
    }

    public static async Task PromenaKadSeCuje()
    {
        var f = NoviFolder();
        var a = NapisiWav(f, "a.wav", 440, 0, Frekvencija * 2);
        var b = NapisiWav(f, "b.wav", 440, 0, Frekvencija * 2);
        var c = NapisiWav(f, "c.wav", 440, 0, Frekvencija);

        using var p = new ZvukPlejer();
        Tvrdi(p.Pokreni(0), "BASS nije pokrenut");

        var pocetak = DateTime.Now;
        var promene = new List<(string Ime, double Sek)>();
        var kraj = new TaskCompletionSource();
        var sb = new ZvukStavka(b);
        var sc = new ZvukStavka(c);
        p.PesmaPocela += s =>
        {
            promene.Add((Path.GetFileName(s.Putanja), (DateTime.Now - pocetak).TotalSeconds));
            if (s == sb) p.PostaviSledecu(sc, 0);
            if (s == sc) p.PostaviSledecu(null, 0);
        };
        p.RedZavrsen += () => kraj.TrySetResult();

        await p.PustiAsync(new ZvukStavka(a));
        pocetak = DateTime.Now;
        p.PostaviSledecu(sb, 0);

        await Task.Delay(1000);
        double pozicija = p.Pozicija;
        Tvrdi(Math.Abs(pozicija - 1.0) < 0.2, $"pozicija posle 1 s: {pozicija:F2}");

        var gotovo = await Task.WhenAny(kraj.Task, Task.Delay(8000));
        Tvrdi(gotovo == kraj.Task, "kraj reda nije javljen");
        Tvrdi(promene.Count == 2, $"promene: {string.Join(", ", promene)}");
        Tvrdi(Math.Abs(promene[0].Sek - 2.0) < 0.25, $"b.wav čuje se u {promene[0].Sek:F2} s, očekivano 2 s");
        Tvrdi(Math.Abs(promene[1].Sek - 4.0) < 0.25, $"c.wav čuje se u {promene[1].Sek:F2} s, očekivano 4 s");
        double ukupno = (DateTime.Now - pocetak).TotalSeconds;
        Tvrdi(Math.Abs(ukupno - 5.0) < 0.4, $"kraj u {ukupno:F2} s, očekivano 5 s");
        Console.WriteLine($"      pozicija posle 1 s: {pozicija:F2}; promene u {promene[0].Sek:F2} i {promene[1].Sek:F2} s; kraj u {ukupno:F2} s");
    }

    public static async Task PrelazUzivo()
    {
        var f = NoviFolder();
        var a = new ZvukStavka(NapisiWav(f, "a.wav", 300, 0, Frekvencija * 10));
        var b = new ZvukStavka(NapisiWav(f, "b.wav", 1000, 0, Frekvencija * 4));

        using var p = new ZvukPlejer();
        Tvrdi(p.Pokreni(0), "BASS nije pokrenut");

        var promena = new TaskCompletionSource<double>();
        var kraj = new TaskCompletionSource();
        var sat = System.Diagnostics.Stopwatch.StartNew();
        p.PesmaPocela += _ => promena.TrySetResult(sat.Elapsed.TotalSeconds);
        p.RedZavrsen += () => kraj.TrySetResult();

        await p.PustiAsync(a);
        p.PostaviSledecu(b, 1.5);
        await Task.Delay(400);

        // Seek forward: 7 s of a 10 s song, 1.5 s before the crossfade starts at 8.5 s.
        sat.Restart();
        p.Premotaj(7);
        await Task.Delay(300);
        Tvrdi(Math.Abs(p.Pozicija - 7.3) < 0.25, $"pozicija posle premotavanja {p.Pozicija:F2}");

        // A pause must hold the crossfade back too.
        p.Pauza();
        double zaustavljeno = p.Pozicija;
        await Task.Delay(500);
        Tvrdi(Math.Abs(p.Pozicija - zaustavljeno) < 0.02, "pozicija se menja tokom pauze");
        p.Nastavi();

        var prvi = await Task.WhenAny(promena.Task, Task.Delay(6000));
        Tvrdi(prvi == promena.Task, "prelaz nije počeo");
        double kadaPocinje = await promena.Task;
        Tvrdi(Math.Abs(kadaPocinje - 2.0) < 0.35, $"prelaz počeo posle {kadaPocinje:F2} s, očekivano oko 2 s (1,5 + pauza 0,5)");
        Tvrdi(p.Tekuca == b, "tokom prelaza prikazuje se nova pesma");

        await Task.Delay(500);
        Tvrdi(p.Pozicija is > 0.2 and < 1.0, $"pozicija nove pesme {p.Pozicija:F2}");

        // Seeking during a crossfade cuts the fading track and must leave the new one at full volume.
        p.Premotaj(3);
        await Task.Delay(200);
        Tvrdi(Math.Abs(p.Pozicija - 3.2) < 0.25, $"premotavanje tokom prelaza {p.Pozicija:F2}");

        p.PostaviSledecu(null, 0);
        var gotovo = await Task.WhenAny(kraj.Task, Task.Delay(4000));
        Tvrdi(gotovo == kraj.Task, "kraj reda nije javljen");
        Console.WriteLine($"      prelaz počeo u {kadaPocinje:F2} s");
    }

    /// <summary>
    /// A manual change empties the mixer for a moment. If that is taken for the end of the queue, the player
    /// believes nothing plays while the song does, and the next press of Pause starts the song over.
    /// </summary>
    public static async Task RucnaPromena(int uredjaj)
    {
        var f = NoviFolder();
        var a = new ZvukStavka(NapisiWav(f, "a.wav", 440, 0, Frekvencija * 8, jacina: 0));
        var b = new ZvukStavka(NapisiWav(f, "b.wav", 330, 0, Frekvencija * 8, jacina: 0));

        using var p = new ZvukPlejer();
        Tvrdi(p.Pokreni(uredjaj), "BASS nije pokrenut");
        int lazniKrajevi = 0;
        p.RedZavrsen += () => Interlocked.Increment(ref lazniKrajevi);

        const int Puta = 150;
        for (int i = 0; i < Puta; i++)
        {
            Tvrdi(await p.PustiAsync(i % 2 == 0 ? a : b), "nije pušteno");
            await Task.Delay(30 + i % 7 * 40);
        }
        await Task.Delay(700);

        Tvrdi(lazniKrajevi == 0 && p.Stanje == ZvukStanje.Svira && p.Tekuca != null,
            $"{lazniKrajevi} lažnih krajeva od {Puta}, stanje {p.Stanje}");

        double pre = p.Pozicija;
        p.Pauza();
        await Task.Delay(300);
        p.Nastavi();
        await Task.Delay(300);
        Tvrdi(p.Pozicija >= pre + 0.2, $"posle pauze pozicija {p.Pozicija:F2}, pre {pre:F2}");
        p.Zaustavi();
    }

    /// <summary>
    /// Opening a song from a hard disk takes a moment. Pause pressed in that moment must hold: the song
    /// that arrives waits at its start instead of playing over the pause.
    /// </summary>
    public static async Task PauzaTokomUcitavanja()
    {
        var f = NoviFolder();
        var a = new ZvukStavka(NapisiWav(f, "a.wav", 440, 0, Frekvencija * 8, jacina: 0));
        var b = new ZvukStavka(NapisiWav(f, "b.wav", 330, 0, Frekvencija * 8, jacina: 0));

        using var p = new ZvukPlejer();
        Tvrdi(p.Pokreni(0), "BASS nije pokrenut");
        await p.PustiAsync(a);
        await Task.Delay(300);

        var ucitavanje = p.PustiAsync(b);
        p.Pauza();
        await ucitavanje;

        Tvrdi(p.Stanje == ZvukStanje.Pauza, $"pauza je poništena, stanje {p.Stanje}");
        Tvrdi(p.Tekuca == b, "nova pesma nije postavljena");
        await Task.Delay(300);
        Tvrdi(p.Pozicija < 0.1, $"pesma ide tokom pauze: {p.Pozicija:F2}");

        p.Nastavi();
        await Task.Delay(400);
        Tvrdi(p.Stanje == ZvukStanje.Svira && p.Pozicija > 0.2, $"nastavak: {p.Stanje}, {p.Pozicija:F2}");
        p.Zaustavi();
    }

    /// <summary>Runs on the real sound cards, so everything here plays digital silence.</summary>
    public static async Task PraviUredjaji()
    {
        var f = NoviFolder();
        var tiho = NapisiWav(f, "tiho.wav", 440, 0, Frekvencija * 6, jacina: 0);

        using var p = new ZvukPlejer();
        Tvrdi(p.Pokreni(-1), "pravi izlaz nije pokrenut");
        var uredjaji = ZvukPlejer.Uredjaji();
        Console.WriteLine($"      uređaji: {string.Join(" | ", uredjaji.Select(u => $"{u.Broj}:{u.Naziv}{(u.Podrazumevani ? " (podrazumevani)" : "")}"))}");

        Tvrdi(await p.PustiAsync(new ZvukStavka(tiho)), "pravi izlaz: nije pušteno");
        await Task.Delay(700);
        Tvrdi(p.Pozicija > 0.3, $"pravi izlaz: pozicija {p.Pozicija:F2}");

        var stvarni = uredjaji.Where(u => u.Broj > 1 || u.Naziv != "Default").ToList();
        if (stvarni.Count < 2)
        {
            Console.WriteLine("      samo jedan izlaz, preslušavanje i promena izlaza nisu probani");
            p.Zaustavi();
            return;
        }

        var drugi = stvarni.First(u => !u.Podrazumevani);
        using var cue = new Preslusavanje();
        Tvrdi(cue.Pusti(tiho, drugi.Broj), $"preslušavanje na {drugi.Naziv}");
        double glavni = p.Pozicija;
        await Task.Delay(800);
        Tvrdi(cue.Svira && cue.Pozicija > 0.3, $"preslušavanje stoji: {cue.Pozicija:F2}");
        Tvrdi(p.Pozicija > glavni + 0.4, "glavni izlaz je stao zbog preslušavanja");
        cue.Zaustavi();

        Tvrdi(p.PromeniUredjaj(drugi.Broj), $"prebacivanje izlaza: {Bass.LastError}");
        double pre = p.Pozicija;
        await Task.Delay(600);
        Tvrdi(p.Pozicija > pre + 0.3, "posle promene izlaza pozicija stoji");
        p.Zaustavi();
    }

    /// <summary>Real file check: Testovi --mp3 file. Encoder padding trimmed means the length is not whole frames.</summary>
    public static int ProveriMp3(string putanja)
    {
        using var p = new ZvukPlejer(dekodiranje: true);
        Tvrdi(p.Pokreni(0), "BASS nije pokrenut");
        int h = Bass.CreateStream(putanja, 0, 0, BassFlags.Decode | BassFlags.Float | BassFlags.Prescan);
        Tvrdi(h != 0, $"ne otvara se: {Bass.LastError}");
        var info = Bass.ChannelGetInfo(h);
        long bajtovi = Bass.ChannelGetLength(h);
        long sempala = bajtovi / 4 / info.Channels;
        Console.WriteLine($"{Path.GetFileName(putanja)}: {info.Frequency} Hz, {sempala} sempala, {sempala % 1152} van celih MP3 okvira, {Bass.ChannelBytes2Seconds(h, bajtovi):F3} s");
        Bass.StreamFree(h);
        return 0;
    }

    /// <summary>Day planning trusts the length from the tags. This compares it to what the decoder really plays.</summary>
    public static int UporediTrajanja(IEnumerable<string> folderi)
    {
        using var p = new ZvukPlejer(dekodiranje: true);
        Tvrdi(p.Pokreni(0), "BASS nije pokrenut");
        var razlike = new List<(double Razlika, string Fajl)>();
        foreach (var folder in folderi)
            foreach (var putanja in Directory.EnumerateFiles(folder, "*.mp3", SearchOption.AllDirectories))
            {
                int h = Bass.CreateStream(putanja, 0, 0, BassFlags.Decode | BassFlags.Float | BassFlags.Prescan);
                if (h == 0)
                {
                    Console.WriteLine($"ne otvara se: {putanja}: {Bass.LastError}");
                    continue;
                }
                double stvarno = Bass.ChannelBytes2Seconds(h, Bass.ChannelGetLength(h));
                Bass.StreamFree(h);
                razlike.Add((Podaci.IzvorDana.Procitaj(putanja).Trajanje - stvarno, putanja));
            }

        var redom = razlike.OrderBy(r => r.Razlika).ToList();
        Console.WriteLine($"fajlova {redom.Count}; razlika tag - stvarno: prosek {redom.Average(r => r.Razlika):0.000} s, " +
                          $"najmanja {redom[0].Razlika:0.000}, najveća {redom[^1].Razlika:0.000}, zbir {redom.Sum(r => r.Razlika):0.0} s");
        foreach (var r in redom.OrderByDescending(r => Math.Abs(r.Razlika)).Take(5))
            Console.WriteLine($"  {r.Razlika:+0.000;-0.000} {Path.GetFileName(r.Fajl)}");
        return 0;
    }

    public static async Task KrajIGreska()
    {
        var f = NoviFolder();
        var a = NapisiWav(f, "a.wav", 440, 0, Frekvencija);
        var los = Path.Combine(f, "los.mp3");
        File.WriteAllText(los, "ovo nije zvuk");

        using var p = new ZvukPlejer();
        Tvrdi(p.Pokreni(0), "BASS nije pokrenut");

        var greska = new TaskCompletionSource<ZvukStavka>();
        p.NijeOtvoreno += (s, _) => greska.TrySetResult(s);
        Tvrdi(!await p.PustiAsync(new ZvukStavka(los)), "pogrešan fajl je 'pušten'");
        Tvrdi((await greska.Task).Putanja == los, "greška nije javljena");

        Tvrdi(await p.PustiAsync(new ZvukStavka(a)), $"ispravan fajl nije pušten: {File.ReadAllText(Bronxamp.Putanje.Greske)}");
        p.Pauza();
        Tvrdi(p.Stanje == ZvukStanje.Pauza, "pauza");
        p.Nastavi();
        Tvrdi(p.Stanje == ZvukStanje.Svira, "nastavak");
        p.Zaustavi();
        Tvrdi(p.Stanje == ZvukStanje.Stoji, "zaustavljanje");
    }
}
