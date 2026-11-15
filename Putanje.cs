using System.IO;

namespace Bronxamp;

/// <summary>Where the program keeps its data. PLEJER_PODACI overrides it (tests must not touch the real library).</summary>
public static class Putanje
{
    public static string Podaci { get; } = Odredi();

    public static string Baza => Path.Combine(Podaci, "biblioteka.db");
    public static string Podesavanja => Path.Combine(Podaci, "podesavanja.json");
    public static string Greske => Path.Combine(Podaci, "greske.log");
    public static string Omoti => Path.Combine(Podaci, "omoti");
    public static string Dani => Path.Combine(Podaci, "dani");
    public static string Emitovano => Path.Combine(Podaci, "emitovano");

    private static string Odredi()
    {
        var zadato = Environment.GetEnvironmentVariable("PLEJER_PODACI");
        if (!string.IsNullOrWhiteSpace(zadato))
        {
            zadato = Path.GetFullPath(zadato);
            Directory.CreateDirectory(zadato);
            return zadato;
        }

        var koren = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var putanja = Path.Combine(koren, "BronxSoft Bronxamp");
        var stara = Path.Combine(koren, "UnderGRAND Plejer");

        // The program was first released under the old name. Its library moves over once.
        if (Directory.Exists(stara) && !Directory.Exists(putanja))
        {
            try
            {
                Directory.Move(stara, putanja);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return stara;
            }
        }

        Directory.CreateDirectory(putanja);
        return putanja;
    }
}
