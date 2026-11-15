namespace Bronxamp.Testovi;

public static class Spisak
{
    public static string Radni { get; set; } = "";

    public static string NoviFolder() => Directory.CreateDirectory(Path.Combine(Radni, Guid.NewGuid().ToString("N"))).FullName;

    public static readonly (string Ime, Func<Task> Test)[] Svi =
    [
        ("zvuk: besprekidan prelaz bez skoka u talasu", ZvukTestovi.Gapless),
        ("zvuk: crossfade traje koliko treba i ne gubi jačinu", ZvukTestovi.Crossfade),
        ("zvuk: stvarna reprodukcija javlja promenu kad se čuje", ZvukTestovi.PromenaKadSeCuje),
        ("zvuk: kraj reda i pogrešan fajl", ZvukTestovi.KrajIGreska),
        ("zvuk: crossfade i premotavanje u stvarnom vremenu", ZvukTestovi.PrelazUzivo),
        ("zvuk: posle ručne promene pesme plejer zna da svira", () => ZvukTestovi.RucnaPromena(0)),
        ("zvuk: isto na pravom izlazu, tiho", () => ZvukTestovi.RucnaPromena(-1)),
        ("zvuk: pauza dok se pesma učitava ostaje pauza", ZvukTestovi.PauzaTokomUcitavanja),
        ("zvuk: dva prava izlazna uređaja, tiho", ZvukTestovi.PraviUredjaji),
        ("red: redosled, ponavljanje, skokovi", RedTestovi.Redosled),
        ("red: mešanje", RedTestovi.Mesanje),
        ("red: dodavanje, izbacivanje, izmena emisije", RedTestovi.Dodavanje),
        ("red: brisanje pesama i pražnjenje", RedTestovi.Uklanjanje),
        ("podešavanja se čuvaju, verzije su u redu", RedTestovi.PodesavanjaIVerzije),
        ("kraj emisije: račun sa prelazima, padeži, vreme", RedTestovi.Kraj),
        ("pretraga: ekv 1983, metal 90-e, kvačice, ćirilica", BibliotekaTestovi.PretragaUpiti),
        ("biblioteka: skeniranje, premeštanje, brisanje, skinut disk", BibliotekaTestovi.Skeniranje),
        ("dan: ceo dan, hit dana i jinglovi", DanTestovi.CeoDan),
        ("dan: plejliste sa vremenom kao blokovi", DanTestovi.Blokovi),
        ("dan: jinglovi se ne ponavljaju", DanTestovi.JingloviSeNePonavljaju),
        ("dan: emisije i preklapanja", DanTestovi.Emisije),
        ("dan: tok celog dana na lažnom satu", DanTestovi.TokCelogDana),
        ("dan: nastavak po satu i čuvanje", DanTestovi.NastavakPoSatu),
        ("dan: oznake jinglova i folderi", DanTestovi.ImenaIFolderi),
        ("dan: vreme iz imena foldera i dan po satima", DanTestovi.VremeIzImenaIDanPoSatima),
        ("podešavanja: lozinka za stream je šifrovana", DanTestovi.LozinkaSifrovana),
        ("stream: veza pukne pa se program vrati na server", StreamTest.VracanjeVeze),
        ("stream: dok ništa ne svira ne otvara vezu, pa krene kad muzika krene", StreamTest.CekaMuziku),
        ("stream: kad muzika stane, program čeka (nije greška)", StreamTest.StaneMuzika),
        ("stream: u ponoć se veza prekida, a pesma koja svira se ne seče", StreamTest.Ponoc),
        ("stream: enkoder dobija frekvenciju miksera (ne fiksno 44100)", StreamTest.FrekvencijaLame),
        ("metar: skala od −60 do 0 dB i blagi prelaz u crveno", MetarTestovi.Skala),
        ("spisak: red po folderu i broju iz imena, ne po abecedi", PoredakTestovi.Red),
        ("metar: slike nivoa za oko", MetarTestovi.Slika),
        ("spektar: široki spektar za traku (slika za oko)", SpektarTestovi.Slika),
        ("spektar: čist ton podiže stubić svoje frekvencije (20 Hz - 20 kHz)", SpektarTestovi.TonUPravomStubicu),
        ("biblioteka: uklanjanje i vraćanje albuma", BibliotekaTestovi.UklanjanjeAlbuma),
        ("biblioteka: uklanjanje pesama, kvalitet, broj puštanja", BibliotekaTestovi.UklanjanjePesama),
        ("biblioteka: nadogradnja stare baze za kvalitet", BibliotekaTestovi.NadogradnjaKvaliteta),
        ("biblioteka: pojedinačne pesme van foldera", BibliotekaTestovi.PojedinacniFajlovi),
        ("biblioteka: stara kodiranja i imena bez tagova", BibliotekaTestovi.ImenaITagovi),
        ("biblioteka: praćenje izmena foldera", BibliotekaTestovi.PracenjeFoldera),
        ("baza: emisije, istorija i upozorenje", BibliotekaTestovi.Emisije),
    ];

    public static void Tvrdi(bool uslov, string poruka)
    {
        if (!uslov) throw new Exception(poruka);
    }
}
