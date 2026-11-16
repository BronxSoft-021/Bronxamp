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
