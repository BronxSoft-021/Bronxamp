using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Bronxamp.Prikaz;

/// <summary>
/// All visible text in one place. The key is the Serbian text; the table below holds the English one, and a
/// missing translation simply stays Serbian. XAML uses <c>{p:Prevod Tekst}</c>, code uses <see cref="T(string)"/>.
/// Changing <see cref="Jezik"/> refreshes every binding at once.
/// </summary>
public sealed class Rec : INotifyPropertyChanged
{
    /// <summary>The one instance bindings listen to.</summary>
    public static readonly Rec Vece = new();

    private static string _jezik = "sr";

    public static string Jezik
    {
        get => _jezik;
        set
        {
            if (_jezik == value) return;
            _jezik = value;
            Vece.PropertyChanged?.Invoke(Vece, new PropertyChangedEventArgs(nameof(Vrednost)));
            Promenjen?.Invoke();
        }
    }

    public static bool EngleskiJezik => _jezik == "en";

    public static event Action? Promenjen;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Only a handle for bindings: its value changes when the language changes, so the text is read again.</summary>
    public string Vrednost => _jezik;

    public static string T(string? kljuc) => string.IsNullOrEmpty(kljuc) ? "" : EngleskiJezik ? Prevedi(kljuc) : Nazad(kljuc);

    public static string T(string format, params object?[] args) => string.Format(T(format), args);

    /// <summary>"1 pesma / 2 pesme / 5 pesama" — or the same in English.</summary>
    public static string Pesama(int n) => EngleskiJezik
        ? n == 1 ? "1 song" : $"{n} songs"
        : n == 1 ? "1 pesma" : n < 5 ? $"{n} pesme" : $"{n} pesama";

    /// <summary>Day of the week, 0 = Sunday, as it is written in the date of a day.</summary>
    public static string Dan(int danUNedelji)
    {
        string[] sr = ["nedelja", "ponedeljak", "utorak", "sreda", "četvrtak", "petak", "subota"];
        string[] en = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
        return (EngleskiJezik ? en : sr)[Math.Clamp(danUNedelji, 0, 6)];
    }

    /// <summary>A line of the broadcast log: the type at the start is translated, the rest is data.</summary>
    public static string Zapis(string linija) => !EngleskiJezik
        ? linija
        : linija
            .Replace("KRAJ ", "END ")
            .Replace("Pesma ", "Song ")
            .Replace("Jingl ", "Jingle ")
            .Replace("Spica ", "Opening ")
            .Replace("Za danas više nema šta da svira.", "nothing left to play that day")
            .Replace("Dan je zaustavljen.", "the day was stopped")
            .Replace("Program je ponovo pokrenut", "the programme was started again");

    /// <summary>"1 jingl / 2 jingla / 5 jinglova" — or the same in English.</summary>
    public static string Jinglova(int n) => EngleskiJezik
        ? n == 1 ? "1 jingle" : $"{n} jingles"
        : n == 1 ? "1 jingl" : n < 5 ? $"{n} jingla" : $"{n} jinglova";

    private static string Prevedi(string kljuc) => Engleski.TryGetValue(kljuc, out var en) ? en : kljuc;

    private static string Nazad(string kljuc) => NazadRecnik.TryGetValue(kljuc, out var sr) ? sr : kljuc;

    /// <summary>English → Serbian, so texts already translated can be put back when Serbian is chosen again.</summary>
    private static Dictionary<string, string>? _nazad;

    private static Dictionary<string, string> NazadRecnik => _nazad ??= NapraviNazad();

    private static Dictionary<string, string> NapraviNazad()
    {
        var nazad = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (sr, en) in Engleski) nazad.TryAdd(en, sr);
        return nazad;
    }

    /// <summary>Serbian → English. Add a line here for every new text.</summary>
    private static readonly Dictionary<string, string> Engleski = new(StringComparer.Ordinal)
    {
        // Left menu
        ["U etru"] = "On air",
        ["Sastavi dan"] = "Build the day",
        ["Jinglovi"] = "Jingles",
        ["Biblioteka"] = "Library",
        ["Folderi"] = "Folders",
        ["Emitovano"] = "Played",
        ["Statistika"] = "Statistics",
        ["Podešavanja"] = "Settings",
        ["M U Z I K A"] = "M U S I C",
        ["I Z V E Š T A J"] = "R E P O R T S",

        // Library
        ["Albumi"] = "Albums",
        ["Izvođači"] = "Artists",
        ["Nedavno"] = "Recent",
        ["Omiljeno"] = "Favourites",
        ["Emisije"] = "Shows",
        ["Omoti"] = "Covers",
        ["Lista"] = "List",
        ["Godina"] = "Year",
        ["+ Dodaj album"] = "+ Add album",
        ["+ Dodaj pesme"] = "+ Add songs",
        ["Dodaj folder sa muzikom"] = "Add a music folder",
        ["Dodaj folder sa muzikom da bi biblioteka oživela."] = "Add a music folder to bring the library to life.",
        ["Pusti album"] = "Play album",
        ["Pusti sledeće"] = "Play next",
        ["Dodaj u red"] = "Add to queue",
        ["Ukloni iz biblioteke"] = "Remove from library",
        ["Pretraži: \"ekv 1983\", \"metal 90-e\"..."] = "Search: \"eqv 1983\", \"metal 90s\"...",

        // Queue and day
        ["SLEDI"] = "UP NEXT",
        ["Sačuvaj kao emisiju"] = "Save as a show",
        ["Isprazni"] = "Clear",
        ["Red je prazan. Klikni album na zidu, dodaj muziku dugmadima ili je prevuci u prozor iz Explorer-a."]
            = "The queue is empty. Click an album on the wall, add music with the buttons, or drag it into the window.",
        ["KRAJ"] = "END OF",
        ["PLEJLISTE"] = "QUEUE",
        ["OSTATAK DANA"] = "REST OF THE DAY",
        ["KRAJ PROGRAMA"] = "END OF PROGRAMME",
        ["pa Auto DJ na Zeno.fm"] = "then Auto DJ on Zeno.fm",
        ["Ništa ne svira"] = "Nothing is playing",
        ["Ništa"] = "Nothing",
        ["Sledi"] = "Next",

        // Transport and status
        ["Pusti"] = "Play",
        ["Pauza"] = "Pause",
        ["Stop"] = "Stop",
        ["Sledeća"] = "Next",
        ["Prethodna"] = "Previous",
        ["Jačina"] = "Volume",
        ["Umanji"] = "Minimise",
        ["Uvećaj"] = "Maximise",
        ["Zatvori"] = "Close",
        ["Pomoć (F1)"] = "Help (F1)",
        ["O programu"] = "About",
        ["Šta je novo"] = "What's new",
        ["Nije povezan"] = "Not connected",
        ["Povezivanje…"] = "Connecting…",
        ["Povezan na server"] = "Connected to the server",
        ["Uključi emitovanje"] = "Start broadcasting",
        ["Prekini emitovanje"] = "Stop broadcasting",
        ["Čeka muziku"] = "Waiting for music",

        // Day screen
        ["Pokreni dan"] = "Start the day",
        ["Zaustavi"] = "Stop",
        ["ODSTUPANJE"] = "DEVIATION",
        ["Pusti sledeće (posle ove pesme)"] = "Play next (after this song)",
        ["Dodaj u red (na kraj)"] = "Add to queue (at the end)",
        ["Pomeri gore"] = "Move up",
        ["Pomeri dole"] = "Move down",
        ["Ukloni iz reda"] = "Remove from the queue",
        ["Dupli klik pušta ovu pesmu"] = "Double-click plays this song",
        ["Preostalo"] = "Remaining",
        ["Proteklo"] = "Elapsed",
        ["Koliko još traje"] = "Time left",
        ["Desni klik: skoči na to mesto u pesmi"] = "Right click: jump to that point in the song",
        ["Izaberi folder sa albumom. Muzika se može i prevući u prozor."] = "Pick an album folder. Music can also be dragged into the window.",
        ["Izaberi jednu ili više pesama. Muzika se može i prevući u prozor."] = "Pick one or more songs. Music can also be dragged into the window.",
        ["Ukloni iz spiska (fajl ostaje na disku)"] = "Remove from the list (the file stays on disk)",
        ["Prikaži pesme"] = "Show songs",

        ["Vreme početka, na primer 20:00"] = "Start time, e.g. 20:00",
        ["Jingl koji ide pre ovoga"] = "Jingle that goes before this one",
        ["Bez vremena, redom popunjavaju dan. Upiši vreme pored foldera i on postaje blok koji počinje tada:"]
            = "Without a time they fill the day in order. Type a time next to a folder and it becomes a block starting then:",
        ["vreme iz imena"] = "time from the name",
        ["Šta ide danas"] = "What is on today",
        ["ili prevuci folder ovde iz Explorer-a"] = "or drag a folder here from Explorer",
        ["Ukloni iz dana"] = "Remove from the day",
        ["Izbaci iz dana"] = "Remove from the day",
        ["Ovde idu plejliste i emisije za taj dan. Vreme u imenu foldera, na primer „20.00 Koncertacija“, program upiše sam."]
            = "Playlists and shows for that day go here. A time in the folder name, e.g. \"20.00 Koncertacija\", is filled in by the program.",
        ["Upiši vreme početka (na primer 15:00), pa ovaj folder postaje blok sa svojim vremenom"]
            = "Type a start time (e.g. 15:00) and this folder becomes a block with its own time",
        ["Klik na red pokazuje šta je u njemu. Pesma po pesma, sa prevlačenjem i ubacivanjem, je u delu U etru."]
            = "Clicking a row shows what is inside. Song by song, with dragging and inserting, is on the On air page.",
        ["Dan se slaže sam čim ima šta da svira."] = "The day builds itself as soon as there is something to play.",
        ["Napredno: folderi, odstupanje, špica i jinglovi za hit, oznake jinglova"]
            = "Advanced: folders, deviation, opening and hit jingles, jingle labels",
        ["Program sam počinje u 08:59:45 i ide do ponoći. Može se pokrenuti i ranije, pa čeka svoje vreme."]
            = "The programme starts by itself at 08:59:45 and runs until midnight. It can be started earlier and waits for its time.",
        ["Upiši vreme početka emisije, na primer 19:05"] = "Type the show start time, e.g. 19:05",
        ["Ostali fajlovi u folderu šeme. Ako je neki emisija u jednom fajlu, upiši joj vreme početka:"]
            = "Other files in the schedule folder. If one is a show in a single file, type its start time:",
        ["DAN PO SATIMA"] = "DAY BY HOUR",
        ["Slažem..."] = "Building…",
        ["Samo pesme"] = "Songs only",
        ["Ceo dan"] = "Whole day",
        ["Zadata vremena"] = "Set times",
        ["Složi iznova"] = "Rebuild",
        ["emisija"] = "show",
        ["Ne staje u blok, ostaje van dana:"] = "Does not fit the block, stays out of the day:",
        ["Ove pesme ne staju u blok i neće se pustiti. Klik na red ih pokazuje."]
            = "These songs do not fit the block and will not be played. Click a row to see them.",

        ["Briše ručne izmene i slaže dan iz foldera iznova. Fajlovi na disku se ne diraju."]
            = "Clears the manual changes and rebuilds the day from the folders. Files on disk are left alone.",
        ["Izaberi fajl..."] = "Choose a file…",
        ["Hit dana"] = "Hit of the day",
        ["Stavi hit dana u folder šeme (kad je tamo jedan fajl, program ga uzme sam) ili ga izaberi ovde."]
            = "Put the hit of the day in the schedule folder (a single file there is taken by itself) or pick it here.",
        ["Program ga je našao sam: jedini fajl u folderu šeme."] = "The program found it: the only file in the schedule folder.",
        ["Isprazni dan"] = "Empty the day",
        ["Briše sve iz dana (emisije, plejliste i hit) i ostavlja prazan spisak da ga sastaviš iznova. Muzika na disku se ne dira."]
            = "Deletes everything in the day (shows, playlists and the hit) and leaves an empty list to build again. Music on disk is left alone.",
        ["Sledeći dan"] = "Next day",
        ["Prethodni dan"] = "Previous day",
        ["POČETAK DANA"] = "START OF THE DAY",
        ["Pusti emisiju"] = "Play the show",
        ["Obriši emisiju"] = "Delete the show",
        ["Desni klik na album ili pesmu i „Dodaj u emisiju“."] = "Right-click an album or a song and choose \"Add to the show\".",
        ["Ukloni iz emisije"] = "Remove from the show",
        ["Istorija"] = "History",
        ["Pesme"] = "Songs",
        ["Izmeni"] = "Edit",
        ["Ukloni izabrano"] = "Remove selected",
        ["Još ništa nije emitovano."] = "Nothing has been broadcast yet.",
        ["Ove nedelje"] = "This week",
        ["Ovog meseca"] = "This month",
        ["+ Nova emisija"] = "+ New show",
        ["Program sam ubacuje samo nasumične. Jinglovi emisija se biraju uz emisiju, a ručni se ubacuju kad ti poželiš."]
            = "The programme only inserts random jingles. Show jingles are chosen with the show, manual ones are inserted when you want.",

        ["sekundi od punog sata."] = "seconds from the full hour.",
        ["Hit dana sme da krene ±"] = "The hit of the day may start ±",
        ["FOLDERI"] = "FOLDERS",
        ["Jingl za hit dana"] = "Jingle for the hit of the day",
        ["Špica"] = "Opening",
        ["Bira se jednom i program ih pamti."] = "Chosen once, the program remembers them.",
        ["Jingl posle hita"] = "Jingle after the hit",
        ["Obriši sve"] = "Delete all",
        ["Briše sve iz liste (kao u klasičnim plejerima). Dok dan traje, pesma koja svira i odsvirano ostaju."]
            = "Deletes everything in the list (as in classic players). While the day runs, the song on air and what already played stay.",
        ["Trajanje"] = "Duration",
        ["Naslov"] = "Title",
        ["Vrsta"] = "Type",
        ["Početak"] = "Start",
        ["Zadato"] = "Scheduled",
        ["Izvođač"] = "Artist",
        ["Dan još nije složen. Otvori Sastavi dan."] = "The day is not built yet. Open Build the day.",

        // Settings
        ["Adresa"] = "Address",
        ["Korisnik"] = "User",
        ["Lozinka"] = "Password",
        ["Izlaz"] = "Output",
        ["Kvalitet"] = "Quality",
        ["Prikaži"] = "Show",
        ["Test veze"] = "Test connection",
        ["Skeniraj ponovo"] = "Rescan",
        ["Dodaj folder"] = "Add folder",
        ["nedelja"] = "weeks",
        ["ZVUK"] = "SOUND",
        ["PRELAZ IZMEĐU PESAMA"] = "CROSSFADE BETWEEN SONGS",
        ["Upozori ako je pesma puštana u istoj emisiji u poslednjih"]
            = "Warn if a song was played in the same show in the last",
        ["Prelaz i kad svira album (inače album ide bez prekida i bez prelaza)"]
            = "Crossfade even inside an album (otherwise an album plays gapless, without crossfade)",
        ["Slušalice (preslušavanje)"] = "Headphones (cue)",
        ["EMITOVANJE NA ZENO.FM"] = "BROADCASTING TO ZENO.FM",
        ["Probno (lokalni Icecast 127.0.0.1:8000 — pravi Zeno se ne dira)"]
            = "Test mode (local Icecast 127.0.0.1:8000 — the real Zeno is left alone)",
        ["Lozinka je šifrovana za ovaj Windows nalog (DPAPI). Dok je uključeno „Probno“, emituje se na lokalni server, ne na pravi Zeno."]
            = "The password is encrypted for this Windows account (DPAPI). While \"Test mode\" is on, it broadcasts to the local server, not the real Zeno.",
        ["Nivo zvuka: zeleno do −3 dB, blagi prelaz u crveno od −3 do 0 dB"]
            = "Sound level: green up to −3 dB, fading into red from −3 to 0 dB",
        ["  Slušalice:"] = "  Headphones:",
        ["A-Š"] = "A–Z",
        ["JEZIK / LANGUAGE"] = "LANGUAGE / JEZIK",

        // Messages and statuses built in code
        ["Veza pukla — vraćam se…"] = "Connection dropped — reconnecting…",
        ["Nije se povezao…"] = "Could not connect…",
        ["Prekinuto u ponoć"] = "Stopped at midnight",
        ["SLEDI · {0}"] = "UP NEXT · {0}",
        ["SLEDI · {0} {1:dd.MM.}"] = "UP NEXT · {0} {1:dd.MM.}",
        ["... još {0}"] = "… {0} more",
        ["Stavki: {0} · {1}"] = "Items: {0} · {1}",
        ["Još {0} · kraj u {1}"] = "{0} left · ends at {1}",
        ["{0} · prelaz {1:0.#} s"] = "{0} · crossfade {1:0.#} s",
        ["{0} · bez prelaza"] = "{0} · no crossfade",
        ["Skeniram {0:N0} / {1:N0}"] = "Scanning {0:N0} / {1:N0}",
        ["Sledi u {0}"] = "Next at {0}",
        ["Dodato na kraj reda: {0}."] = "Added to the end of the queue: {0}.",
        ["Novih jinglova: {0}. Proveri njihove oznake u delu Jinglovi."]
            = "New jingles: {0}. Check their labels on the Jingles page.",

        ["{0} · još {1}"] = "{0} · {1} left",
        ["Dan svira"] = "The day is on air",
        ["Čeka"] = "Waiting",
        ["Dan nije pokrenut"] = "The day is not running",
        ["Dan nije složen."] = "The day is not built.",
        ["Dan još nije složen."] = "The day is not built yet.",
        ["Folder sa šemom ili jinglovima nije dostupan."] = "The schedule or jingles folder is not available.",
        ["Slažem dan..."] = "Building the day…",
        ["Dan nije složen: neki fajl nije dostupan."] = "The day is not built: a file is not available.",
        ["Taj dan je prošao."] = "That day has passed.",
        ["{0} · {1} · kraj u {2}{3}"] = "{0} · {1} · ends at {2}{3}",
        [" · ostalo neiskorišćeno: {0}"] = " · unused: {0}",

        // About and help
        ["Verzija {0}"] = "Version {0}",
        ["Muzički plejer za Windows. Program ništa ne šalje na internet, sve ostaje na ovom računaru."]
            = "A music player for Windows. The program sends nothing to the internet; everything stays on this computer.",
        ["KORIŠĆENE TEHNOLOGIJE"] = "TECHNOLOGIES USED",
        ["osnova programa i prozori"] = "the program itself and its windows",
        ["zvuk, prelazi, spektar"] = "sound, crossfades, spectrum",
        ["FLAC i Opus fajlovi"] = "FLAC and Opus files",
        ["slanje zvuka na Zeno.fm"] = "sending sound to Zeno.fm",
        ["veza između programa i BASS-a"] = "the link between the program and BASS",
        ["veza između programa i BASSenc-a"] = "the link between the program and BASSenc",
        ["pretvaranje u MP3 za etar"] = "converting to MP3 for broadcast",
        ["čitanje tagova i omota"] = "reading tags and covers",
        ["biblioteka, emisije, istorija"] = "library, shows, history",
        ["veza između programa i SQLite-a"] = "the link between the program and SQLite",
        ["pomoć za izradu prozora"] = "help with building the windows",
        ["LCD cifre"] = "LCD digits",
        ["besplatno za nekomercijalnu upotrebu"] = "free for non-commercial use",
        ["javno vlasništvo"] = "public domain",
        ["Licence"] = "Licences",
        ["Dodaj u emisiju"] = "Add to the show",
        ["Nova emisija..."] = "New show…",

        ["{0} za {1}"] = "{0} at {1}",
        ["Odstupanje"] = "Deviation",
        ["+ Dodaj"] = "+ Add",
        ["Najslušanije"] = "Most played",
        ["Nije puštano godinu dana"] = "Not played for a year",
        ["Ručno"] = "Manual",
        ["Nasumičan"] = "Random",
        ["prazan"] = "empty",
        ["Ništa nije pronađeno."] = "Nothing was found.",
        ["Žanr"] = "Genre",
        ["Broj pesme"] = "Track no.",
        ["Puštano"] = "Played",
        ["Poslednji put"] = "Last played",
        ["Dodato"] = "Added",
        ["Frekvencija"] = "Sample rate",
        ["Veličina"] = "Size",
        ["Naziv fajla"] = "File name",
        ["Slušano"] = "Listens",
        ["Izaberi šta se vidi u spisku"] = "Choose what the list shows",
        ["OBRADA ZVUKA"] = "AUDIO PROCESSING",
        ["Limiter (zaštita strima od klipovanja)"] = "Limiter (protects the stream from clipping)",
        ["Vrhovi iznad −1,5 dB se prigušuju. Ne menja dužinu pesme. Primenjuje se pri sledećem pokretanju."]
            = "Peaks above −1.5 dB are pressed down. It does not change the length of a song. Applied at the next start.",
        ["Vrhovi iznad −1,5 dB se prigušuju. Ne menja dužinu pesme. Primenjuje se odmah pošto sačuvaš."]
            = "Peaks above −1.5 dB are pressed down. It does not change the length of a song. Applied as soon as you save.",
        ["Normalizuj glasnoću"] = "Normalize loudness",
        ["Svaka pesma se pušta na istoj glasnoći. Dužina pesme se ne menja, pa raspored dana ostaje tačan."]
            = "Every song plays at the same loudness. A song stays as long as it is, so the day keeps its clock.",
        ["Cilj"] = "Target",
        ["LUFS (za strim −14)"] = "LUFS (−14 for a stream)",
        ["Prag"] = "Threshold",
        ["Izmeri biblioteku"] = "Measure the library",
        ["Merenje… {0:N0} / {1:N0}"] = "Measuring… {0:N0} / {1:N0}",
        ["Izmereno: {0:N0}."] = "Measured: {0:N0}.",
        ["Sve pesme imaju izmerenu glasnoću."] = "Every song already has its loudness measured.",
        ["Merim glasnoću {0:N0} / {1:N0}"] = "Measuring loudness {0:N0} / {1:N0}",
        ["{0} stvari treba da se srede"] = "{0} things to sort out",
        ["Fali pesama za {0} do {1}."] = "Not enough songs for {0} until {1}.",
        ["Dan je složen. Pogledaj listu i pokreni ga."] = "The day is built. Check the list and start it.",
        ["Dan je složen, ali proveri upozorenja."] = "The day is built, but check the warnings.",
        ["Za danas više nema šta da svira."] = "There is nothing left to play today.",
        ["Izaberi album ili emisiju"] = "Pick an album or a show",
        ["Čitam folder..."] = "Reading the folder…",
        ["Jingl hit"] = "Hit jingle",
        ["HIT DANA"] = "HIT OF THE DAY",
        ["Pesma"] = "Song",
        ["Jingl"] = "Jingle",
        ["Emisija"] = "Show",

        // Common
        ["Dodaj"] = "Add",
        ["Ukloni"] = "Remove",
        ["Sačuvaj"] = "Save",
        ["Otkaži"] = "Cancel",
        ["Promeni"] = "Change",
        ["Otvori"] = "Open",
        ["Nazad"] = "Back",
        ["Da"] = "Yes",
        ["Ne"] = "No",
    };
}

/// <summary>XAML: <c>Text="{p:Prevod Pusti}"</c> — the key is the Serbian text.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class Prevod : MarkupExtension
{
    public Prevod()
    {
    }

    public Prevod(string kljuc) => Kljuc = kljuc;

    public string Kljuc { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding(nameof(Rec.Vrednost))
        {
            Source = Rec.Vece,
            Mode = BindingMode.OneWay,
            Converter = PrevodKonvertor.Jedan,
            ConverterParameter = Kljuc,
        }.ProvideValue(serviceProvider);
}

/// <summary>Turns a key into the text of the chosen language.</summary>
public sealed class PrevodKonvertor : IValueConverter
{
    public static readonly PrevodKonvertor Jedan = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Rec.T(parameter as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
