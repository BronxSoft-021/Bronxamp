using System.Security.Cryptography;
using System.Text;

namespace Bronxamp.Sistem;

/// <summary>Keeps a password in the settings file readable only by this Windows user on this computer.</summary>
public static class Zastita
{
    public static string Sifruj(string tekst) =>
        tekst.Length == 0 ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(tekst), null, DataProtectionScope.CurrentUser));

    /// <summary>Empty when it cannot be read, as after moving the settings to another computer.</summary>
    public static string Desifruj(string sifrovano)
    {
        if (sifrovano.Length == 0) return "";
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(sifrovano), null, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            Dnevnik.Greska("Čitanje lozinke", ex);
            return "";
        }
    }
}
