using Bronxamp.Prikaz;
using static Bronxamp.Testovi.Spisak;

namespace Bronxamp.Testovi;

/// <summary>
/// The help is written twice, in Serbian and in English. A topic or a bullet added to one language has to be
/// added to the other too, and every literal in the windows has to have its translation.
/// </summary>
public static class PomocTestovi
{
    /// <summary>Same number of topics, same number of bullets in each, and none of them empty.</summary>
    public static Task ObaJezika()
    {
        var sr = PomocProzor.TemeSr;
        var en = PomocEngleski.Teme;
        Tvrdi(sr.Length == en.Length, $"tema: srpski {sr.Length}, engleski {en.Length}");
        Tvrdi(sr.Length >= 13, $"tema ima {sr.Length}");

        for (int i = 0; i < sr.Length; i++)
        {
            Tvrdi(sr[i].Naslov.Length > 0 && en[i].Naslov.Length > 0, $"tema {i + 1} nema naslov");
            Tvrdi(sr[i].Stavke.Length == en[i].Stavke.Length,
                $"„{sr[i].Naslov}“: {sr[i].Stavke.Length} srpskih, {en[i].Stavke.Length} engleskih stavki");
            Tvrdi(sr[i].Stavke.All(s => s.Trim().Length > 0) && en[i].Stavke.All(s => s.Trim().Length > 0),
                $"prazna stavka u „{sr[i].Naslov}“");
        }

        // The help has to say what the program has now, not what it had before.
        var zvuk = string.Join(" ", sr.Single(t => t.Naslov == "Zvuk i prelazi").Stavke);
        Tvrdi(zvuk.Contains("12 sekundi"), "prelaz ide do 12 sekundi");
        Tvrdi(zvuk.Contains("Limiter"), "limiter nije opisan");
        Tvrdi(zvuk.Contains("Normalizacija"), "normalizacija nije opisana");
        Tvrdi(sr.Any(t => t.Naslov == "Podešavanja"), "nema teme o podešavanjima");
        return Task.CompletedTask;
    }

    /// <summary>„Pomoć“ is the title of the help window: it must become „Help“, not stay Serbian.</summary>
    public static Task NaslovPomoci()
    {
        var jezik = Rec.Jezik;
        try
        {
            Rec.Jezik = "sr";
            Tvrdi(Rec.T("Pomoć") == "Pomoć", $"na srpskom je „{Rec.T("Pomoć")}“");
            Rec.Jezik = "en";
            Tvrdi(Rec.T("Pomoć") == "Help", $"na engleskom je „{Rec.T("Pomoć")}“");
            Tvrdi(Rec.T("Pomoć (F1)") == "Help (F1)", "savet dugmeta za pomoć");
            Tvrdi(Rec.T("Dodaj u omiljeno") == "Add to favourites", "stavka menija za omiljeno");
            Tvrdi(Rec.T("Preslušaj na slušalicama") == "Cue on headphones", "stavka menija za preslušavanje");
        }
        finally
        {
            Rec.Jezik = jezik;
        }
        return Task.CompletedTask;
    }
}
