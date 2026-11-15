using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Bronxamp.Sistem;

public static class ProzorStil
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atribut, ref int vrednost, int velicina);

    /// <summary>Windows 11 rounds the corners itself, without the cost of a transparent WPF window.</summary>
    public static void ZaobljeneIvice(Window prozor)
    {
        var hwnd = new WindowInteropHelper(prozor).Handle;
        int vrednost = DwmwcpRound;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref vrednost, sizeof(int));
    }
}
