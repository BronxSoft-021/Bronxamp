using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Bronxamp.Prikaz;

/// <summary>Spectrum bars. Reads the FFT itself on a timer, and only while there is something to read.</summary>
public sealed class Spektar : FrameworkElement
{
    public static readonly DependencyProperty AktivanProperty = DependencyProperty.Register(
        nameof(Aktivan), typeof(bool), typeof(Spektar), new PropertyMetadata(false, (d, _) => ((Spektar)d).Podesi()));

    public static readonly DependencyProperty BrojStubicaProperty = DependencyProperty.Register(
        nameof(BrojStubica), typeof(int), typeof(Spektar), new PropertyMetadata(22));

    public static readonly DependencyProperty BojaProperty = DependencyProperty.Register(
        nameof(Boja), typeof(Brush), typeof(Spektar), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Colour at the top of the tallest bars; when set, the bars fade from <see cref="Boja"/> up into this.</summary>
    public static readonly DependencyProperty BojaVrhaProperty = DependencyProperty.Register(
        nameof(BojaVrha), typeof(Brush), typeof(Spektar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Small frequency marks (100, 1k, 10k...) under the bars, like on an equalizer.</summary>
    public static readonly DependencyProperty OznakeProperty = DependencyProperty.Register(
        nameof(Oznake), typeof(bool), typeof(Spektar), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The bars cover 20 Hz to 20 kHz on a logarithmic scale, as hearing does (31 bars = one third of an octave each).</summary>
    public const double NajnizaHz = 20, NajvisaHz = 20000;

    /// <summary>FFT4096 gives 2048 bins: about 11 Hz each, fine enough for the low bars.</summary>
    public const int VelicinaFft = 2048;

    private const double VisinaOznaka = 11;
    private static readonly (double Hz, string Naziv)[] Oznacene =
        [(50, "50"), (100, "100"), (250, "250"), (500, "500"), (1000, "1k"), (2000, "2k"), (5000, "5k"), (10000, "10k"), (20000, "20k")];

    private readonly DispatcherTimer _sat = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly float[] _fft = new float[VelicinaFft];
    private float[] _ciljevi = [];
    private float[] _stubici = [];
    private float[] _vrhovi = [];

    /// <summary>Fills the FFT buffer; false when nothing is playing.</summary>
    public static Func<float[], bool>? Izvor { get; set; }

    /// <summary>Sample rate of what the FFT reads, so a bin can be turned into hertz.</summary>
    public static Func<int>? Frekvencija { get; set; }

    public bool Oznake
    {
        get => (bool)GetValue(OznakeProperty);
        set => SetValue(OznakeProperty, value);
    }

    public bool Aktivan
    {
        get => (bool)GetValue(AktivanProperty);
        set => SetValue(AktivanProperty, value);
    }

    public int BrojStubica
    {
        get => (int)GetValue(BrojStubicaProperty);
        set => SetValue(BrojStubicaProperty, value);
    }

    public Brush Boja
    {
        get => (Brush)GetValue(BojaProperty);
        set => SetValue(BojaProperty, value);
    }

    public Brush? BojaVrha
    {
        get => (Brush?)GetValue(BojaVrhaProperty);
        set => SetValue(BojaVrhaProperty, value);
    }

    /// <summary>Sets the bars directly — for previews and tests (the timer keeps running when Aktivan).</summary>
    public void PostaviStubice(IReadOnlyList<double> nivoi)
    {
        _stubici = nivoi.Select(v => (float)Math.Clamp(v, 0, 1)).ToArray();
        InvalidateVisual();
    }

    public Spektar()
    {
        _sat.Tick += (_, _) => Otkucaj();
        IsVisibleChanged += (_, _) => Podesi();
        Unloaded += (_, _) => _sat.Stop();
        Loaded += (_, _) => Podesi();
    }

    private void Podesi()
    {
        // Keeps running after Aktivan goes false until the bars have fallen.
        if (IsVisible && (Aktivan || _stubici.Any(s => s > 0.01f))) _sat.Start();
        else _sat.Stop();
    }

    private void Otkucaj()
    {
        int n = BrojStubica;
        if (_stubici.Length != n) { _stubici = new float[n]; _vrhovi = new float[n]; _ciljevi = new float[n]; }

        bool ima = Aktivan && Izvor?.Invoke(_fft) == true;
        if (ima) Pojasevi(_fft, Frekvencija?.Invoke() ?? 44100, _ciljevi);
        for (int i = 0; i < n; i++)
        {
            float cilj = ima ? _ciljevi[i] : 0;
            // Quick rise, slow fall.
            _stubici[i] = cilj > _stubici[i] ? cilj : Math.Max(cilj, _stubici[i] - 0.045f);
            // The top of each band floats above the bar and falls more slowly, like the peak on an equalizer.
            _vrhovi[i] = cilj > _vrhovi[i] ? cilj : Math.Max(cilj, _vrhovi[i] - 0.012f);
        }

        InvalidateVisual();
        if (!Aktivan && _stubici.All(s => s <= 0.01f)) _sat.Stop();
    }

    /// <summary>Lower edge of bar i, in hertz.</summary>
    public static double GranicaHz(int i, int n) => NajnizaHz * Math.Pow(NajvisaHz / NajnizaHz, i / (double)n);

    /// <summary>
    /// Heights 0..1 for all bars from an FFT of <paramref name="fft"/>.Length bins (half the FFT size).
    /// Bar i covers <see cref="GranicaHz"/>(i) to (i+1). A bar narrower than one bin reads the bin under its middle,
    /// so the lowest bars move on their own and do not copy each other.
    /// </summary>
    public static void Pojasevi(float[] fft, int frekvencija, float[] izlaz)
    {
        int n = izlaz.Length;
        double hzPoBinu = frekvencija / (2.0 * fft.Length);
        for (int i = 0; i < n; i++)
        {
            double od = GranicaHz(i, n), @do = GranicaHz(i + 1, n);
            int prvi = Math.Max(1, (int)Math.Round(od / hzPoBinu));
            int poslednji = Math.Min(fft.Length - 1, (int)Math.Round(@do / hzPoBinu) - 1);
            if (poslednji < prvi) prvi = poslednji = Math.Clamp((int)Math.Round(Math.Sqrt(od * @do) / hzPoBinu), 1, fft.Length - 1);

            float vrh = 0;
            for (int k = prvi; k <= poslednji; k++) vrh = Math.Max(vrh, fft[k]);

            // Music has much less energy up high, so each octave above 1 kHz gets a little lift (and below, a little less).
            double db = 20 * Math.Log10(Math.Max(vrh, 1e-6f)) + 3 * Math.Log2(Math.Sqrt(od * @do) / 1000);
            izlaz[i] = (float)Math.Clamp((db + 62) / 56, 0, 1);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        int n = _stubici.Length == 0 ? BrojStubica : _stubici.Length;
        double sirina = ActualWidth / n, visina = ActualHeight - (Oznake ? VisinaOznaka : 0);
        if (visina <= 0) return;
        if (Oznake) CrtajOznake(dc, visina);
        double stubic = Math.Max(1, sirina * 0.62);
        double zaobljenje = Math.Min(stubic / 2, 1.6);

        // Taller bars run into the hot colour, so the spectrum itself has a few shades (not the text colour).
        Brush cetka = Boja;
        if (BojaVrha is { } vrh && vrh is SolidColorBrush gornja && Boja is SolidColorBrush donja)
        {
            var preliv = new LinearGradientBrush
            {
                StartPoint = new Point(0, 1),
                EndPoint = new Point(0, 0),
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
            };
            preliv.GradientStops.Add(new GradientStop(donja.Color, 0));
            preliv.GradientStops.Add(new GradientStop(donja.Color, 0.55));
            preliv.GradientStops.Add(new GradientStop(gornja.Color, 1));
            preliv.Freeze();
            cetka = preliv;
        }

        Brush? kapica = BojaVrha ?? Boja;

        for (int i = 0; i < n; i++)
        {
            double nivo = i < _stubici.Length ? _stubici[i] : 0;
            double h = Math.Max(2, nivo * visina);
            dc.DrawRoundedRectangle(cetka, null, new Rect(i * sirina, visina - h, stubic, h), zaobljenje, zaobljenje);

            // Floating top: a short cap a little above the bar, so the bands move freely like on an equalizer.
            if (_vrhovi.Length == n && _vrhovi[i] > nivo + 0.02)
            {
                double hv = Math.Max(2, _vrhovi[i] * visina);
                dc.DrawRoundedRectangle(kapica, null, new Rect(i * sirina, visina - hv - 2, stubic, 2), 1, 1);
            }
        }
    }

    private static readonly Brush BojaOznaka = Zamrzni(new SolidColorBrush(Color.FromArgb(0xB0, 0xEF, 0xEB, 0xE0)));
    private static readonly Typeface SlovaOznaka = new("Segoe UI");

    private static Brush Zamrzni(Brush b)
    {
        b.Freeze();
        return b;
    }

    /// <summary>Hertz marks under the bars, placed where that frequency falls on the log scale.</summary>
    private void CrtajOznake(DrawingContext dc, double dno)
    {
        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double raspon = Math.Log(NajvisaHz / NajnizaHz);
        foreach (var (hz, naziv) in Oznacene)
        {
            var tekst = new FormattedText(naziv, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SlovaOznaka, 9, BojaOznaka, ppd);
            double x = ActualWidth * Math.Log(hz / NajnizaHz) / raspon - tekst.Width / 2;
            dc.DrawText(tekst, new Point(Math.Clamp(x, 0, ActualWidth - tekst.Width), dno + 0.5));
        }
    }
}

/// <summary>
/// Tiny dots drifting behind the „on air“ panel — the cheapest way to make it feel alive
/// (RadioBOSS does the same in its On Air bar). Stops when hidden or when nothing plays.
/// </summary>
public sealed class Prah : FrameworkElement
{
    public static readonly DependencyProperty AktivanProperty = DependencyProperty.Register(
        nameof(Aktivan), typeof(bool), typeof(Prah), new PropertyMetadata(false, (d, _) => ((Prah)d).Podesi()));

    public static readonly DependencyProperty BojaProperty = DependencyProperty.Register(
        nameof(Boja), typeof(Brush), typeof(Prah),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0xEF, 0xEB, 0xE0)), FrameworkPropertyMetadataOptions.AffectsRender));

    private const int Koliko = 110;
    private readonly DispatcherTimer _sat = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly Random _slucajno = new();
    private readonly double[] _x = new double[Koliko];
    private readonly double[] _y = new double[Koliko];
    private readonly double[] _vx = new double[Koliko];
    private readonly double[] _vy = new double[Koliko];
    private readonly double[] _alfa = new double[Koliko];
    private bool _posejano;

    public bool Aktivan
    {
        get => (bool)GetValue(AktivanProperty);
        set => SetValue(AktivanProperty, value);
    }

    public Brush Boja
    {
        get => (Brush)GetValue(BojaProperty);
        set => SetValue(BojaProperty, value);
    }

    public Prah()
    {
        _sat.Tick += (_, _) => Otkucaj();
        IsVisibleChanged += (_, _) => Podesi();
        Loaded += (_, _) => Podesi();
        Unloaded += (_, _) => _sat.Stop();
    }

    private void Posej()
    {
        for (int i = 0; i < Koliko; i++)
        {
            _x[i] = _slucajno.NextDouble();
            _y[i] = _slucajno.NextDouble();
            _vx[i] = (_slucajno.NextDouble() - 0.5) * 0.0012;
            _vy[i] = -0.0004 - _slucajno.NextDouble() * 0.0011;   // slowly upwards
            _alfa[i] = 0.10 + _slucajno.NextDouble() * 0.35;
        }
        _posejano = true;
    }

    private void Podesi()
    {
        if (IsVisible && Aktivan)
        {
            if (!_posejano) Posej();
            _sat.Start();
        }
        else _sat.Stop();
    }

    private void Otkucaj()
    {
        for (int i = 0; i < Koliko; i++)
        {
            _x[i] += _vx[i];
            _y[i] += _vy[i];
            if (_y[i] < -0.02) { _y[i] = 1.02; _x[i] = _slucajno.NextDouble(); }
            if (_x[i] < -0.01) _x[i] = 1.01;
            else if (_x[i] > 1.01) _x[i] = -0.01;
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!_posejano) Posej();
        double sirina = ActualWidth, visina = ActualHeight;
        if (sirina < 4 || visina < 4) return;

        Color osnova = Boja is SolidColorBrush s ? s.Color : Colors.White;
        for (int i = 0; i < Koliko; i++)
        {
            var cetka = new SolidColorBrush(Color.FromArgb((byte)(_alfa[i] * 255), osnova.R, osnova.G, osnova.B));
            cetka.Freeze();
            dc.DrawRectangle(cetka, null, new Rect(_x[i] * sirina, _y[i] * visina, 1.6, 1.6));
        }
    }
}

/// <summary>
/// Horizontal stereo level meter. Green up to −3 dB, then a soft fade into red over the last 3 dB
/// (a gradient, not a hard line). The peak holds for a moment and then falls.
/// </summary>
public sealed class NivoMetar : FrameworkElement
{
    /// <summary>Reads the current output level (0..1 per channel); false when nothing plays.</summary>
    public delegate bool NivoIzvor(out double levo, out double desno);

    public static NivoIzvor? Izvor { get; set; }

    /// <summary>Left end of the scale.</summary>
    public const double NajniziDb = -60;

    /// <summary>Where red begins.</summary>
    public const double CrvenaOdDb = -3;

    /// <summary>Full scale (right end).</summary>
    public const double CrvenaDoDb = 0;

    public static readonly DependencyProperty AktivanProperty = DependencyProperty.Register(
        nameof(Aktivan), typeof(bool), typeof(NivoMetar), new PropertyMetadata(false, (d, _) => ((NivoMetar)d).Podesi()));

    public static readonly DependencyProperty BojaProperty = DependencyProperty.Register(
        nameof(Boja), typeof(Brush), typeof(NivoMetar),
        new FrameworkPropertyMetadata(Kist("#3FBF6F"), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BojaUpozorenjaProperty = DependencyProperty.Register(
        nameof(BojaUpozorenja), typeof(Brush), typeof(NivoMetar),
        new FrameworkPropertyMetadata(Kist("#E8B33A"), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BojaCrvenaProperty = DependencyProperty.Register(
        nameof(BojaCrvena), typeof(Brush), typeof(NivoMetar),
        new FrameworkPropertyMetadata(Kist("#E2504E"), FrameworkPropertyMetadataOptions.AffectsRender));

    private static Brush Kist(string hex)
    {
        var kist = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        kist.Freeze();
        return kist;
    }

    private const double PadDbUSekundi = 26;    // how quickly the bar falls after a tone
    private const double VrhDrziSekundi = 1.2;  // how long the peak stays

    private readonly DispatcherTimer _sat = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly double[] _nivo = new double[2];
    private readonly double[] _vrh = new double[2];
    private readonly long[] _vrhDo = new long[2];
    private readonly Stopwatch _vreme = Stopwatch.StartNew();

    public bool Aktivan
    {
        get => (bool)GetValue(AktivanProperty);
        set => SetValue(AktivanProperty, value);
    }

    public Brush Boja
    {
        get => (Brush)GetValue(BojaProperty);
        set => SetValue(BojaProperty, value);
    }

    public Brush BojaUpozorenja
    {
        get => (Brush)GetValue(BojaUpozorenjaProperty);
        set => SetValue(BojaUpozorenjaProperty, value);
    }

    public Brush BojaCrvena
    {
        get => (Brush)GetValue(BojaCrvenaProperty);
        set => SetValue(BojaCrvenaProperty, value);
    }

    public NivoMetar()
    {
        _sat.Tick += (_, _) => Otkucaj();
        IsVisibleChanged += (_, _) => Podesi();
        Loaded += (_, _) => Podesi();
        Unloaded += (_, _) => _sat.Stop();
    }

    /// <summary>Sets the bars directly — for previews and tests (the timer still runs when Aktivan).</summary>
    public void PostaviNivo(double levo, double desno)
    {
        _nivo[0] = Math.Clamp(levo, 0, 1);
        _nivo[1] = Math.Clamp(desno, 0, 1);
        _vrh[0] = _nivo[0];
        _vrh[1] = _nivo[1];
        InvalidateVisual();
    }

    /// <summary>0..1 place of a dB value on the scale (NajniziDb = 0, 0 dB = 1).</summary>
    public static double Deo(double db) => Math.Clamp((db - NajniziDb) / -NajniziDb, 0, 1);

    /// <summary>Where the red zone starts and ends on the scale, in 0..1 units (0.95 … 1 for −3…0 dB).</summary>
    public static (double Od, double Do) CrvenaZona() => (Deo(CrvenaOdDb), Deo(CrvenaDoDb));

    /// <summary>Bar colour at one place: green, fading through amber into red for the last 3 dB.</summary>
    public static Color BojaNa(double deo, Color niska, Color srednja, Color visoka)
    {
        double od = Deo(CrvenaOdDb);
        double blagi = Math.Max(0, od - 0.09);   // green holds until here, then the fade starts
        if (deo >= 1) return visoka;
        if (deo >= od) return Pomesaj(srednja, visoka, (deo - od) / Math.Max(0.0001, 1 - od));
        if (deo <= blagi) return niska;
        return Pomesaj(niska, srednja, (deo - blagi) / Math.Max(0.0001, od - blagi));
    }

    private static Color Pomesaj(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    }

    /// <summary>Colour of a (solid) brush; theme brushes are solid, so a cast is safe enough.</summary>
    private static Color BojaKiste(Brush kist) => kist is SolidColorBrush s ? s.Color : Colors.White;

    private void Podesi()
    {
        if (IsVisible && (Aktivan || _nivo[0] > 0.001 || _nivo[1] > 0.001)) _sat.Start();
        else _sat.Stop();
    }

    private void Otkucaj()
    {
        double levo = 0, desno = 0;
        bool ima = Aktivan && Izvor != null && Izvor(out levo, out desno);
        Span<double> ulaz = [(ima ? levo : 0), (ima ? desno : 0)];
        double pad = PadDbUSekundi * _sat.Interval.TotalSeconds / -NajniziDb;
        for (int k = 0; k < 2; k++)
        {
            double cilj = Deo(20 * Math.Log10(Math.Max(ulaz[k], 1e-5)));
            _nivo[k] = cilj >= _nivo[k] ? cilj : Math.Max(cilj, _nivo[k] - pad);
            if (cilj >= _vrh[k])
            {
                _vrh[k] = cilj;
                _vrhDo[k] = _vreme.ElapsedMilliseconds + (long)(VrhDrziSekundi * 1000);
            }
            else if (_vreme.ElapsedMilliseconds > _vrhDo[k])
            {
                _vrh[k] = Math.Max(cilj, _vrh[k] - pad);
            }
        }

        InvalidateVisual();
        if (!ima && _nivo[0] <= 0.001 && _nivo[1] <= 0.001) _sat.Stop();
    }

    /// <summary>Green → amber → red across the whole width, so the pass into red is a soft fade.</summary>
    private LinearGradientBrush Preliv()
    {
        var (od, kraj) = CrvenaZona();
        double blagi = Math.Max(0, od - 0.09);
        var kist = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
        };
        kist.GradientStops.Add(new GradientStop(BojaKiste(Boja), 0));
        kist.GradientStops.Add(new GradientStop(BojaKiste(Boja), blagi));
        kist.GradientStops.Add(new GradientStop(BojaKiste(BojaUpozorenja), od));
        kist.GradientStops.Add(new GradientStop(BojaKiste(BojaCrvena), kraj));
        kist.Freeze();
        return kist;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double visina = ActualHeight, sirina = ActualWidth;
        if (visina < 6 || sirina < 12) return;

        double razmak = Math.Max(2, visina * 0.12);
        double red = (visina - razmak) / 2;
        var (od, kraj) = CrvenaZona();
        var podloga = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255));
        var zona = new SolidColorBrush(Color.FromArgb(40, BojaKiste(BojaCrvena).R, BojaKiste(BojaCrvena).G, BojaKiste(BojaCrvena).B));
        var vrhBoja = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255));
        podloga.Freeze();
        zona.Freeze();
        vrhBoja.Freeze();
        var preliv = Preliv();

        for (int k = 0; k < 2; k++)
        {
            double y = k * (red + razmak);
            double ugao = red / 2;
            var staza = new RectangleGeometry(new Rect(0, y, sirina, red), ugao, ugao);
            dc.DrawRoundedRectangle(podloga, null, new Rect(0, y, sirina, red), ugao, ugao);
            // Red zone is part of the track, not a separate pill: clipped to the track's rounded shape.
            dc.PushClip(staza);
            dc.DrawRectangle(zona, null, new Rect(sirina * od, y, sirina * (kraj - od), red));
            dc.Pop();

            double deo = _nivo[k];
            if (deo > 0.002)
            {
                var ispunjeno = new Rect(0, y, Math.Max(2, sirina * deo), red);
                dc.PushClip(new RectangleGeometry(ispunjeno, ugao, ugao));
                dc.DrawRectangle(preliv, null, new Rect(0, y, sirina, red));
                dc.Pop();
            }

            double vrh = _vrh[k];
            if (vrh > 0.004)
            {
                double x = Math.Clamp(sirina * vrh - 1, 0, Math.Max(0, sirina - 2));
                dc.DrawRectangle(vrh >= od ? BojaCrvena : vrhBoja, null, new Rect(x, y, 2, red));
            }
        }
    }
}

/// <summary>Thin progress line. Click or drag to seek.</summary>
public sealed class TrakaNapretka : FrameworkElement
{
    public static readonly DependencyProperty OdnosProperty = DependencyProperty.Register(
        nameof(Odnos), typeof(double), typeof(TrakaNapretka), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VisinaLinijeProperty = DependencyProperty.Register(
        nameof(VisinaLinije), typeof(double), typeof(TrakaNapretka), new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush Pozadina = Zamrzni(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x33)));
    private static readonly Brush Popuna = Zamrzni(new SolidColorBrush(Color.FromRgb(0xE8, 0x76, 0x3A)));

    private double? _prevlacenje;

    public event Action<double>? Premotano;

    private static Brush Zamrzni(Brush b)
    {
        b.Freeze();
        return b;
    }

    public double Odnos
    {
        get => (double)GetValue(OdnosProperty);
        set => SetValue(OdnosProperty, value);
    }

    public double VisinaLinije
    {
        get => (double)GetValue(VisinaLinijeProperty);
        set => SetValue(VisinaLinijeProperty, value);
    }

    public static readonly DependencyProperty SamoDesniKlikProperty = DependencyProperty.Register(
        nameof(SamoDesniKlik), typeof(bool), typeof(TrakaNapretka), new PropertyMetadata(false));

    /// <summary>While a day is on air a stray left click must not move the song.</summary>
    public bool SamoDesniKlik
    {
        get => (bool)GetValue(SamoDesniKlikProperty);
        set => SetValue(SamoDesniKlikProperty, value);
    }

    public TrakaNapretka() => Cursor = Cursors.Hand;

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        Premotano?.Invoke(Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1));
        e.Handled = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double y = (ActualHeight - VisinaLinije) / 2;
        // Transparent full-height rectangle, so the whole element takes clicks and not only the thin line.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        dc.DrawRectangle(Pozadina, null, new Rect(0, y, ActualWidth, VisinaLinije));
        double odnos = Math.Clamp(_prevlacenje ?? Odnos, 0, 1);
        dc.DrawRectangle(Popuna, null, new Rect(0, y, ActualWidth * odnos, VisinaLinije));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (SamoDesniKlik) return;
        CaptureMouse();
        _prevlacenje = Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_prevlacenje == null) return;
        _prevlacenje = Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1);
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_prevlacenje is not double odnos) return;
        _prevlacenje = null;
        ReleaseMouseCapture();
        Premotano?.Invoke(odnos);
        InvalidateVisual();
    }
}

/// <summary>LCD digits with the unlit segments faintly visible behind them, as on a real display.</summary>
public sealed class LcdTekst : Grid
{
    public static readonly DependencyProperty TekstProperty = DependencyProperty.Register(
        nameof(Tekst), typeof(string), typeof(LcdTekst), new PropertyMetadata("", (d, e) => ((LcdTekst)d).Osvezi()));

    private readonly TextBlock _duh = new();
    private readonly TextBlock _cifre = new();

    public LcdTekst()
    {
        Children.Add(_duh);
        Children.Add(_cifre);
        Loaded += (_, _) => Stil();
    }

    public string Tekst
    {
        get => (string)GetValue(TekstProperty);
        set => SetValue(TekstProperty, value);
    }

    public double Velicina { get; set; } = 40;
    public bool Bold { get; set; } = true;
    public Brush? Boja { get; set; }

    private void Stil()
    {
        foreach (var t in new[] { _duh, _cifre })
        {
            t.FontFamily = (FontFamily)FindResource("Lcd7");
            t.FontSize = Velicina;
            t.FontWeight = Bold ? FontWeights.Bold : FontWeights.Normal;
        }
        _duh.Foreground = (Brush)FindResource("LcdDuh");
        _cifre.Foreground = Boja ?? (Brush)FindResource("Krem");
    }

    private void Osvezi()
    {
        var tekst = Tekst ?? "";
        _cifre.Text = tekst;
        _duh.Text = new string(tekst.Select(c => char.IsDigit(c) ? '8' : c == '-' ? '8' : c).ToArray());
    }
}

public sealed class HexUBoju : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value as string ?? "#E8763A"));
        }
        catch (FormatException)
        {
            return Brushes.Gray;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Visible when the value is not null and not an empty string. "obrnuto" inverts it.</summary>
public sealed class ImaUVidljivost : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool ima = value is string s ? s.Length > 0 : value != null;
        if (value is bool b) ima = b;
        if (value is int i) ima = i != 0;
        if (parameter as string == "obrnuto") ima = !ima;
        return ima ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class JednakoUBool : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Enum.Parse(targetType, (string)parameter!) : Binding.DoNothing;
}
