using Bronxamp.Podaci;
using static Bronxamp.Testovi.Spisak;

namespace Bronxamp.Testovi;

public static class BibliotekaTestovi
{
    private static Pesma Napravi(string naslov, string izvodjac, string album, int godina, string zanr = "")
    {
        var a = new Album { Naziv = album, Izvodjac = izvodjac, Godina = godina };
        var p = new Pesma { Naslov = naslov, Izvodjac = izvodjac, Godina = godina, Zanr = zanr, Album = a };
        p.ZaPretragu = Pretraga.TekstZaPretragu(p, a);
        return p;
    }

    public static Task PretragaUpiti()
    {
        var mladost = Napravi("Mladost", "Električni Orgazam", "Lišće prekriva Lisabon", 1982, "Rock");
        var ekv = Napravi("Oči boje meda", "Ekatarina Velika", "S vetrom uz lice", 1986, "New Wave");
        var ekv83 = Napravi("Aut", "Ekatarina Velika", "Ekatarina Velika", 1983, "New Wave");
        var metallica = Napravi("One", "Metallica", "...And Justice for All", 1988, "Metal");
        var metal90 = Napravi("Enter Sandman", "Metallica", "Metallica", 1991, "Metal");
        var djordje = Napravi("Šta da se radi", "Đorđe Balašević", "Bezdan", 1986, "Pop");
        var cirilica = Napravi("Плави воз", "Рибља Чорба", "Буђење", 1984, "Рок");
        var sve = new[] { mladost, ekv, ekv83, metallica, metal90, djordje, cirilica };

        void Proveri(string upit, params Pesma[] ocekivano)
        {
            var q = new Pretraga(upit);
            var nadjeno = sve.Where(q.Odgovara).ToArray();
            Tvrdi(nadjeno.SequenceEqual(ocekivano),
                $"\"{upit}\" našao: {string.Join(", ", nadjeno.Select(p => p.Naslov))}");
        }

        Proveri("ekv 1983", ekv83);
        Proveri("ekv", ekv, ekv83);
        Proveri("metal 90-e", metal90);
        Proveri("metal 80-e", metallica);
        Proveri("metal 90e", metal90);
        Proveri("lisce", mladost);
        Proveri("LIŠĆE lisabon", mladost);
        Proveri("elektricni 1982", mladost);
        Proveri("djordje", djordje);
        Proveri("dorde", djordje);
        Proveri("balasevic sta", djordje);
        Proveri("plavi voz", cirilica);
        Proveri("ribja", []);
        Proveri("riblja corba", cirilica);
        Proveri("nema toga", []);
        Tvrdi(new Pretraga("  ").Prazna, "prazan upit");
        return Task.CompletedTask;
    }

    /// <summary>Copies a real WAV shell into a tree that mimics a user's folders.</summary>
    private static string NapraviWav(string putanja, int sekundi = 1)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(putanja)!);
        int bajtova = 44100 * 4 * sekundi;
        using var w = new BinaryWriter(File.Create(putanja));
        w.Write("RIFF"u8.ToArray()); w.Write(36 + bajtova); w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)2);
        w.Write(44100); w.Write(44100 * 4); w.Write((short)4); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(bajtova); w.Write(new byte[bajtova]);
        return putanja;
    }

    private static void Tagiraj(string putanja, string naslov, string izvodjac, string album, uint broj, uint godina = 0, string? albumIzvodjac = null)
    {
        using var f = TagLib.File.Create(putanja);
        f.Tag.Title = naslov;
        f.Tag.Performers = [izvodjac];
        f.Tag.Album = album;
        f.Tag.Track = broj;
        f.Tag.Year = godina;
        if (albumIzvodjac != null) f.Tag.AlbumArtists = [albumIzvodjac];
        f.Save();
    }

    public static async Task Skeniranje()
    {
        var koren = NoviFolder();
        using var baza = new Baza(Path.Combine(koren, "t.db"));
        var katalog = new Katalog(baza);
        using var skener = new Skener(baza, katalog);
        var muzika = Path.Combine(koren, "muzika");

        var a1 = NapraviWav(Path.Combine(muzika, "EKV", "01 Aut.wav"));
        var a2 = NapraviWav(Path.Combine(muzika, "EKV", "02 Ljudi iz gradova.wav"), 2);
        Tagiraj(a1, "Aut", "Ekatarina Velika", "Ekatarina Velika", 1, 1983);
        Tagiraj(a2, "Ljudi iz gradova", "Ekatarina Velika", "Ekatarina Velika", 2, 1983);

        // Compilation over two disc folders, no album-artist tag: must stay one album of various artists.
        var k1 = NapraviWav(Path.Combine(muzika, "Paket", "CD1", "01.wav"));
        var k2 = NapraviWav(Path.Combine(muzika, "Paket", "CD1", "02.wav"));
        var k3 = NapraviWav(Path.Combine(muzika, "Paket", "CD2", "01.wav"));
        Tagiraj(k1, "Prva", "Idoli", "Paket aranžman", 1);
        Tagiraj(k2, "Druga", "Šarlo Akrobata", "Paket aranžman", 2);
        Tagiraj(k3, "Treća", "EKV", "Paket aranžman", 1);

        // No tags at all, and a file that is not audio.
        NapraviWav(Path.Combine(muzika, "Bez tagova", "neka pesma.wav"));
        var los = Path.Combine(muzika, "Bez tagova", "los.mp3");
        File.WriteAllText(los, "nije zvuk");
        File.WriteAllText(Path.Combine(muzika, "Bez tagova", "beleske.txt"), "x");

        baza.DodajFolder(muzika);
        await skener.SkenirajAsync();

        Tvrdi(katalog.Pesme.Count == 7, $"pesama {katalog.Pesme.Count}, očekivano 7");
        Tvrdi(katalog.Albumi.Count == 3, $"albuma {katalog.Albumi.Count}: {string.Join(" | ", katalog.Albumi.Select(a => a.Naziv))}");

        var ekv = katalog.Albumi.Single(a => a.Naziv == "Ekatarina Velika");
        Tvrdi(ekv.Izvodjac == "Ekatarina Velika" && ekv.Godina == 1983 && ekv.Pesme.Count == 2, "EKV album");
        Tvrdi(ekv.Pesme[0].Naslov == "Aut" && Math.Abs(ekv.Pesme[1].Trajanje - 2) < 0.1, "redosled i trajanje");

        var paket = katalog.Albumi.Single(a => a.Naziv == "Paket aranžman");
        Tvrdi(paket.Pesme.Count == 3 && paket.Izvodjac == Tagovi.RazniIzvodjaci, $"kompilacija: {paket.Pesme.Count}, {paket.Izvodjac}");

        Tvrdi(katalog.Pesme.Any(p => p.Naslov == "neka pesma"), "pesma bez tagova nosi ime fajla");

        // Unchanged files are not read again.
        long izmenjeno = new FileInfo(a1).LastWriteTimeUtc.Ticks;
        var obradjeno = -1;
        skener.Napredak += n => { if (n.Gotovo) obradjeno = n.Obradjeno; };
        await skener.SkenirajAsync();
        Tvrdi(obradjeno == 0, $"drugo skeniranje pročitalo {obradjeno} fajlova");

        // History survives a move.
        var aut = katalog.Pesme.Single(p => p.Naslov == "Aut");
        baza.ZabeleziPustanje(aut.Id, null, DateTime.Now);
        var novaPutanja = Path.Combine(muzika, "Premesteno", "01 Aut.wav");
        Directory.CreateDirectory(Path.GetDirectoryName(novaPutanja)!);
        File.Move(a1, novaPutanja);
        await skener.SkenirajAsync();
        var premestena = katalog.Pesme.Single(p => p.Naslov == "Aut");
        Tvrdi(premestena.Id == aut.Id && premestena.Putanja == novaPutanja, "premeštena pesma nije zadržala id");

        // Deleted file leaves the library.
        File.Delete(a2);
        await skener.SkenirajAsync();
        Tvrdi(!katalog.Pesme.Any(p => p.Naslov == "Ljudi iz gradova"), "obrisana pesma je ostala");

        // Removing a folder by hand does empty it from the library, and adding it back restores it.
        var drugi = Path.Combine(koren, "drugi");
        NapraviWav(Path.Combine(drugi, "X", "1.wav"));
        baza.DodajFolder(drugi);
        await skener.SkenirajAsync();
        var saDrugim = katalog.Pesme.Count;
        baza.UkloniFolder(drugi);
        katalog.Ucitaj();
        Tvrdi(katalog.Pesme.Count == saDrugim - 1, "uklonjen folder je ostao u biblioteci");

        // A disk that is gone must not wipe the library.
        var koliko = katalog.Pesme.Count;
        Directory.Move(muzika, muzika + "_skinut");
        await skener.SkenirajAsync();
        katalog.Ucitaj();
        Tvrdi(katalog.Pesme.Count == koliko, "nedostupan folder je obrisao pesme");
    }

    public static async Task UklanjanjeAlbuma()
    {
        var koren = NoviFolder();
        using var baza = new Baza(Path.Combine(koren, "t.db"));
        var katalog = new Katalog(baza);
        using var skener = new Skener(baza, katalog);
        var muzika = Path.Combine(koren, "muzika");
        var a = NapraviWav(Path.Combine(muzika, "A", "1.wav"));
        NapraviWav(Path.Combine(muzika, "B", "1.wav"));
        Tagiraj(a, "Prva", "Izvođač", "A", 1);
        baza.DodajFolder(muzika);
        await skener.SkenirajAsync();
        Tvrdi(katalog.Albumi.Count == 2, "dva albuma");

        var album = katalog.Albumi.Single(x => x.Naziv == "A");
        baza.IskljuciAlbum(album.Id);
        katalog.Ucitaj();
        Tvrdi(katalog.Albumi.Count == 1 && baza.BrojIskljucenihPesama() == 1, "album nije uklonjen");
        Tvrdi(File.Exists(a), "fajl na disku mora da ostane");

        await skener.SkenirajAsync();
        katalog.Ucitaj();
        Tvrdi(katalog.Albumi.Count == 1, "skeniranje je vratilo uklonjen album");

        baza.VratiIskljucene();
        katalog.Ucitaj();
        Tvrdi(katalog.Albumi.Count == 2 && baza.BrojIskljucenihPesama() == 0, "album nije vraćen");
    }

    public static async Task UklanjanjePesama()
    {
        var koren = NoviFolder();
        using var baza = new Baza(Path.Combine(koren, "t.db"));
        var katalog = new Katalog(baza);
        using var skener = new Skener(baza, katalog);
        var muzika = Path.Combine(koren, "muzika");
        var fajlovi = Enumerable.Range(1, 4).Select(i => NapraviWav(Path.Combine(muzika, "A", $"{i}.wav"))).ToList();
        baza.DodajFolder(muzika);
        await skener.SkenirajAsync();
        Tvrdi(katalog.Pesme.Count == 4, "četiri pesme");

        var prva = katalog.Pesme[0];
        Tvrdi(prva.Bitrate > 0 && prva.Frekvencija > 0, $"kvalitet nije pročitan: {prva.Bitrate} / {prva.Frekvencija}");

        baza.ZabeleziPustanje(prva.Id, null, DateTime.Now);
        baza.ZabeleziPustanje(prva.Id, null, DateTime.Now);
        katalog.Ucitaj();
        var ponovo = katalog.Pesme.Single(p => p.Id == prva.Id);
        Tvrdi(ponovo.Pustano == 2 && ponovo.PoslednjePustanje != null, "broj puštanja nije učitan");

        baza.IskljuciPesme(katalog.Pesme.Take(2).Select(p => p.Id).ToList());
        katalog.Ucitaj();
        Tvrdi(katalog.Pesme.Count == 2 && baza.BrojIskljucenihPesama() == 2, "pesme nisu uklonjene");
        Tvrdi(fajlovi.All(File.Exists), "fajlovi na disku moraju da ostanu");

        await skener.SkenirajAsync();
        katalog.Ucitaj();
        Tvrdi(katalog.Pesme.Count == 2, "skeniranje je vratilo uklonjene pesme");

        baza.VratiIskljucene();
        katalog.Ucitaj();
        Tvrdi(katalog.Pesme.Count == 4 && baza.BrojIskljucenihPesama() == 0, "pesme nisu vraćene");
    }

    /// <summary>A library from before 0.15.0 has no quality columns, and must not lose anything when it gets them.</summary>
    public static async Task NadogradnjaKvaliteta()
    {
        var koren = NoviFolder();
        var putanja = Path.Combine(koren, "t.db");
        var muzika = Path.Combine(koren, "muzika");
        NapraviWav(Path.Combine(muzika, "A", "1.wav"));
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
            k.CommandText = "ALTER TABLE pesme DROP COLUMN bitrate; ALTER TABLE pesme DROP COLUMN frekvencija;";
            k.ExecuteNonQuery();
        }

        using (var baza = new Baza(putanja))
        {
            var katalog = new Katalog(baza);
            using var skener = new Skener(baza, katalog);
            katalog.Ucitaj();
            Tvrdi(katalog.Pesme[0].Bitrate == 0, "pre skeniranja kvalitet nije poznat");
            await skener.SkenirajAsync();
            katalog.Ucitaj();
            var p = katalog.Pesme.Single();
            Tvrdi(p.Id == id && p.Omiljeno, "nadogradnja je izgubila pesmu ili omiljeno");
            Tvrdi(p.Bitrate > 0, "skeniranje posle nadogradnje nije pročitalo kvalitet");
        }
    }

    public static async Task PojedinacniFajlovi()
    {
        var koren = NoviFolder();
        using var baza = new Baza(Path.Combine(koren, "t.db"));
        var katalog = new Katalog(baza);
        using var skener = new Skener(baza, katalog);
        var muzika = Path.Combine(koren, "muzika");
        NapraviWav(Path.Combine(muzika, "A", "1.wav"));
        baza.DodajFolder(muzika);
        await skener.SkenirajAsync();

        var van = NapraviWav(Path.Combine(koren, "negde drugde", "03 - Pesma.wav"));
        await skener.DodajFajloveAsync([van, Path.Combine(koren, "ne postoji.mp3")]);
        Tvrdi(katalog.Pesme.Count == 2 && katalog.Pesme.Any(p => p.Putanja == van && p.Naslov == "Pesma"), "pesma van biblioteke nije dodata");

        await skener.SkenirajAsync();
        katalog.Ucitaj();
        Tvrdi(katalog.Pesme.Any(p => p.Putanja == van), "skeniranje je izbacilo pojedinačnu pesmu");

        // Adding a removed album again brings it back.
        var pesma = katalog.Pesme.Single(p => p.Putanja == van);
        baza.IskljuciAlbum(pesma.AlbumId);
        katalog.Ucitaj();
        Tvrdi(katalog.Pesme.All(p => p.Putanja != van), "pesma nije uklonjena");
        await skener.DodajFajloveAsync([van]);
        Tvrdi(katalog.Pesme.Any(p => p.Putanja == van), "ponovo dodata pesma se nije vratila");
    }

    public static Task ImenaITagovi()
    {
        Tvrdi(Tagovi.PopraviKodiranje("Ispoèetka") == "Ispočetka", "č");
        Tvrdi(Tagovi.PopraviKodiranje("Ðorðe Balaševiæ") == "Đorđe Balašević", "đ i ć");
        Tvrdi(Tagovi.PopraviKodiranje("\u008Aarlo Akrobata") == "Šarlo Akrobata", "Š");
        Tvrdi(Tagovi.PopraviKodiranje("Íà Ðàéîíå") == "На Районе", "ćirilica u 1251");
        Tvrdi(Tagovi.PopraviKodiranje("Ðèá\u009Aà ×îðáà") == "Рибља Чорба", "srpska ćirilica u 1251");
        Tvrdi(Tagovi.PopraviKodiranje("Été") == "Été", "kratka francuska reč mora da ostane");
        Tvrdi(Tagovi.PopraviKodiranje("Crème brûlée") == "Crème brûlée", "francuski naslov mora da ostane");
        Tvrdi(Tagovi.PopraviKodiranje("Mötley Crüe") == "Mötley Crüe", "nemačka slova moraju da ostanu");

        Tvrdi(Tagovi.IzFoldera("5 Minuta Slave - PMS [Demo] (2018)") == ("5 Minuta Slave", "PMS [Demo]", 2018), "folder sa godinom");
        Tvrdi(Tagovi.IzFoldera("Various Artists - 00s Hits (2022)") == ("", "00s Hits", 2022), "kompilacija");
        Tvrdi(Tagovi.IzFoldera("Neki folder") == ("", "Neki folder", 0), "običan folder");

        Tvrdi(Tagovi.IzImenaFajla("03 - Pesma") == (3, "", "Pesma"), "broj i naslov");
        Tvrdi(Tagovi.IzImenaFajla("03. Pesma") == (3, "", "Pesma"), "broj sa tačkom");
        Tvrdi(Tagovi.IzImenaFajla("07 Bijelo Dugme - Ima neka tajna veza") == (7, "Bijelo Dugme", "Ima neka tajna veza"), "izvođač u imenu");
        Tvrdi(Tagovi.IzImenaFajla("Moby - Porcelain") == (0, "Moby", "Porcelain"), "izvođač bez broja");
        Tvrdi(Tagovi.IzImenaFajla("1984") == (0, "", "1984"), "broj koji je naslov");
        return Task.CompletedTask;
    }

    public static async Task PracenjeFoldera()
    {
        var koren = NoviFolder();
        using var baza = new Baza(Path.Combine(koren, "t.db"));
        var katalog = new Katalog(baza);
        using var skener = new Skener(baza, katalog);
        var muzika = Path.Combine(koren, "muzika");
        NapraviWav(Path.Combine(muzika, "A", "1.wav"));
        baza.DodajFolder(muzika);
        await skener.SkenirajAsync();
        skener.PratiFoldere();

        var dosla = new TaskCompletionSource();
        katalog.Promenjen += () => { if (katalog.Pesme.Count == 2) dosla.TrySetResult(); };
        NapraviWav(Path.Combine(muzika, "B", "2.wav"));

        var gotovo = await Task.WhenAny(dosla.Task, Task.Delay(12000));
        Tvrdi(gotovo == dosla.Task, "novi fajl nije primećen");
    }

    public static async Task<int> PravoSkeniranje(string folder, string putanjaBaze)
    {
        using var baza = new Baza(putanjaBaze);
        var katalog = new Katalog(baza);
        using var skener = new Skener(baza, katalog);
        baza.DodajFolder(folder);

        var sat = System.Diagnostics.Stopwatch.StartNew();
        skener.Napredak += n => { if (n.Obradjeno % 2000 == 0 || n.Gotovo) Console.WriteLine($"{sat.Elapsed.TotalSeconds:F0} s  {n.Obradjeno}/{n.Ukupno}"); };
        await skener.SkenirajAsync();
        Console.WriteLine($"gotovo za {sat.Elapsed.TotalSeconds:F1} s: {katalog.Pesme.Count} pesama, {katalog.Albumi.Count} albuma");

        sat.Restart();
        katalog.Ucitaj();
        Console.WriteLine($"učitavanje iz baze: {sat.ElapsedMilliseconds} ms");

        foreach (var upit in new[] { "metal 90-e", "ekv 1983", "love", "a" })
        {
            sat.Restart();
            var q = new Pretraga(upit);
            int n = katalog.Filtriraj(q).Count;
            int m = katalog.FiltrirajPesme(q).Count;
            Console.WriteLine($"\"{upit}\": {n} albuma, {m} pesama za {sat.ElapsedMilliseconds} ms");
        }

        var uzorak = katalog.Albumi.Take(5).Select(a => $"{a.Izvodjac} - {a.Naziv} ({a.Godina}), {a.Pesme.Count} pesama, {Vreme.Dugo(a.Trajanje)}");
        Console.WriteLine(string.Join(Environment.NewLine, uzorak));
        return 0;
    }

    public static Task Emisije()
    {
        var koren = NoviFolder();
        using var baza = new Baza(Path.Combine(koren, "t.db"));
        var e = baza.NovaEmisija("Gluvo doba", "#6C63FF");
        baza.SacuvajPesmeEmisije(e.Id, [5, 3, 9]);
        Tvrdi(baza.PesmeEmisije(e.Id).SequenceEqual([5L, 3L, 9L]), "redosled pesama emisije");

        var pre3 = DateTime.Now.AddDays(-21);
        var pre20 = DateTime.Now.AddDays(-140);
        baza.ZabeleziPustanje(5, e.Id, pre3);
        baza.ZabeleziPustanje(3, e.Id, pre20);
        baza.ZabeleziPustanje(9, null, DateTime.Now);

        var skoro = baza.PoslednjeUEmisiji(e.Id, DateTime.Now.AddDays(-56));
        Tvrdi(skoro.Count == 1 && skoro.ContainsKey(5), "upozorenje gleda samo poslednjih N nedelja i samo ovu emisiju");
        Tvrdi(baza.IstorijaEmisije(e.Id).Count == 2, "istorija emisije");

        baza.ObrisiEmisiju(e.Id);
        Tvrdi(baza.Emisije().Count == 0 && baza.Najslusanije(DateTime.Now.AddYears(-1)).Count == 3, "brisanje emisije čuva statistiku");
        return Task.CompletedTask;
    }
}
