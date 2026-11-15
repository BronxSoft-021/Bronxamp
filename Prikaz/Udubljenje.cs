using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Bronxamp.Prikaz;

/// <summary>
/// The place where songs go in: a rounded area whose floor fades smoothly from black at the top into the panel
/// grey at the bottom. Nothing is drawn over the content, so the rows stay untouched and sharp.
/// </summary>
public sealed class Udubljenje : Decorator
{
    public static readonly DependencyProperty ZaobljenjeProperty = DependencyProperty.Register(
        nameof(Zaobljenje), typeof(double), typeof(Udubljenje),
        new FrameworkPropertyMetadata(10d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PozadinaProperty = DependencyProperty.Register(
        nameof(Pozadina), typeof(Brush), typeof(Udubljenje),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IvicaProperty = DependencyProperty.Register(
        nameof(Ivica), typeof(Brush), typeof(Udubljenje),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Lit while music is being dragged over the window: the edge glows in the accent colour.</summary>
    public static readonly DependencyProperty OsvetljenoProperty = DependencyProperty.Register(
        nameof(Osvetljeno), typeof(bool), typeof(Udubljenje),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnutrasnjeProperty = DependencyProperty.Register(
        nameof(Unutrasnje), typeof(Thickness), typeof(Udubljenje),
        new FrameworkPropertyMetadata(new Thickness(4), FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>The floor: black at the far (top) end, panel grey at the near (bottom) end.</summary>
    public static readonly LinearGradientBrush PodrazumevanaPozadina = new(
        [
            new GradientStop(Color.FromRgb(0x00, 0x00, 0x00), 0),
            new GradientStop(Color.FromRgb(0x12, 0x12, 0x18), 0.55),
            new GradientStop(Color.FromRgb(0x22, 0x22, 0x2B), 1),
        ], new Point(0, 0), new Point(0, 1));

    private static readonly Brush PodIvica = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x2F));
    private static readonly Brush PodAkcenat = new SolidColorBrush(Color.FromRgb(0xE8, 0x76, 0x3A));

    static Udubljenje()
    {
        PodrazumevanaPozadina.Freeze();
        PodIvica.Freeze();
        PodAkcenat.Freeze();
    }

    public double Zaobljenje
    {
        get => (double)GetValue(ZaobljenjeProperty);
        set => SetValue(ZaobljenjeProperty, value);
    }

    public Brush? Pozadina
    {
        get => (Brush?)GetValue(PozadinaProperty);
        set => SetValue(PozadinaProperty, value);
    }

    public Brush? Ivica
    {
        get => (Brush?)GetValue(IvicaProperty);
        set => SetValue(IvicaProperty, value);
    }

    public bool Osvetljeno
    {
        get => (bool)GetValue(OsvetljenoProperty);
        set => SetValue(OsvetljenoProperty, value);
    }

    public Thickness Unutrasnje
    {
        get => (Thickness)GetValue(UnutrasnjeProperty);
        set => SetValue(UnutrasnjeProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) return new Size(0, 0);
        Child.Measure(new Size(
            Math.Max(0, constraint.Width - Unutrasnje.Left - Unutrasnje.Right),
            Math.Max(0, constraint.Height - Unutrasnje.Top - Unutrasnje.Bottom)));
        return new Size(
            Child.DesiredSize.Width + Unutrasnje.Left + Unutrasnje.Right,
            Child.DesiredSize.Height + Unutrasnje.Top + Unutrasnje.Bottom);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(
            Unutrasnje.Left,
            Unutrasnje.Top,
            Math.Max(0, arrangeSize.Width - Unutrasnje.Left - Unutrasnje.Right),
            Math.Max(0, arrangeSize.Height - Unutrasnje.Top - Unutrasnje.Bottom)));
        return arrangeSize;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 1 || h <= 1) return;

        double r = Math.Max(0, Zaobljenje);
        var okvir = new Rect(0.5, 0.5, w - 1, h - 1);

        dc.DrawRoundedRectangle(Pozadina ?? PodrazumevanaPozadina, null, okvir, r, r);

        var ivica = Osvetljeno ? PodAkcenat : Ivica ?? PodIvica;
        dc.DrawRoundedRectangle(null, new Pen(ivica, Osvetljeno ? 1.4 : 1), okvir, r, r);
    }
}
