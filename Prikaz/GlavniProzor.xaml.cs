using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Bronxamp.Podaci;
using Bronxamp.Vm;
using Microsoft.Win32;

namespace Bronxamp.Prikaz;

public partial class GlavniProzor : Window
{
    private const int WmAppCommand = 0x0319;
    private const int AppCommandNext = 11, AppCommandPrevious = 12, AppCommandStop = 13, AppCommandPlayPause = 14;
    private const int AppCommandPlay = 46, AppCommandPause = 47;

    private readonly GlavniVm _vm;
    private readonly Brush _akcenat;
    private readonly Brush _bela = Brushes.White;
    private readonly Brush _tamno = new SolidColorBrush(Color.FromRgb(0x1B, 0x12, 0x06));

    public GlavniProzor(GlavniVm vm)
    {
        _vm = vm;
        DataContext = vm;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(GlavniVm.Strana)) return;
            // Pages are built from templates when you switch, so their texts are translated again a moment later.
            Dispatcher.BeginInvoke(() => PrevodSvega.Primeni(this), DispatcherPriority.Loaded);
        };
        InitializeComponent();
        _akcenat = (Brush)FindResource("Akcenat");

        var p = vm.Podesavanja;
        Width = p.Sirina;
        Height = p.Visina;
        if (p.Levo is double levo && p.Gore is double gore && NaEkranu(levo, gore))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = levo;
            Top = gore;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        if (p.Uvecan) WindowState = WindowState.Maximized;

        Napredak.Premotano += odnos =>
        {
            vm.Plejer.PremotajNa(odnos);
            vm.Dan.NaPremotavanje();
        };
        vm.Plejer.PropertyChanged += NaPromenuPlejera;
        OsveziDugmad();

        SourceInitialized += NaInicijalizaciju;
        StateChanged += (_, _) => NaPromenuStanja();
        Closing += NaZatvaranje;
        vm.Dan.PitajPrekid = () => Potvrda.Pitaj(this, "Dan je u toku. Prekinuti ga?", "Program dana prestaje, a pušta se ono što si izabrao.", "Prekini");
        PreviewKeyDown += NaTaster;
    }

    private static bool NaEkranu(double levo, double gore) =>
        levo < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80 &&
        gore < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 80 &&
        levo > SystemParameters.VirtualScreenLeft - 200 && gore >= SystemParameters.VirtualScreenTop - 20;

    private void NaInicijalizaciju(object? sender, EventArgs e)
    {
        Sistem.ProzorStil.ZaobljeneIvice(this);
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(Poruke);
        NaPromenuStanja();
    }

    /// <summary>Media keys arrive here as WM_APPCOMMAND when the window has focus; otherwise through SMTC.</summary>
    private IntPtr Poruke(IntPtr hwnd, int poruka, IntPtr wParam, IntPtr lParam, ref bool obradjeno)
    {
        if (poruka != WmAppCommand) return IntPtr.Zero;

        int komanda = (int)((lParam.ToInt64() >> 16) & 0x0FFF);
        switch (komanda)
        {
            case AppCommandPlayPause:
            case AppCommandPlay:
            case AppCommandPause:
                _vm.Plejer.PustiPauza();
                break;
            case AppCommandNext:
                _vm.Plejer.Sledeca();
                break;
            case AppCommandPrevious:
                _vm.Plejer.Prethodna();
                break;
            case AppCommandStop:
                if (_vm.Plejer.Svira) _vm.Plejer.PustiPauza();
                break;
            default:
                return IntPtr.Zero;
        }
        obradjeno = true;
        return new IntPtr(1);
    }

    private void NaPromenuPlejera(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlejerVm.BojaTrake):
                AnimirajBoju(_vm.Plejer.BojaTrake);
                break;
            case nameof(PlejerVm.Svira):
            case nameof(PlejerVm.Mesanje):
            case nameof(PlejerVm.Ponavljanje):
                OsveziDugmad();
                break;
            case nameof(PlejerVm.Tekuca):
                Title = _vm.Plejer.Tekuca is { } p ? $"{p.Naslov} - {p.Izvodjac}" : PlejerVm.ImePrograma;
                break;
        }
    }

    private void AnimirajBoju(Color boja)
    {
        var trajanje = TimeSpan.FromMilliseconds(700);
        BojaLevo.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(boja, trajanje));
        var tamnija = Color.Multiply(boja, 0.55f);
        tamnija.A = 255;
        BojaSredina.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(tamnija, trajanje));
    }

    private void OsveziDugmad()
    {
        var p = _vm.Plejer;
        ThumbPusti.Description = p.Svira ? "Pauza" : "Pusti";
        ThumbPusti.ImageSource = (ImageSource)FindResource(p.Svira ? "IkonaPauza" : "IkonaPusti");
        Upali(DugmeMesanje, p.Mesanje);
        Upali(DugmePonavljanje, p.Ponavljanje != Ponavljanje.Ne);
        DugmePonavljanje.Content = p.Ponavljanje == Ponavljanje.Jedna ? "" : "";
        DugmeMesanje.ToolTip = p.Mesanje ? "Nasumično: uključeno" : "Nasumično: isključeno";
        DugmePonavljanje.ToolTip = p.Ponavljanje switch
        {
            Ponavljanje.Sve => "Ponovi sve",
            Ponavljanje.Jedna => "Ponovi pesmu",
            _ => "Ponavljanje: isključeno",
        };
    }

    /// <summary>A switched-on button is a filled orange circle, so its state reads at a glance.</summary>
    private void Upali(Button dugme, bool upaljeno)
    {
        if (upaljeno)
        {
            dugme.Background = _akcenat;
            dugme.Foreground = _tamno;
        }
        else
        {
            dugme.ClearValue(BackgroundProperty);
            dugme.Foreground = _bela;
        }
    }

    private void NaPromenuStanja()
    {
        // A maximized chromeless window overhangs the screen by its resize border.
        Koren.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        DugmeUvecaj.Content = WindowState == WindowState.Maximized ? "" : "";
        App.Trenutni.NaPromenuProzora(WindowState);
    }

    private void NaTaster(object sender, KeyEventArgs e)
    {
        bool uPolju = Keyboard.FocusedElement is TextBox;
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Pretraga.Focus();
            Pretraga.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && Keyboard.FocusedElement == Pretraga)
        {
            _vm.Upit = "";
            e.Handled = true;
        }
        else if (e.Key == Key.Space && !uPolju)
        {
            _vm.Plejer.PustiPauza();
            e.Handled = true;
        }
        else if (e.Key == Key.F1)
        {
            NaPomoc(this, e);
            e.Handled = true;
        }
    }

    private void NaPrevlacenjeProzora(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            NaUvecaj(sender, e);
            return;
        }
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void NaUmanji(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void NaUvecaj(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void NaZatvori(object sender, RoutedEventArgs e) => Close();

    private void NaZatvaranje(object? sender, CancelEventArgs e)
    {
        if (_vm.Dan.Aktivan && !Potvrda.Pitaj(this, "Dan je u toku. Zatvoriti plejer?", "Muzika prestaje da svira.", "Zatvori"))
        {
            e.Cancel = true;
            return;
        }
        _vm.Dan.Zaustavi();
        var p = _vm.Podesavanja;
        p.Uvecan = WindowState == WindowState.Maximized;
        var okvir = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (!okvir.IsEmpty)
        {
            p.Levo = okvir.Left;
            p.Gore = okvir.Top;
            p.Sirina = okvir.Width;
            p.Visina = okvir.Height;
        }
    }

    private void NaPodesavanja(object sender, RoutedEventArgs e) => new PodesavanjaProzor { Owner = this }.ShowDialog();

    private void NaDodajAlbum(object sender, RoutedEventArgs e)
    {
        var dijalog = new OpenFolderDialog { Title = "Izaberi folder sa albumom ili muzikom", Multiselect = true };
        if (dijalog.ShowDialog(this) == true) _ = _vm.DodajMuzikuAsync(dijalog.FolderNames);
    }

    private void NaDodajPesme(object sender, RoutedEventArgs e)
    {
        var dijalog = new OpenFileDialog
        {
            Title = "Izaberi pesme",
            Multiselect = true,
            Filter = "Muzika|*.mp3;*.flac;*.ogg;*.oga;*.opus;*.m4a;*.mp4;*.wav|Svi fajlovi|*.*",
        };
        if (dijalog.ShowDialog(this) == true) _ = _vm.DodajMuzikuAsync(dijalog.FileNames);
    }

    private void NaPrevlacenjeFajlova(object sender, DragEventArgs e)
    {
        // Only files from outside. Rows dragged inside a show are handled by the list itself.
        bool fajlovi = e.Data.GetDataPresent(DataFormats.FileDrop);
        if (fajlovi)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        // The queue well lights up, so it is clear where the songs will land.
        RedUsek.Osvetljeno = fajlovi;
    }

    private void NaNapustanjeFajlova(object sender, DragEventArgs e) => RedUsek.Osvetljeno = false;

    private void NaSpustanjeFajlova(object sender, DragEventArgs e)
    {
        RedUsek.Osvetljeno = false;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] putanje)
        {
            _ = _vm.DodajMuzikuAsync(putanje);
            e.Handled = true;
        }
    }

    private void NaNovaEmisija(object sender, RoutedEventArgs e)
    {
        var dijalog = new EmisijaDijalog { Owner = this };
        if (dijalog.ShowDialog() == true) _vm.NovaEmisija(dijalog.Ime, dijalog.Boja);
    }

    private void NaIzvodjac(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && e.RoutedEvent == MouseDoubleClickEvent) return;
        if (sender is ListBox { SelectedItem: IzvodjacStavka izvodjac } lista)
        {
            lista.SelectedItem = null;
            _vm.OtvoriIzvodjaca(izvodjac);
        }
    }

    private void NaStop(object sender, RoutedEventArgs e)
    {
        if (_vm.Dan.Aktivan)
        {
            if (Potvrda.Pitaj(this, "Zaustaviti dan?", "Muzika prestaje da svira.", "Zaustavi")) _vm.Dan.Zaustavi();
            return;
        }
        _vm.Plejer.Stop();
    }

    private void NaVerzije(object sender, RoutedEventArgs e) => new VerzijeProzor { Owner = this }.ShowDialog();

    private void NaPomoc(object sender, RoutedEventArgs e) => new PomocProzor { Owner = this }.ShowDialog();

    private void NaOProgramu(object sender, RoutedEventArgs e) => new OProgramuProzor { Owner = this }.ShowDialog();

    private static SlediStavka? SlediPod(object? izvor)
    {
        for (var e = izvor as DependencyObject; e != null; e = VisualTreeHelper.GetParent(e))
        {
            if (e is Button) return null;
            if (e is ListBoxItem { DataContext: SlediStavka s }) return s;
        }
        return null;
    }

    private void NaSlediUkloni(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is SlediStavka s) _vm.Plejer.UkloniIzReda([s]);
    }

    private void NaSlediDvoklik(object sender, MouseButtonEventArgs e)
    {
        if (SlediPod(e.OriginalSource) is { Tekuca: false } s) _vm.Plejer.SkociNa(s.Unapred);
    }

    private void NaSlediTaster(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && ListaSledi.SelectedItems.Count > 0)
        {
            _vm.Plejer.UkloniIzReda(ListaSledi.SelectedItems.Cast<SlediStavka>().ToList());
            e.Handled = true;
        }
    }

    private void NaSlediDesniKlik(object sender, MouseButtonEventArgs e)
    {
        if (SlediPod(e.OriginalSource) is not { } s) return;
        if (!ListaSledi.SelectedItems.Contains(s))
        {
            ListaSledi.SelectedItems.Clear();
            ListaSledi.SelectedItems.Add(s);
        }

        var meni = KontekstMeni.ZaRed(s, ListaSledi.SelectedItems.Cast<SlediStavka>().ToList());
        meni.PlacementTarget = ListaSledi;
        meni.IsOpen = true;
        e.Handled = true;
    }

    private void NaSacuvajRed(object sender, RoutedEventArgs e)
    {
        var dijalog = new EmisijaDijalog { Owner = this };
        if (dijalog.ShowDialog() == true) _vm.SacuvajRedKaoEmisiju(dijalog.Ime, dijalog.Boja);
    }

    private void NaIsprazniRed(object sender, RoutedEventArgs e) => _vm.Plejer.IsprazniRed();
}
