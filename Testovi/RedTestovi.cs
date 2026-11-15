using Bronxamp.Podaci;
using static Bronxamp.Testovi.Spisak;

namespace Bronxamp.Testovi;

public static class RedTestovi
{
    private static List<Pesma> Pesme(int n, double trajanje = 100)
    {
        var album = new Album { Naziv = "A" };
        return Enumerable.Range(1, n).Select(i => new Pesma { Id = i, Naslov = $"P{i}", Trajanje = trajanje, Album = album }).ToList();
    }

    public static Task Redosled()
    {
        var p = Pesme(4);
        var red = new Red();
        red.Postavi(p, 1, "Test");
        Tvrdi(red.Tekuca == p[1] && red.Sledeca == p[2], "početak od druge");
        Tvrdi(red.Preostale.SequenceEqual([p[2], p[3]]), "preostale");

        Tvrdi(red.Napred() && red.Napred() && red.Tekuca == p[3], "napred do kraja");
        Tvrdi(red.Sledeca == null && !red.Napred(), "bez ponavljanja kraj je kraj");

        red.Ponavljanje = Ponavljanje.Sve;
        Tvrdi(red.Sledeca == p[0], "ponavljanje svih se vraća na početak");
        Tvrdi(red.Napred() && red.Tekuca == p[0], "krug");

        red.Ponavljanje = Ponavljanje.Jedna;
        Tvrdi(red.Sledeca == p[0], "ponavljanje jedne");
        Tvrdi(red.RucnoNapred() && red.Tekuca == p[1], "dugme Sledeća izlazi iz ponavljanja jedne");

        red.Ponavljanje = Ponavljanje.Ne;
        Tvrdi(red.Nazad() && red.Tekuca == p[0] && !red.Nazad(), "nazad");
        Tvrdi(red.SkociNa(1) && red.Tekuca == p[2], "skok na stavku iz panela Sledi");
        return Task.CompletedTask;
    }

    public static Task Mesanje()
    {
        var p = Pesme(50);
        var red = new Red(seme: 7);
        red.Postavi(p, 10, "Test");

        red.Mesanje = true;
        Tvrdi(red.Tekuca == p[10], "mešanje ne menja tekuću");
        var ostale = red.Preostale.ToList();
        Tvrdi(ostale.Count == 49 && !ostale.Contains(p[10]) && ostale.Distinct().Count() == 49, "sve ostale tačno jednom");
        Tvrdi(!ostale.SequenceEqual(p.Where(x => x != p[10])), "stvarno promešano");

        red.Mesanje = false;
        Tvrdi(red.Tekuca == p[10] && red.Sledeca == p[11], "isključeno mešanje vraća redosled");
        return Task.CompletedTask;
    }

    public static Task Dodavanje()
    {
        var p = Pesme(5);
        var red = new Red();
        red.Postavi(p.Take(3), 0, "Test");
        red.DodajSledece([p[4]]);
        Tvrdi(red.Sledeca == p[4], "pusti sledeće");
        red.DodajNaKraj([p[3]]);
        Tvrdi(red.Preostale.Last() == p[3] && red.Ukupno == 5, "dodaj u red");
        red.Izbaci(p[4]);
        Tvrdi(red.Sledeca == p[1] && red.Ukupno == 4, "pesma koja ne može da se otvori ispada");

        // The same song twice in a show must stay two separate entries.
        var dupli = new Red();
        dupli.Postavi([p[0], p[1], p[0]], 0, "Test");
        Tvrdi(dupli.Napred() && dupli.Napred() && dupli.Tekuca == p[0] && dupli.Sledeca == null, "ista pesma dva puta");

        // Editing a show that plays keeps the current song and follows the new order.
        var e = new Emisija { Id = 1, Ime = "E" };
        var red2 = new Red();
        red2.Postavi(p, 1, "E", e);
        red2.OsveziEmisiju([p[4], p[1], p[0]]);
        Tvrdi(red2.Tekuca == p[1] && red2.Sledeca == p[0] && red2.Emisija == e, "izmena emisije dok svira");
        return Task.CompletedTask;
    }

    public static Task Uklanjanje()
    {
        var p = Pesme(6);
        var red = new Red();
        red.Postavi(p, 1, "Test");

        red.UkloniUnapred(1);
        Tvrdi(red.Preostale.SequenceEqual([p[2], p[4], p[5]]) && red.Tekuca == p[1], "uklanjanje iz sredine reda");

        Tvrdi(red.UkloniTekucu() && red.Tekuca == p[2], "posle uklanjanja tekuće dolazi sledeća");
        red.SkociNa(1);
        Tvrdi(red.Tekuca == p[5] && !red.UkloniTekucu() && red.Tekuca == p[4], "uklanjanje poslednje");
        Tvrdi(red.SvePesme.SequenceEqual([p[0], p[2], p[4]]), "sve pesme u redu");

        red.Mesanje = true;
        red.Mesanje = false;
        Tvrdi(red.Ukupno == 3 && !red.SvePesme.Contains(p[3]), "uklonjena se ne vraća posle mešanja");

        red.Isprazni();
        Tvrdi(red.Ukupno == 0 && red.Tekuca == null && !red.UkloniTekucu(), "prazan red");
        return Task.CompletedTask;
    }

    public static Task PodesavanjaIVerzije()
    {
        // The default settings must be writable as they are: a broken Save button shipped once.
        var p = new Podesavanja { Prelaz = 2.5, Slusalice = "Š" };
        p.Sacuvaj();
        var ucitano = Podesavanja.Ucitaj();
        Tvrdi(ucitano.Prelaz == 2.5 && ucitano.Slusalice == "Š" && ucitano.Levo == null, "podešavanja se ne čuvaju");

        p.Levo = 240;
        p.Sacuvaj();
        Tvrdi(Podesavanja.Ucitaj().Levo == 240, "pozicija prozora se ne čuva");

        Tvrdi(Verzije.Sve[0].Broj == Verzije.BrojPrograma, $"Verzije.cs {Verzije.Sve[0].Broj}, program {Verzije.BrojPrograma}");
        var brojevi = Verzije.Sve.Select(v => new Version(v.Broj)).ToList();
        Tvrdi(brojevi.SequenceEqual(brojevi.OrderDescending()), "najnovija verzija mora biti na vrhu");
        Tvrdi(string.Join("+", new Verzija("", "", ["a"], ["b"]).Vrste) == "Novo+Dorada" && new Verzija("", "", [], ["b"]).Vrste.Single() == "Dorada", "vrsta iz spiskova");
        Tvrdi(Verzije.Sve[^1].Broj == "0.1.0", "prva verzija");
        Tvrdi(Verzije.Sve[0].Broj == $"0.{Verzije.Sve.Sum(v => v.Nove.Length)}.{Verzije.Sve.Sum(v => v.Dorade.Length)}", "broj je zbir istorije");
        return Task.CompletedTask;
    }

    public static Task Kraj()
    {
        Tvrdi(KrajEmisije.Preostalo(60, 200, [100, 100], 0) == 260, "bez prelaza: zbir");
        Tvrdi(KrajEmisije.Preostalo(60, 200, [100, 100], 3) == 254, "dva prelaza od 3 s skidaju 6 s");
        Tvrdi(KrajEmisije.Preostalo(60, 200, [], 3) == 60, "poslednja pesma nema prelaz");
        Tvrdi(KrajEmisije.Preostalo(60, 200, [4, 100], 5) == 60 + 4 - 2 + 100 - 2, "prelaz se skraćuje na pola kraće pesme, kao u zvuku");

        Tvrdi(Vreme.BrojPesama(1) == "1 pesma" && Vreme.BrojPesama(3) == "3 pesme" && Vreme.BrojPesama(14) == "14 pesama"
              && Vreme.BrojPesama(21) == "21 pesma" && Vreme.BrojPesama(22) == "22 pesme" && Vreme.BrojPesama(12) == "12 pesama", "padeži");
        Tvrdi(Vreme.Kratko(432) == "07:12" && Vreme.Kratko(4209) == "1:10:09" && Vreme.Dugo(7120) == "1:58:40", "format vremena");
        return Task.CompletedTask;
    }
}
