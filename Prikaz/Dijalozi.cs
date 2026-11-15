using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Bronxamp.Prikaz;

/// <summary>Shared look of the small dark dialogs.</summary>
public abstract class TamniDijalog : Window
{
    protected TamniDijalog(double sirina)
    {
        Width = sirina;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.FindResource("Panel");
        BorderBrush = (Brush)Application.Current.FindResource("PovrsinaHover");
        BorderThickness = new Thickness(1);
        FontFamily = new FontFamily("Segoe UI");
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) DialogResult = false;
        };
        MouseLeftButtonDown += (_, _) => DragMove();
        SourceInitialized += (_, _) => Sistem.ProzorStil.ZaobljeneIvice(this);
    }

    protected static TextBlock Tekst(string tekst, double velicina, string boja = "Tekst", bool bold = false) => new()
    {
        Text = tekst,
        FontSize = velicina,
        Foreground = (Brush)Application.Current.FindResource(boja),
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>The X in the corner, so a dialog can always be left the usual way.</summary>
    protected Button DugmeZatvori()
    {
        var b = new Button
        {
            Content = "\uE8BB",
            Style = (Style)Application.Current.FindResource("DugmeZatvori"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = "Zatvori",
        };
        b.Click += (_, _) => DialogResult = false;
        return b;
    }

    protected static Button Dugme(string tekst, bool glavno, RoutedEventHandler klik)
    {
        var b = new Button
        {
            Content = tekst,
            Style = (Style)Application.Current.FindResource(glavno ? "DugmeGlavno" : "Dugme"),
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 90,
            IsDefault = glavno,
        };
        b.Click += klik;
        return b;
    }
}

public sealed class Potvrda : TamniDijalog
{
    private Potvrda(string pitanje, string opis, string potvrda) : base(440)
    {
        var dugmad = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        dugmad.Children.Add(Dugme("Otkaži", false, (_, _) => DialogResult = false));
        dugmad.Children.Add(Dugme(potvrda, true, (_, _) => DialogResult = true));

        var sadrzaj = new StackPanel { Margin = new Thickness(24) };
        sadrzaj.Children.Add(Tekst(pitanje, 17, bold: true));
        var o = Tekst(opis, 13, "Mutan");
        o.Margin = new Thickness(0, 8, 0, 0);
        sadrzaj.Children.Add(o);
        sadrzaj.Children.Add(dugmad);
        Content = sadrzaj;
    }

    public static bool Pitaj(Window vlasnik, string pitanje, string opis, string potvrda = "Obriši") =>
        new Potvrda(pitanje, opis, potvrda) { Owner = vlasnik }.ShowDialog() == true;
}

/// <summary>"Šta je novo": all versions, newest on top, as in the other apps.</summary>
public sealed class VerzijeProzor : TamniDijalog
{
    public VerzijeProzor() : base(640)
    {
        var spisak = new StackPanel { Margin = new Thickness(26, 4, 16, 22) };
        foreach (var v in Verzije.Sve)
        {
            var naslov = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 6) };
            naslov.Children.Add(Tekst(v.Oznaka, 15, bold: true));
            foreach (var ime in v.Vrste)
            {
                var vrsta = new Border
                {
                    Background = (Brush)Application.Current.FindResource(ime == "Dorada" ? "Povrsina" : "Akcenat"),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(9, 1, 9, 2),
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var oznaka = Tekst(ime, 11);
                if (ime != "Dorada") oznaka.Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x12, 0x06));
                vrsta.Child = oznaka;
                naslov.Children.Add(vrsta);
            }
            var datum = Tekst(v.Datum, 12, "Mutan");
            datum.Margin = new Thickness(10, 0, 0, 0);
            datum.VerticalAlignment = VerticalAlignment.Center;
            naslov.Children.Add(datum);
            spisak.Children.Add(naslov);

            foreach (var izmena in v.Izmene)
            {
                var red = Tekst("•  " + izmena, 13);
                red.Margin = new Thickness(2, 0, 0, 4);
                spisak.Children.Add(red);
            }
        }

        var logo = new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Bronxamp;component/assets/logo_bronxamp.png")),
            Stretch = Stretch.Uniform,
        };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);

        var slika = new Grid();
        slika.Children.Add(logo);
        slika.Children.Add(DugmeZatvori());

        var natpis = Tekst("Šta je novo", 19, bold: true);
        natpis.Margin = new Thickness(26, 14, 0, 0);

        var sadrzaj = new DockPanel();
        DockPanel.SetDock(slika, Dock.Top);
        DockPanel.SetDock(natpis, Dock.Top);
        sadrzaj.Children.Add(slika);
        sadrzaj.Children.Add(natpis);
        sadrzaj.Children.Add(new ScrollViewer { Content = spisak, MaxHeight = 460, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = sadrzaj;
    }
}

/// <summary>Picks one jingle out of all of them, including those the program never plays on its own.</summary>
public sealed class IzborJingla : TamniDijalog
{
    private readonly IReadOnlyList<Vm.IzborFajla> _svi;
    private readonly ListBox _lista;

    public Vm.IzborFajla? Izabran => _lista.SelectedItem as Vm.IzborFajla;

    public IzborJingla(IReadOnlyList<Vm.IzborFajla> jinglovi) : base(520)
    {
        _svi = jinglovi;
        var pretraga = new TextBox { Style = (Style)Application.Current.FindResource("PoljeZaUnos"), Margin = new Thickness(0, 12, 0, 8) };
        _lista = new ListBox { Style = (Style)Application.Current.FindResource("Lista"), Height = 380, DisplayMemberPath = "Naziv", ItemsSource = _svi };
        pretraga.TextChanged += (_, _) =>
        {
            var deo = pretraga.Text.Trim();
            _lista.ItemsSource = deo.Length == 0 ? _svi : _svi.Where(j => j.Naziv.Contains(deo, StringComparison.CurrentCultureIgnoreCase)).ToList();
        };
        _lista.MouseDoubleClick += (_, _) => { if (Izabran != null) DialogResult = true; };

        var dugmad = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        dugmad.Children.Add(Dugme("Otkaži", false, (_, _) => DialogResult = false));
        dugmad.Children.Add(Dugme("Dodaj", true, (_, _) => { if (Izabran != null) DialogResult = true; }));

        var sadrzaj = new StackPanel { Margin = new Thickness(24) };
        sadrzaj.Children.Add(Tekst("Izaberi jingl", 17, bold: true));
        sadrzaj.Children.Add(pretraga);
        sadrzaj.Children.Add(_lista);
        sadrzaj.Children.Add(dugmad);
        Content = sadrzaj;
        Loaded += (_, _) => pretraga.Focus();
    }
}

public sealed class EmisijaDijalog : TamniDijalog
{
    private static readonly string[] Boje =
        ["#6C5CE7", "#E8763A", "#E8B33A", "#3FB27F", "#D9455F", "#3A9BD9", "#B55CE7", "#9A978F"];

    private readonly TextBox _ime;
    private readonly List<ToggleButton> _boje = [];

    public string Ime => _ime.Text.Trim();
    public string Boja { get; private set; }

    public EmisijaDijalog(string ime = "", string? boja = null) : base(420)
    {
        Boja = boja ?? Boje[0];
        _ime = new TextBox { Text = ime, Style = (Style)Application.Current.FindResource("PoljeZaUnos"), Margin = new Thickness(0, 12, 0, 0) };

        var paleta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        foreach (var b in Boje)
            paleta.Children.Add(Uzorak(b));

        var potvrdi = Dugme("Sačuvaj", true, (_, _) => { if (Ime.Length > 0) DialogResult = true; });
        var dugmad = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        dugmad.Children.Add(Dugme("Otkaži", false, (_, _) => DialogResult = false));
        dugmad.Children.Add(potvrdi);

        var sadrzaj = new StackPanel { Margin = new Thickness(24) };
        sadrzaj.Children.Add(Tekst(ime.Length == 0 ? "Nova emisija" : "Izmeni emisiju", 17, bold: true));
        sadrzaj.Children.Add(_ime);
        sadrzaj.Children.Add(paleta);
        sadrzaj.Children.Add(dugmad);
        Content = sadrzaj;

        Loaded += (_, _) =>
        {
            _ime.Focus();
            _ime.SelectAll();
        };
    }

    private ToggleButton Uzorak(string boja)
    {
        var krug = new Ellipse { Width = 24, Height = 24, Fill = (Brush)new BrushConverter().ConvertFromString(boja)! };
        var uzorak = new ToggleButton { Content = krug, Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand, IsChecked = boja == Boja, Focusable = false };

        var sablon = new ControlTemplate(typeof(ToggleButton));
        var ivica = new FrameworkElementFactory(typeof(Border), "Ivica");
        ivica.SetValue(Border.PaddingProperty, new Thickness(3));
        ivica.SetValue(Border.CornerRadiusProperty, new CornerRadius(100));
        ivica.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        ivica.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        ivica.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        sablon.VisualTree = ivica;
        var izabrano = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        izabrano.Setters.Add(new Setter(Border.BorderBrushProperty, Brushes.White, "Ivica"));
        sablon.Triggers.Add(izabrano);
        uzorak.Template = sablon;

        uzorak.Click += (_, _) =>
        {
            Boja = boja;
            foreach (var b in _boje) b.IsChecked = b == uzorak;
        };
        _boje.Add(uzorak);
        return uzorak;
    }
}
