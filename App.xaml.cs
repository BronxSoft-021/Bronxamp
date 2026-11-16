using System.Windows;
using System.Windows.Threading;
using Bronxamp.Podaci;
using Bronxamp.Prikaz;
using Bronxamp.Sistem;
using Bronxamp.Vm;

namespace Bronxamp;

public partial class App : Application
{
    private JedanPrimerak? _primerak;
    private WindowsMediji? _mediji;
    private WindowState _poslednjeStanje = WindowState.Normal;

    public static App Trenutni => (App)Current;
    public static GlavniVm Vm { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += NaGresku;
        AppDomain.CurrentDomain.UnhandledException += (_, a) => Dnevnik.Greska("Neobrađena greška", a.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, a) =>
        {
            Dnevnik.Greska("Zadatak", a.Exception);
            a.SetObserved();
        };

        // A second start only hands its request over to the running player.
        _primerak = new JedanPrimerak();
        if (!_primerak.Prvi)
        {
            _primerak.Posalji(string.Join(' ', e.Args));
            Shutdown();
            return;
        }

        var uvod = PrikaziUvod(e.Args);

        // The language has to be known before the menu and the window are built.
        Rec.Jezik = Podesavanja.Ucitaj().Jezik;

        // Every window and page is translated when it loads, and again when the language changes.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) => PrevodSvega.Primeni(s as DependencyObject)));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.UserControl), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) => PrevodSvega.Primeni(s as DependencyObject)));
        Rec.Promenjen += PrevodSvega.PrimeniNaSve;

        Vm = new GlavniVm(Dispatcher, e.Args.Contains("--bez-zvuka"));
        var prozor = new GlavniProzor(Vm);
        MainWindow = prozor;
        prozor.Show();
        uvod?.Close(TimeSpan.FromMilliseconds(400));

        _primerak.Stiglo += poruka => Dispatcher.BeginInvoke(() => NaPoruku(poruka));
        _primerak.Slusaj();

        _mediji = new WindowsMediji(Vm.Plejer, Vm.Omoti, Dispatcher);
        Vm.NedavniPromenjeni += () => SkokLista.Osvezi(Vm.Katalog);

        _ = PokreniAsync(e.Args);
    }

    /// <summary>The logo, on screen while the library opens. Test runs go without it.</summary>
    private static SplashScreen? PrikaziUvod(string[] argumenti)
    {
        if (argumenti.Contains("--bez-zvuka")) return null;
        try
        {
            var uvod = new SplashScreen(typeof(App).Assembly, "assets/uvod.png");
            uvod.Show(false, true);
            return uvod;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Dnevnik.Greska("Uvodni ekran", ex);
            return null;
        }
    }

    private async Task PokreniAsync(string[] argumenti)
    {
        await Vm.PokreniAsync();
        SkokLista.Osvezi(Vm.Katalog);
        NaPoruku(string.Join(' ', argumenti));
    }

    /// <summary>Handles "--album 123" from the taskbar list. Anything else just brings the window forward.</summary>
    private void NaPoruku(string poruka)
    {
        var delovi = poruka.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int indeks = Array.IndexOf(delovi, "--album");
        if (indeks >= 0 && indeks + 1 < delovi.Length && long.TryParse(delovi[indeks + 1], out long id) &&
            Vm.Katalog.NadjiAlbum(id) is { } album)
        {
            Vm.PustiAlbum(album);
        }
        VratiProzor();
    }

    public void VratiProzor()
    {
        var prozor = MainWindow;
        if (prozor == null) return;
        if (prozor.WindowState == WindowState.Minimized) prozor.WindowState = _poslednjeStanje;
        prozor.Show();
        prozor.Activate();
    }

    private static void NaGresku(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Dnevnik.Greska("Greška u prozoru", e.Exception);
        e.Handled = true;
    }

    public void NaPromenuProzora(WindowState stanje)
    {
        if (stanje != WindowState.Minimized) _poslednjeStanje = stanje;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mediji?.Dispose();
        _primerak?.Dispose();
        Vm?.Dispose();
        base.OnExit(e);
    }
}
