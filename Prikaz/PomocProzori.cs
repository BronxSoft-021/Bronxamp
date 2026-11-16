using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Bronxamp.Prikaz;

/// <summary>"O programu": what the program is and everything it is built from.</summary>
public sealed class OProgramuProzor : TamniDijalog
{
    /// <summary>A new library means a new row here and a new block in Licence.txt.</summary>
    public static readonly (string Ime, string Namena, string Licenca)[] Tehnologije =
    [
        (".NET 10 i WPF", "osnova programa i prozori", "MIT"),
        ("BASS, BASSmix", "zvuk, prelazi, spektar", "besplatno za nekomercijalnu upotrebu"),
        ("BASSFLAC, BASSOPUS", "FLAC i Opus fajlovi", "besplatno za nekomercijalnu upotrebu"),
        ("BASSenc", "slanje zvuka na Zeno.fm", "besplatno za nekomercijalnu upotrebu"),
        ("ManagedBass", "veza između programa i BASS-a", "MIT"),
        ("ManagedBass.Enc", "veza između programa i BASSenc-a", "MIT"),
        ("LAME (lame.exe)", "pretvaranje u MP3 za etar", "LGPL 2.1"),
        ("TagLib#", "čitanje tagova i omota", "LGPL 2.1"),
        ("SQLite", "biblioteka, emisije, istorija", "javno vlasništvo"),
        ("Microsoft.Data.Sqlite", "veza između programa i SQLite-a", "MIT"),
        ("SQLitePCLRaw", "veza između programa i SQLite-a", "Apache 2.0"),
        ("CommunityToolkit.Mvvm", "pomoć za izradu prozora", "MIT"),
        ("DSEG fontovi", "LCD cifre", "SIL OFL 1.1"),
    ];

    public static string PutanjaLicenci => Path.Combine(AppContext.BaseDirectory, "Licence.txt");

    public OProgramuProzor() : base(680)
    {
        var logo = new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Bronxamp;component/assets/logo_bronxamp.png")),
            Stretch = Stretch.Uniform,
            MaxHeight = 190,
        };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);

        var slika = new Grid();
        slika.Children.Add(logo);
        slika.Children.Add(DugmeZatvori());

        var sadrzaj = new StackPanel { Margin = new Thickness(26, 6, 26, 22) };
        sadrzaj.Children.Add(Tekst("BronxSoft Bronxamp", 19, bold: true));
        sadrzaj.Children.Add(Tekst(Rec.T("Verzija {0}", Verzije.Trenutna), 13, "Mutan"));
        var opis = Tekst(Rec.T("Muzički plejer za Windows. Program ništa ne šalje na internet, sve ostaje na ovom računaru."), 13);
        opis.Margin = new Thickness(0, 10, 0, 0);
        sadrzaj.Children.Add(opis);

        var naslov = Tekst(Rec.T("KORIŠĆENE TEHNOLOGIJE"), 12, "Mutan", bold: true);
        naslov.Margin = new Thickness(0, 20, 0, 8);
        sadrzaj.Children.Add(naslov);
        sadrzaj.Children.Add(Tabela());

        var licence = Dugme("Licence", false, (_, _) => OtvoriLicence());
        licence.Margin = new Thickness(0);
        licence.IsEnabled = File.Exists(PutanjaLicenci);
        var dole = new DockPanel { Margin = new Thickness(0, 18, 0, 0) };
        DockPanel.SetDock(licence, Dock.Right);
        dole.Children.Add(licence);
        var potpis = Tekst("BronxSoft 2026", 12, "Mutan");
        potpis.VerticalAlignment = VerticalAlignment.Center;
        dole.Children.Add(potpis);
        sadrzaj.Children.Add(dole);

        var sve = new StackPanel();
        sve.Children.Add(slika);
        sve.Children.Add(sadrzaj);
        Content = sve;
    }

    private static Grid Tabela()
    {
        var tabela = new Grid();
        tabela.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        tabela.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(216) });
        tabela.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        foreach (var (ime, namena, licenca) in Tehnologije)
        {
            int red = tabela.RowDefinitions.Count;
            tabela.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Celija(tabela, Tekst(ime, 13), red, 0);
            Celija(tabela, Tekst(Rec.T(namena), 13, "Mutan"), red, 1);
            Celija(tabela, Tekst(Rec.T(licenca), 12, "Mutan"), red, 2);
        }
        return tabela;
    }

    private static void Celija(Grid tabela, TextBlock tekst, int red, int kolona)
    {
        tekst.Margin = new Thickness(0, 0, 10, 5);
        Grid.SetRow(tekst, red);
        Grid.SetColumn(tekst, kolona);
        tabela.Children.Add(tekst);
    }

    private static void OtvoriLicence()
    {
        try
        {
            Process.Start(new ProcessStartInfo(PutanjaLicenci) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dnevnik.Greska("otvaranje licenci", ex);
        }
    }
}

/// <summary>"Pomoć": a short manual, one topic at a time.</summary>
public sealed class PomocProzor : TamniDijalog
{
    /// <summary>A new feature the user has to learn gets a line here too.</summary>
    public static readonly (string Naslov, string[] Stavke)[] TemeSr =
    [
        ("Sastavi dan",
        [
            "Tri koraka, odozgo nadole: 1. hit dana, 2. šta ide danas, 3. Pokreni dan. Dan se slaže sam čim nešto promeniš, nema dugmeta „Složi“.",
            "Semafor na vrhu: zeleno znači da je sve spremno, a žuto nabraja šta treba srediti.",
            "Vreme može da stoji u imenu foldera ili fajla: „20.00 Koncertacija“, „9:05 Alternative“ ili „23h Laku noć“. Program ga upiše sam. Folder sa datumom („14-11-2026 20.00 Koncertacija“) ide samo tog dana.",
            "Ako je u korenu foldera sa šemom jedan mp3 bez vremena u imenu, on je hit dana. Folder ili fajl se dodaje i prevlačenjem iz Explorer-a na Sastavi dan.",
            "Pored svakog foldera bez vremena je polje --:--. Upiši vreme (15:00) i folder postaje blok koji tada počinje. Ostali fajlovi u folderu šeme, na primer emisija u jednom fajlu, stoje ispod sa istim poljem; upisano vreme program zapamti za sledeći put.",
            "Dan po satima: svaki blok, hit i emisija su jedan red. Klik na red pokazuje pesme i jinglove u njemu i pesme koje ne staju. Višak nije greška, semafor zbog njega ostaje zelen.",
            "Početak dana (špica, jingl za hit dana i jingl posle hita), odstupanje i folderi su sklopljeni u delu Napredno. Biraš ih ti, jednom, a program pamti i sam ništa ne bira.",
            "Program iz jednog bloka ne uzima za drugi. Unutar bloka sme da pomeri pesme pred kraj, da ne štrče. Ako pesama zafali, kaže koliko, a ti ih dodaš.",
            "U raspored ide sve što ima svoje vreme. Folder sa pesmama je blok: unutra idu jinglovi, a hit dana ga prekine u svom terminu i blok nastavi. Gotov fajl se ne dira. Ako blok ne stane do sledećeg, program kaže koje pesme je izostavio.",
            "Dok dan traje, i kad je složen a ništa drugo nije u redu, desno stoji Ostatak dana i Kraj programa. Kad puštaš muziku van dana, desno je red pesama i Kraj plejliste.",
            "Podaci za Zeno.fm su u Podešavanjima, u delu Emitovanje na Zeno.fm. Dugme Prikaži otkriva lozinku.",
            "„Složi iznova“ briše ručne izmene u listi i slaže dan iz foldera iznova, a muzika na disku ostaje.",
            "„Isprazni dan“ briše sve iz dana (emisije, plejliste i hit) i ostavlja prazan spisak, pa sastavljaš iznova. Muzika na disku ostaje.",
            "Na ekranu U etru vidiš svaku stavku sa vremenom, a kod hita i emisija odstupanje od zadatog vremena. Stavke se pomeraju levim dugmetom miša, prevlačenjem. Desni klik nudi preslušavanje, brisanje i dodavanje jingla ili pesme. To radi i dok dan traje, za sve što još nije puštano.",
            "„Pokreni dan“ čeka 08:59:45. Ako je to vreme već prošlo, kreće od stavke koja treba da svira po satu.",
            "Blok hita kreće 15 sekundi pre termina (10:59:45 za 11h), kao i špica u 08:59:45.",
            "Hit dana se drži sata na nekoliko sekundi, a blok i emisija kreću do 30 sekundi od svog vremena. Odmah posle hita kreću bez ikakvog dodavanja, koliko god hit trajao. Poslednje pesme dana smeju da odu malo posle ponoći.",
            "Stavka sa zadatim vremenom čeka kraj pesme koja svira. Ako dan kasni, program preskače pesme i jinglove pre nje, a to upisuje u dnevnik.",
            "Ako program pukne ili se računar restartuje, posle pokretanja sam nastavlja od mesta na kome treba da bude po satu.",
            "Jinglovi: program sam podeli fajlove po imenu, a ti to proveriš i ispraviš. Nasumične ubacuje sam, na svake tri pesme, a samo izuzetno na dve. Nikad ne dodaje jingl mimo toga, i nijedan se u toku dana ne ponavlja. Oznaka „Ručno“ znači da ih program nikad ne pušta sam.",
            "Šta je stvarno odsvirano piše u folderu sa podacima, u `emitovano`, po jedan fajl za svaki dan.",
        ]),
        ("Emitovanje",
        [
            "Dugme „Uključi emitovanje“ u gornjoj traci šalje baš ono što svira, pa na Zeno.fm ide ono što čuješ u programu.",
            "Pored dugmeta su tačka i status: zelena i „Povezan na server“ znači da si na etru, žuta „Povezivanje…“ da se povezuje, crvena da veze nema.",
            "Ako veza pukne, program piše „Veza pukla — vraćam se…“ i sam pokušava ponovo, bez tvog klika.",
            "Kad predješ mišem preko trake, vidiš adresu servera, a i razlog ako nešto nije u redu.",
            "Pri svakoj promeni pesme na etar se pošalje „Izvođač - Naslov“, pa slušaoci vide šta svira.",
            "Dok je u Podešavanjima uključeno „Probno“, program šalje na lokalni server (127.0.0.1:8000) i to piše u opisu kad predješ mišem preko trake. Za pravi etar isključi „Probno“.",
            "„Test veze“ u Podešavanjima proveri adresu, lozinku i kvalitet i odmah se odjavi, pa ništa ne ostane uključeno.",
            "Isto dugme prekida slanje: tada piše „Prekini emitovanje“.",
            "U ponoć program sam prekine emitovanje: poslednja pesma dana sme da se završi, a onda na serveru nastavlja njegov Auto DJ. U traci tada piše „Prekinuto u ponoć“, a ujutru uključi emitovanje pre početka dana.",
            "Lozinka za Zeno je šifrovana za tvoj Windows nalog i ne vidi se u fajlovima programa.",
        ]),
        ("Prvi koraci",
        [
            "Klikni „+ Dodaj album“ i izaberi folder sa albumom, ili „+ Dodaj pesme“ i izaberi jednu ili više pesama.",
            "Muzika može i da se prevuče iz Explorer-a u prozor plejera.",
            "Dodato odmah ide u red. Ako je red bio prazan, a dodat je ceo album, album kreće da svira.",
            "Folder koji dodaš postaje folder biblioteke: plejer ga prati, pa se nove pesme same pojavljuju.",
            "Fajlovi se nikad ne menjaju i ne brišu. Plejer ih samo čita.",
        ]),
        ("Biblioteka",
        [
            "Albumi se mogu gledati na četiri načina: Omoti, Lista, A-Š i Godina. Izbor je gore desno.",
            "Izvođači, Nedavno, Omiljeno i Emisije su pilule iznad Biblioteke.",
            "Klik na album ga pušta od prve pesme.",
            "Kad miš stane na album ili pesmu, pojave se dugmad: pusti, pusti sledeće, dodaj u red, pesme albuma i ukloni. Isto je i na desni klik.",
            "Spisak pesama pokazuje izvođača, naslov i trajanje. Desni klik na zaglavlje spiska, ili dugme sa zupčanikom desno gore, bira šta se još vidi: album, godina, žanr, puštano, poslednji put, format, bitrate, frekvencija, veličina, folder... Nova kolona se pojavi na kraju.",
            "Kolona se pomera tako što njen naziv u zaglavlju prevučeš na drugo mesto, a širina se menja prevlačenjem ivice. Plejer pamti raspored.",
            "„Ukloni iz biblioteke“ sklanja album iz plejera, a „Ukloni iz spiska“ izabrane pesme (Ctrl+A bira sve, Delete uklanja). Fajlovi ostaju na disku, a sve se vraća u Podešavanjima, dugmetom „Vrati uklonjeno“.",
            "Ako pesme nema na disku, na primer kad je disk isključen, ona i njena istorija ostaju sačuvane dok se disk ne vrati.",
        ]),
        ("Pretraga",
        [
            "Polje za pretragu traži dok kucaš, po izvođaču, albumu, pesmi, godini i žanru. Ctrl+F te odmah vodi u polje, a Esc ga briše.",
            "Svaka reč može biti u drugom polju: „ekv 1983“ nalazi Ekatarinu Veliku iz 1983.",
            "Decenija se piše ovako: „metal 90-e“ traži metal od 1990. do 1999.",
            "Kvačice nisu potrebne, a ćirilica i latinica su isto.",
        ]),
        ("Puštanje",
        [
            "Gore su sat, U etru i Sledi, a dugmad i jačina su dole, ispod liste. Traka u polju U etru pokazuje napredak. Desni klik skače na to mesto u pesmi. Levi klik i prevlačenje rade isto, osim dok dan traje, da se pesma ne pomeri slučajno.",
            "Razmak pušta i pauzira, osim dok se kuca u nekom polju.",
            "„Nasumično“ pušta pesme nasumičnim redom. Uključeno dugme je narandžasto.",
            "„Ponavljanje“ se menja klikom: bez ponavljanja, ponovi sve, ponovi pesmu.",
            "Boja gornje trake se uzima sa omota pesme koja svira.",
        ]),
        ("Red i „Sledi“",
        [
            "Desni panel pokazuje šta sledi i koliko to ukupno traje.",
            "„Pusti sledeće“ stavlja pesmu odmah posle tekuće, a „Dodaj u red“ na kraj reda.",
            "Dupli klik na pesmu u redu je pušta.",
            "X na redu ili taster Delete uklanja pesmu, a „Isprazni“ ceo red.",
            "„Sačuvaj kao emisiju“ pravi emisiju od svega što je u redu.",
            "„KRAJ PLEJLISTE“ dole pokazuje u koliko sati se red završava i koliko je još ostalo, računajući i prelaze.",
            "Red je udubljen kao ležište, sa tankim linijama koje pokazuju mesta za pesme; kad prevlačiš muziku u prozor, ivica se oboji narandžasto — tu pesme upadaju.",
        ]),
        ("Emisije",
        [
            "Biblioteka, pa Emisije: „Nova emisija“ pravi emisiju sa imenom i bojom.",
            "Pesme se dodaju desnim klikom na album ili pesmu, pa „Dodaj u emisiju“.",
            "Redosled se menja prevlačenjem pesama u emisiji.",
            "„Pusti emisiju“ je pušta od početka. Ono što odsvira upisuje se u karticu „Istorija“.",
            "Ako je pesma već puštana u istoj emisiji, plejer upozorava. Period je u Podešavanjima, podrazumevano 8 nedelja.",
            "„Izmeni“ menja ime i boju, a „Obriši emisiju“ briše emisiju, ne i pesme.",
        ]),
        ("Zvuk i prelazi",
        [
            "Album ide bez prekida, kao na ploči: između pesama nema tišine ni skoka.",
            "Prelaz između pesama se podešava od 0 do 5 sekundi. Za albume važi samo ako je uključeno „Prelaz i kad svira album“. Sastavljen dan ide bez prelaza.",
            "Izlaz zvuka se bira u Podešavanjima.",
            "Preslušavanje: u Podešavanjima izaberi drugi uređaj kod „Slušalice“, pa na desni klik na pesmu izaberi „Preslušaj na slušalicama“. Glavni izlaz svira dalje, a „Zaustavi“ prekida preslušavanje.",
        ]),
        ("Statistika",
        [
            "„Najslušanije“ pokazuje šta je najviše puštano ove nedelje ili ovog meseca.",
            "„Nije puštano godinu dana“ podseća na muziku koja je stala u zapećak.",
        ]),
        ("Windows",
        [
            "Medijski tasteri na tastaturi rade i kad plejer nije izabran.",
            "Windows prikazuje pesmu i kontrole na zaključanom ekranu i u prozoru za jačinu.",
            "Na sličici plejera na taskbaru su dugmad za prethodnu pesmu, puštanje i sledeću pesmu.",
            "Desni klik na ikonicu na taskbaru nudi nedavne albume.",
        ]),
        ("Tasteri",
        [
            "F1 - ova pomoć",
            "Razmak - pusti ili pauziraj",
            "Ctrl+F - idi u pretragu",
            "Esc - obriši pretragu, zatvori prozor",
            "Enter - pusti izabranu pesmu",
            "Ctrl+A - izaberi sve pesme u spisku",
            "Delete - ukloni izabrano iz spiska, reda ili emisije",
            "U etru: Ctrl+A bira ceo dan, Delete briše izabrano, a desni klik i dugme „Obriši sve“ brišu sve (dok dan traje, pesma koja svira i odsvirano ostaju).",
        ]),
    ];

    /// <summary>The help in the chosen language.</summary>
    public static (string Naslov, string[] Stavke)[] Teme => Rec.EngleskiJezik ? PomocEngleski.Teme : TemeSr;

    private readonly List<Button> _dugmad = [];
    private readonly StackPanel _tekst = new() { Margin = new Thickness(24, 20, 24, 20) };
    private readonly ScrollViewer _klizac = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

    public PomocProzor() : base(860)
    {
        var meni = new StackPanel { Margin = new Thickness(14, 16, 14, 14) };
        var naslov = Tekst("Pomoć", 19, bold: true);
        naslov.Margin = new Thickness(10, 0, 0, 12);
        meni.Children.Add(naslov);
        for (int i = 0; i < Teme.Length; i++)
        {
            int tema = i;
            var dugme = new Button { Content = Teme[i].Naslov, Style = (Style)Application.Current.FindResource("StavkaMenija") };
            dugme.Click += (_, _) => Prikazi(tema);
            _dugmad.Add(dugme);
            meni.Children.Add(dugme);
        }

        var levo = new Border
        {
            Background = (Brush)Application.Current.FindResource("Pozadina"),
            Child = meni,
        };

        _klizac.Content = _tekst;
        Grid.SetColumn(_klizac, 1);
        var zatvori = DugmeZatvori();
        Grid.SetColumn(zatvori, 1);

        var sve = new Grid { Height = 560 };
        sve.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        sve.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sve.Children.Add(levo);
        sve.Children.Add(_klizac);
        sve.Children.Add(zatvori);
        Content = sve;

        Prikazi(0);
    }

    public void Prikazi(int tema)
    {
        for (int i = 0; i < _dugmad.Count; i++)
        {
            if (i == tema) _dugmad[i].Background = (Brush)Application.Current.FindResource("Povrsina");
            else _dugmad[i].ClearValue(BackgroundProperty);
        }

        _tekst.Children.Clear();
        var naslov = Tekst(Teme[tema].Naslov, 19, bold: true);
        naslov.Margin = new Thickness(0, 0, 30, 14);
        _tekst.Children.Add(naslov);
        foreach (var stavka in Teme[tema].Stavke)
        {
            var red = Tekst("•  " + stavka, 14);
            red.Margin = new Thickness(0, 0, 0, 9);
            _tekst.Children.Add(red);
        }
        _klizac.ScrollToTop();
    }
}
