using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Bronxamp.Prikaz;

/// <summary>
/// Translates the plain texts in a window or a page: every literal that is not data bound goes through the
/// dictionary, both ways, so it is enough to run it again when the language changes. Bound values are left
/// alone — those are translated where they are made (<see cref="Rec.T(string)"/>).
/// </summary>
public static class PrevodSvega
{
    public static void Primeni(DependencyObject? koren)
    {
        if (koren == null) return;

        // The LCD shows digits it draws itself; nothing there is a text to translate.
        if (koren.GetType().Name == "LcdTekst") return;

        try
        {
            PrevediJedan(koren);
            foreach (var dete in Deca(koren)) Primeni(dete);
        }
        catch (Exception)
        {
            // A node that cannot be walked (a Run, a column) is simply skipped.
        }
    }

    private static void PrevediJedan(DependencyObject koren)
    {
        if (koren is TextBlock tb && !string.IsNullOrEmpty(tb.Text) && BindingOperations.GetBindingExpression(tb, TextBlock.TextProperty) == null)
        {
            var nov = Rec.T(tb.Text);
            if (nov != tb.Text) tb.Text = nov;
        }
        else if (koren is ContentControl { Content: string tekst } cc && BindingOperations.GetBindingExpression(cc, ContentControl.ContentProperty) == null)
        {
            var nov = Rec.T(tekst);
            if (nov != tekst) cc.Content = nov;
        }

        if (koren is FrameworkElement { ToolTip: string savet } fe)
        {
            var nov = Rec.T(savet);
            if (nov != savet) fe.ToolTip = nov;
        }
    }

    /// <summary>Windows and pages call this when they load; the app calls it again when the language changes.</summary>
    public static void PrimeniNaSve()
    {
        foreach (Window prozor in Application.Current.Windows) Primeni(prozor);
    }

    private static IEnumerable<DependencyObject> Deca(DependencyObject koren)
    {
        foreach (var dete in LogicalTreeHelper.GetChildren(koren).OfType<DependencyObject>()) yield return dete;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(koren); i++)
            yield return System.Windows.Media.VisualTreeHelper.GetChild(koren, i);
    }
}
