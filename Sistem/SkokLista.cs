using System.Windows;
using System.Windows.Shell;
using Bronxamp.Podaci;

namespace Bronxamp.Sistem;

/// <summary>Recent albums in the right-click list of the taskbar icon.</summary>
public static class SkokLista
{
    private const int NajviseAlbuma = 7;
    private static string _poslednji = "";

    public static void Osvezi(Katalog katalog)
    {
        var exe = Environment.ProcessPath;
        if (exe == null) return;

        var albumi = katalog.Albumi
            .Where(a => a.PoslednjePustanje != null)
            .OrderByDescending(a => a.PoslednjePustanje)
            .Take(NajviseAlbuma)
            .ToList();

        // Writing the list costs a shell round trip, so it happens only when it really changed.
        var kljuc = string.Join(",", albumi.Select(a => a.Id));
        if (kljuc == _poslednji) return;
        _poslednji = kljuc;

        try
        {
            var lista = new JumpList { ShowRecentCategory = false, ShowFrequentCategory = false };
            foreach (var a in albumi)
                lista.JumpItems.Add(new JumpTask
                {
                    Title = $"{a.Izvodjac} - {a.Naziv}",
                    Description = "Pusti album",
                    ApplicationPath = exe,
                    IconResourcePath = exe,
                    Arguments = $"--album {a.Id}",
                    CustomCategory = "Nedavni albumi",
                });
            JumpList.SetJumpList(Application.Current, lista);
            lista.Apply();
        }
        catch (Exception ex)
        {
            Dnevnik.Greska("Lista na taskbaru", ex);
        }
    }
}
