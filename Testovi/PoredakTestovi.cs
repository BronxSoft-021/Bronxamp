using System.IO;
using Bronxamp.Podaci;

namespace Bronxamp.Testovi;

public static class PoredakTestovi
{
    /// <summary>„01. …“ → 1; the list follows folder and numbered order instead of the alphabet.</summary>
    public static Task Red()
    {
        Tvrdi(Poredak.BrojIzImena("01. Aaliyah - If Your Girl Only Knew.mp3") == 1, "„01.“ nije 1");
        Tvrdi(Poredak.BrojIzImena("3 - Abakus - Under the Stars.mp3") == 3, "„3 -“ nije 3");
        Tvrdi(Poredak.BrojIzImena("007) X.mp3") == 7, "„007)“ nije 7");
        Tvrdi(Poredak.BrojIzImena("Bez broja.mp3") == 0, "ime bez broja nije 0");
        Tvrdi(Poredak.BrojIzImena("2024 let.mp3") == 0, "„2024“ nije broj pesme");

        var pesme = new[]
        {
            P(@"D:\Muzika\B", "10. Deseta.mp3"),
            P(@"D:\Muzika\A", "2. Druga.mp3"),
            P(@"D:\Muzika\B", "02. Druga.mp3"),
            P(@"D:\Muzika\A", "01. Prva.mp3"),
            P(@"D:\Muzika\B", "01. Prva.mp3"),
        };
        var redom = Poredak.PoFolderuINumeraciji(pesme).Select(p => Path.GetFileName(p.Putanja)).ToList();
        var ocekivano = "01. Prva.mp3|2. Druga.mp3|01. Prva.mp3|02. Druga.mp3|10. Deseta.mp3";
        Tvrdi(string.Join("|", redom) == ocekivano, "red nije po folderu i broju: " + string.Join("|", redom));
        return Task.CompletedTask;
    }

    private static Pesma P(string folder, string ime) => new()
    {
        Putanja = Path.Combine(folder, ime),
        Album = new Album { Folder = folder },
        Naslov = ime,
    };

    private static void Tvrdi(bool uslov, string opis)
    {
        if (!uslov) throw new InvalidOperationException(opis);
    }
}
