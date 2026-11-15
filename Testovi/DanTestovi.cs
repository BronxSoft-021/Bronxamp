using Bronxamp.Podaci;
using static Bronxamp.Testovi.Spisak;

namespace Bronxamp.Testovi;

public static class DanTestovi
{
    private static readonly DateTime Dan = new(2026, 11, 5);

    private static List<FajlDana> Fajlovi(string ime, int koliko, int od, int doo, int seme)
    {
        var r = new Random(seme);
        return Enumerable.Range(0, koliko)
            .Select(i => new FajlDana($@"X:\{ime}\{i}.mp3", ime, $"{ime} {i}", r.Next(od, doo)))
            .ToList();
    }

    private static UlazDana Ulaz(int pesama = 260, IReadOnlyList<EmisijaDana>? emisije = null, double prelaz = 0) => new()
    {
        Pocetak = Dan.AddHours(9).AddSeconds(-15),
        Kraj = Dan.AddDays(1),
        TerminiHita = new[] { 11, 13, 15, 17, 19, 21, 23 }.Select(h => Dan.AddHours(h)).ToList(),
        Spica = new FajlDana(@"X:\spica.mp3", "", "Špica", 15),
        PocetniJinglHita = new FajlDana(@"X:\pocetni hit.mp3", "", "Hit dana", 8),
        PocetniJingl = new FajlDana(@"X:\pocetni.mp3", "", "Prvi", 10),
        HitDana = new FajlDana(@"X:\hit.mp3", "Izvođač", "Hit", 200),
        JingloviHita = Fajlovi("JinglHit", 9, 6, 15, 1),
        Jinglovi = Fajlovi("Jingl", 200, 5, 80, 2),
        Pesme = Fajlovi("Pesma", pesama, 150, 330, 3),
        Emisije = emisije ?? [],
        Prelaz = prelaz,
    };

    public static Task CeoDan()
    {
        foreach (double prelaz in new[] { 0.0, 3.0 })
        {
            var dan = SlaganjeDana.Slozi(Ulaz(prelaz: prelaz), 7);
            var s = dan.Stavke;

            Tvrdi(s[0].Vrsta == VrstaStavke.Spica && s[0].Pocetak == Dan.AddHours(9).AddSeconds(-15), "dan počinje špicom u 08:59:45");
            Tvrdi(s[1].Vrsta == VrstaStavke.JinglHit && s[2].Vrsta == VrstaStavke.HitDana && s[3].Vrsta == VrstaStavke.Jingl, "početni blok");
            Tvrdi(s[1].Fajl.Naslov == "Hit dana" && s[3].Fajl.Naslov == "Prvi", "početak dana je izbor korisnika, ne slučajan");
            Tvrdi(s.Count(x => x.Vrsta == VrstaStavke.HitDana) == 8, "hit dana ide na početku i u sedam termina");

            var zadate = s.Where(x => x.Zadato != null && x.Vrsta != VrstaStavke.Spica).ToList();
            Tvrdi(zadate.Count == 7, $"termina ima {zadate.Count}");
            Tvrdi(zadate[0].Zadato == Dan.AddHours(11).AddSeconds(-15), $"blok hita treba da krene u 10:59:45, a ne u {zadate[0].Zadato:HH:mm:ss}");
            double najgore = zadate.Max(x => Math.Abs((x.Pocetak - x.Zadato!.Value).TotalSeconds));
            Console.WriteLine($"      prelaz {prelaz}: {s.Count} stavki, najveće odstupanje {najgore:0.0} s, kraj {s[^1].Pocetak.AddSeconds(s[^1].Fajl.Trajanje):HH:mm:ss}, ostalo {dan.Neiskorisceno}, problemi {dan.Problemi.Count}");

            Tvrdi(najgore <= 5, $"hit kasni {najgore:0.0} s");

            for (int i = 0; i < zadate.Count; i++)
            {
                int j = s.IndexOf(zadate[i]);
                Tvrdi(s[j].Vrsta == VrstaStavke.Jingl && s[j + 1].Vrsta == VrstaStavke.JinglHit && s[j + 2].Vrsta == VrstaStavke.HitDana && s[j + 3].Vrsta == VrstaStavke.Jingl,
                    "blok hita: jingl, jingl za hit, hit, jingl");
            }

            // A jingle every three songs, and none of them twice in a day.
            var jinglovi = s.Where(x => x.Vrsta is VrstaStavke.Jingl or VrstaStavke.JinglHit).Select(x => x.Fajl.Putanja).ToList();
            Tvrdi(jinglovi.Distinct().Count() == jinglovi.Count, "jingl se ponovio u toku dana");
            int pesama = s.Count(x => x.Vrsta == VrstaStavke.Pesma);
            double naJingl = pesama / (double)s.Count(x => x.Vrsta == VrstaStavke.Jingl);
            Console.WriteLine($"      jingl na {naJingl:0.00} pesama");
            Tvrdi(naJingl is > 2 and <= 3.1, $"jingl na {naJingl:0.0} pesama");
            int zaredom = 0, najvise = 0;
            for (int i = 1; i < s.Count; i++)
            {
                Tvrdi(s[i].Fajl.Putanja != s[i - 1].Fajl.Putanja, "isti fajl dva puta zaredom");
                zaredom = s[i].Vrsta == VrstaStavke.Pesma ? zaredom + 1 : 0;
                najvise = Math.Max(najvise, zaredom);
            }
            Tvrdi(najvise <= 3, $"{najvise} pesama bez jingla");
            Tvrdi(s.Select(x => x.Fajl.Putanja).Where(p => p.Contains("Pesma")).Distinct().Count() == pesama, "ista pesma dva puta");

            // Never a jingle the rhythm did not call for: outside the hit block, no two in a row.
            for (int i = 1; i < s.Count; i++)
                if (s[i].Vrsta == VrstaStavke.Jingl && s[i - 1].Vrsta == VrstaStavke.Jingl)
                    Tvrdi(false, $"dva jingla zaredom u {s[i].Pocetak:HH:mm:ss}");
            Tvrdi(dan.Problemi.Count == 0, string.Join("; ", dan.Problemi));
        }
        return Task.CompletedTask;
    }

    /// <summary>The way the people really work: playlists with a start time, one finished show, and blocks too long for their slot.</summary>
    public static Task Blokovi()
    {
        var dugi = new EmisijaDana("Dugi", new TimeSpan(9, 5, 0), Fajlovi("Dugi", 110, 150, 330, 4), null, true);
        var preveliki = new EmisijaDana("Preveliki", new TimeSpan(17, 5, 0), Fajlovi("Preveliki", 40, 150, 330, 5), null, true);
        var fajl = new EmisijaDana("Gotov", new TimeSpan(19, 5, 0), [new FajlDana(@"X:\gotov.mp3", "", "Gotov", 3600)], null);
        var kraj = new EmisijaDana("Kraj", new TimeSpan(20, 5, 0), Fajlovi("Kraj", 80, 150, 330, 6), new FajlDana(@"X:\jk.mp3", "", "Jingl bloka", 10), true);

        var ulaz = Ulaz(pesama: 0, emisije: [dugi, preveliki, fajl, kraj], prelaz: 3);
        var dan = SlaganjeDana.Slozi(ulaz, 7);
        var s = dan.Stavke;
        foreach (var p in dan.Problemi) Console.WriteLine("      " + p);

        Tvrdi(s.Count(x => x.Vrsta == VrstaStavke.Emisija) == 1, "samo gotov fajl je netaknuta emisija");
        Tvrdi(s.Count(x => x.Vrsta == VrstaStavke.HitDana) == 8, $"hitova {s.Count(x => x.Vrsta == VrstaStavke.HitDana)}");

        foreach (int sat in new[] { 11, 13, 15, 17, 21, 23 })
        {
            var termin = Dan.AddHours(sat).AddSeconds(-15);
            var blok = s.FirstOrDefault(x => x.Zadato == termin);
            Tvrdi(blok != null, $"nema hita u {termin:HH:mm:ss}");
            double odstupanje = Math.Abs((blok!.Pocetak - termin).TotalSeconds);
            Tvrdi(odstupanje <= 5, $"hit u {termin:HH:mm:ss} odstupa {odstupanje:0} s");
        }

        var pocetakBloka = s.First(x => x.Zadato == Dan.AddHours(17).AddMinutes(5));
        Tvrdi(pocetakBloka.Fajl.Izvodjac == "Preveliki", "blok ne kreće u svoje vreme");

        // Right after a hit the block just follows it, with nothing added in between.
        int b = s.IndexOf(pocetakBloka);
        Tvrdi(pocetakBloka.Okvirno && s[b - 2].Vrsta == VrstaStavke.HitDana && s[b - 1].Vrsta == VrstaStavke.Jingl, "blok treba da krene odmah posle hita");
        Tvrdi(s.First(x => x.Zadato == Dan.AddHours(20).AddMinutes(5)).Vrsta == VrstaStavke.JinglEmisije, "jingl bloka ide prvi");

        int zaredom = 0, najvise = 0;
        foreach (var x in s)
        {
            zaredom = x.Vrsta == VrstaStavke.Pesma ? zaredom + 1 : 0;
            najvise = Math.Max(najvise, zaredom);
        }
        Tvrdi(najvise <= 3, $"{najvise} pesama bez jingla");
        var jinglovi = s.Where(x => x.Vrsta is VrstaStavke.Jingl or VrstaStavke.JinglHit).Select(x => x.Fajl.Putanja).ToList();
        Tvrdi(jinglovi.Distinct().Count() == jinglovi.Count, "jingl se ponovio");
        Tvrdi(dan.Napomene.Any(p => p.Contains("Preveliki") && p.Contains("izostavljeno")), "izostavljene pesme moraju da se navedu");
        Tvrdi(!dan.Problemi.Any(p => p.Contains("izostavljeno")), "višak u bloku nije greška, semafor ostaje zelen");
        Tvrdi(dan.Izostavljeno.TryGetValue("Preveliki", out var ostale) && ostale.Count > 0, "izostavljene pesme po imenu bloka");

        // The last songs may run a little past midnight, but not on and on.
        var kraj2 = s[^1].Pocetak.AddSeconds(s[^1].Fajl.Trajanje);
        Console.WriteLine($"      kraj dana {kraj2:HH:mm:ss}");
        Tvrdi(kraj2 >= Dan.AddDays(1) && kraj2 <= Dan.AddDays(1).AddMinutes(21), $"kraj {kraj2:HH:mm:ss}");

        // A block in one piece: nothing inside it, and the hit waits for its end.
        var komad = new EmisijaDana("Komad", new TimeSpan(10, 0, 0), Fajlovi("Komad", 30, 200, 240, 8), null);
        var uKomadu = SlaganjeDana.Slozi(Ulaz(emisije: [komad]), 7);
        int i = uKomadu.Stavke.FindIndex(x => x.Vrsta == VrstaStavke.Emisija);
        Tvrdi(uKomadu.Stavke.Skip(i).Take(30).All(x => x.Vrsta == VrstaStavke.Emisija), "bez prekida mora da ostane u komadu");
        return Task.CompletedTask;
    }

    public static Task JingloviSeNePonavljaju()
    {
        var malo = new UlazDana
        {
            Pocetak = Ulaz().Pocetak,
            Kraj = Ulaz().Kraj,
            TerminiHita = Ulaz().TerminiHita,
            Spica = Ulaz().Spica,
            HitDana = Ulaz().HitDana,
            JingloviHita = Fajlovi("JinglHit", 3, 6, 15, 1),
            Jinglovi = Fajlovi("Jingl", 12, 5, 40, 2),
            Pesme = Ulaz().Pesme,
        };
        var dan = SlaganjeDana.Slozi(malo, 7);
        var jinglovi = dan.Stavke.Where(x => x.Vrsta is VrstaStavke.Jingl or VrstaStavke.JinglHit).Select(x => x.Fajl.Putanja).ToList();
        Tvrdi(jinglovi.Distinct().Count() == jinglovi.Count, "ponavljanje kad jinglova ima malo");
        Tvrdi(dan.Problemi.Any(p => p.StartsWith("Ponestalo je jinglova")), "manjak jinglova mora da se prijavi");
        Tvrdi(dan.Problemi.Any(p => p.StartsWith("Izaberi jingl za hit")), "neizabran početak mora da se prijavi");
        return Task.CompletedTask;
    }

    public static Task Emisije()
    {
        var emisija = new EmisijaDana("Reggae", new TimeSpan(20, 0, 0), [new FajlDana(@"X:\em.mp3", "", "Reggae", 7200)], new FajlDana(@"X:\je.mp3", "", "Jingl emisije", 12));
        var dan = SlaganjeDana.Slozi(Ulaz(emisije: [emisija]), 7);
        var s = dan.Stavke;

        int i = s.FindIndex(x => x.Vrsta == VrstaStavke.JinglEmisije);
        Tvrdi(i > 0 && s[i + 1].Vrsta == VrstaStavke.Emisija, "jingl emisije ide pre emisije");
        Tvrdi(s[i].Okvirno && Math.Abs((s[i].Pocetak - Dan.AddHours(20)).TotalSeconds) <= 30, $"emisija kreće u {s[i].Pocetak:HH:mm:ss}");

        // The show runs across the 21h slot: the hit goes right after it, and the problem is reported.
        Tvrdi(s[i + 2].Vrsta == VrstaStavke.Jingl && s[i + 3].Vrsta == VrstaStavke.JinglHit && s[i + 4].Vrsta == VrstaStavke.HitDana, "hit ide odmah posle emisije");
        Tvrdi(dan.Problemi.Any(p => p.Contains("21:00")), "preklapanje mora da se prijavi");
        Tvrdi(s.Count(x => x.Vrsta == VrstaStavke.HitDana) == 8, "nijedan hit nije izgubljen");

        var kratka = SlaganjeDana.Slozi(Ulaz(pesama: 40), 7);
        Tvrdi(kratka.Problemi.Any(p => p.StartsWith("Fali pesama")), "premalo pesama mora da se prijavi");

        var druga = new EmisijaDana("Druga", new TimeSpan(20, 30, 0), [new FajlDana(@"X:\d.mp3", "", "Druga", 600)], null);
        Tvrdi(SlaganjeDana.Slozi(Ulaz(emisije: [emisija, druga]), 7).Problemi.Any(p => p.Contains("preklapa")), "dve emisije u isto vreme");
        return Task.CompletedTask;
    }

    /// <summary>A whole day on a fake clock, with the real files running a little longer or shorter than the tags said.</summary>
    public static Task TokCelogDana()
    {
        // Files that run a second off the tags keep the day on time. A song a whole minute off can only be
        // absorbed by dropping something, so the limit there is half of the longest song plus that minute.
        foreach (var (seme, smetnje, granica) in new[] { (1, false, 60.0), (2, false, 60.0), (3, true, 230.0), (4, true, 230.0) })
        {
            var dan = SlaganjeDana.Slozi(Ulaz(prelaz: 3), seme);
            var s = dan.Stavke;
            var r = new Random(seme);
            var sat = s[0].Pocetak;
            double najgore = 0;
            int preskoceno = 0;

            for (int i = 0; i < s.Count; i++)
            {
                if (s[i].Zadato is { } z && s[i].Vrsta != VrstaStavke.Spica)
                    najgore = Math.Max(najgore, Math.Abs((sat - z).TotalSeconds));

                double stvarno = s[i].Fajl.Trajanje + (r.NextDouble() - 0.5) * 2 + (smetnje && r.Next(40) == 0 ? 60 : 0);
                var kraj = sat.AddSeconds(stvarno);
                var izbaci = TokDana.ZaPreskakanje(s, i, kraj, 3, 5);
                foreach (int k in izbaci) s.RemoveAt(k);
                preskoceno += izbaci.Count;
                sat = i + 1 < s.Count ? kraj.AddSeconds(-TokDana.Preklop(s[i].Fajl.Trajanje, s[i + 1].Fajl.Trajanje, 3)) : kraj;
            }

            Console.WriteLine($"      seme {seme}, smetnje {smetnje}: najveće odstupanje {najgore:0} s, preskočeno {preskoceno}");
            Tvrdi(s.Count(x => x.Vrsta == VrstaStavke.HitDana) == 8, "hit je preskočen");
            Tvrdi(najgore < granica, $"odstupanje {najgore:0} s");
        }
        return Task.CompletedTask;
    }

    public static Task NastavakPoSatu()
    {
        var dan = SlaganjeDana.Slozi(Ulaz(), 7);
        var s = dan.Stavke;
        var cilj = s[40];

        var nadjeno = TokDana.ZaVreme(s, cilj.Pocetak.AddSeconds(12));
        Tvrdi(nadjeno is { Indeks: 40 } && Math.Abs(nadjeno.Value.Pomak - 12) < 0.01, $"posle pada: {nadjeno}");
        Tvrdi(TokDana.ZaVreme(s, Dan.AddHours(8)) == null, "pre početka nema šta da svira");
        Tvrdi(TokDana.ZaVreme(s, Dan.AddDays(1).AddHours(2)) == null, "posle kraja nema šta da svira");

        var folder = Path.Combine(Radni, "dani");
        dan.Pokrenut = true;
        dan.Sacuvaj(folder);
        var ucitan = SlozenDan.Ucitaj(folder, Dan);
        Tvrdi(ucitan is { Pokrenut: true } && ucitan.Stavke.Count == s.Count && ucitan.Stavke[40].Pocetak == cilj.Pocetak, "dan nije sačuvan kako treba");
        return Task.CompletedTask;
    }

    public static Task LozinkaSifrovana()
    {
        var sifrovana = Sistem.Zastita.Sifruj("proba lozinke");
        Tvrdi(!sifrovana.Contains("proba") && Sistem.Zastita.Desifruj(sifrovana) == "proba lozinke", "lozinka");
        Tvrdi(Sistem.Zastita.Desifruj("nije sifrovano") == "" && Sistem.Zastita.Sifruj("") == "", "prazna ili pokvarena lozinka");
        return Task.CompletedTask;
    }

    /// <summary>The time typed into a folder's name, and the day cut into lines by the hours.</summary>
    public static Task VremeIzImenaIDanPoSatima()
    {
        Tvrdi(IzvorDana.VremeIzImena("20.00 Koncertacija") is { Naziv: "Koncertacija" } a && a.Vreme == new TimeSpan(20, 0, 0), "20.00");
        Tvrdi(IzvorDana.VremeIzImena("9:05 Alternative") is { Naziv: "Alternative" } b && b.Vreme == new TimeSpan(9, 5, 0), "9:05");
        Tvrdi(IzvorDana.VremeIzImena("23h Laku noć") is { Naziv: "Laku noć" } c && c.Vreme == new TimeSpan(23, 0, 0), "23h");
        Tvrdi(IzvorDana.VremeIzImena("15.30h Domaće") is { Naziv: "Domaće" } d4 && d4.Vreme == new TimeSpan(15, 30, 0), "15.30h");
        foreach (var ime in new[] { "01. Hank - Tell The Leaves Not To Turn", "Vol.1", "Volume 14", "10 - Deset", "25.00 Ne postoji", "2026 Album", "Metronom 273" })
            Tvrdi(IzvorDana.VremeIzImena(ime) == null, $"nije vreme: {ime}");
        Tvrdi(IzvorDana.DatumFoldera("14-11-2026 20.00 Koncertacija") is { Naziv: "20.00 Koncertacija" }, "datum pa vreme");

        // What is typed into the time box: a whole time only, never a lone digit (TimeSpan would take "2" as two days).
        Tvrdi(Vm.DanStrana.ProcitajVreme("19:05") == new TimeSpan(19, 5, 0), "19:05");
        Tvrdi(Vm.DanStrana.ProcitajVreme("9.30") == new TimeSpan(9, 30, 0), "9.30");
        Tvrdi(Vm.DanStrana.ProcitajVreme("20h") == new TimeSpan(20, 0, 0), "20h");
        foreach (var t in new[] { "", "2", "19", "19:", "19:0", "24:00", "19:60", "abc" })
            Tvrdi(Vm.DanStrana.ProcitajVreme(t) == null, $"nije vreme: „{t}“");

        var alternative = new EmisijaDana("Alternative", new TimeSpan(9, 5, 0), Fajlovi("Alternative", 60, 150, 330, 4), null, true);
        var emisija = new EmisijaDana("Koncertacija", new TimeSpan(20, 0, 0), [new FajlDana(@"X:\koncertacija.mp3", "", "Koncertacija", 3500)], null);
        var dan = SlaganjeDana.Slozi(Ulaz(pesama: 0, emisije: [alternative, emisija]), 7);
        var s = dan.Stavke;
        var delovi = PodelaDana.Podeli(s);
        foreach (var d in delovi) Console.WriteLine($"      {s[d.Od].Pocetak:HH:mm:ss}  {d.Vrsta,-8} {d.Naziv}{(d.Nastavak ? ", nastavak" : "")}  ({d.Do - d.Od})");

        Tvrdi(delovi[0].Od == 0 && delovi[^1].Do == s.Count, "delovi pokrivaju ceo dan");
        for (int i = 1; i < delovi.Count; i++) Tvrdi(delovi[i].Od == delovi[i - 1].Do, "delovi idu jedan za drugim");
        Tvrdi(delovi[0].Vrsta == VrstaDela.Pocetak, "dan počinje početkom");
        Tvrdi(delovi.Count(d => d.Vrsta == VrstaDela.Hit) == s.Count(x => x.Vrsta == VrstaStavke.HitDana) - 1, "svaki hit posle početka je svoj red");
        foreach (var d in delovi.Where(d => d.Vrsta is VrstaDela.Hit or VrstaDela.Pocetak))
            Tvrdi(s.Skip(d.Od).Take(d.Do - d.Od).All(x => x.Vrsta != VrstaStavke.Pesma), "u redu hita nema pesama");
        Tvrdi(delovi.Count(d => d.Naziv == "Alternative" && !d.Nastavak) == 1, "blok se jednom zove po imenu");
        Tvrdi(delovi.Any(d => d.Naziv == "Alternative" && d.Nastavak), "posle hita blok ide kao nastavak");
        Tvrdi(delovi.Single(d => d.Vrsta == VrstaDela.Emisija).Naziv == "Koncertacija", "emisija je svoj red");
        return Task.CompletedTask;
    }

    public static Task ImenaIFolderi()
    {
        Tvrdi(IzvorDana.PredlogZa("Jingle - Pocetak programa.mp3") == VrstaJingla.Spica, "špica");
        Tvrdi(IzvorDana.PredlogZa("Jingle - Hit dana Jelena.mp3") == VrstaJingla.HitDana, "hit dana");
        Tvrdi(IzvorDana.PredlogZa("Jingle - Hit of the Day - 80s.mp3") == VrstaJingla.HitDana, "hit of the day");
        Tvrdi(IzvorDana.PredlogZa("Commercial - Gluvo doba.mp3") == VrstaJingla.Retko, "commercial");
        Tvrdi(IzvorDana.PredlogZa("Reklame - Biserka.mp3") == VrstaJingla.Retko, "reklame");
        Tvrdi(IzvorDana.PredlogZa("Ekološki trenutak - Jingle 01.mp3") == VrstaJingla.Retko, "ekološki trenutak");
        Tvrdi(IzvorDana.PredlogZa("lakunoc.mp3") == VrstaJingla.Retko, "laku noć");
        Tvrdi(IzvorDana.PredlogZa("Jingle - Etnopoganija - špica.mp3") == VrstaJingla.Emisija, "jingl emisije");
        Tvrdi(IzvorDana.PredlogZa("Jingle - Preslusavanje - jingle.mp3") == VrstaJingla.Emisija, "preslušavanje");
        Tvrdi(IzvorDana.PredlogZa("Jingle - Bolje Se Zivi.mp3") == VrstaJingla.Opsti, "običan jingl");

        Tvrdi(IzvorDana.DatumFoldera("05-11-2026 Reggae Tunes October 2026") is { Naziv: "Reggae Tunes October 2026" } d && d.Datum == Dan, "folder sa datumom");
        Tvrdi(IzvorDana.DatumFoldera("Focus 054") == null, "folder bez datuma");

        var folder = Path.Combine(Radni, "redosled");
        Directory.CreateDirectory(Path.Combine(folder, "Obrisane"));
        foreach (var ime in new[] { "10 - Deset.mp3", "2.Dva.mp3", "01 - Jedan.mp3", "beleska.txt", "Bez broja.mp3" })
            File.WriteAllBytes(Path.Combine(folder, ime), []);
        File.WriteAllBytes(Path.Combine(folder, "Obrisane", "stari.mp3"), []);
        var redom = IzvorDana.Fajlovi(folder).Select(Path.GetFileName).ToList();
        Tvrdi(string.Join("|", redom) == "01 - Jedan.mp3|2.Dva.mp3|10 - Deset.mp3|Bez broja.mp3", string.Join("|", redom));
        Tvrdi(IzvorDana.FajloviJinglova(folder).Count == 4, "podfolderi jinglova se ne čitaju");
        return Task.CompletedTask;
    }
}
