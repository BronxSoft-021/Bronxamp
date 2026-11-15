using System.IO;
using System.Text.RegularExpressions;

namespace Bronxamp.Podaci;

/// <summary>
/// Default order of songs in a list: by folder, then by the number at the start of the file name
/// („01. …“), then by the file name. The radio's music is kept in folders, not albums
/// (see CLAUDE.md), so a numbered folder keeps its running order instead of falling into alphabet.
/// </summary>
public static partial class Poredak
{
    /// <summary>„01. …“ → 1, „3 - …“ → 3, „007) …“ → 7; 0 when the name does not start with a number.</summary>
    public static int BrojIzImena(string ime)
    {
        var m = BrojNaPocetku().Match(ime);
        return m.Success && int.TryParse(m.Groups[1].Value, out int broj) ? broj : 0;
    }

    /// <summary>Folder, then number from the file name, then the name itself.</summary>
    public static List<Pesma> PoFolderuINumeraciji(IEnumerable<Pesma> pesme) =>
        pesme.OrderBy(FolderOd, StringComparer.CurrentCultureIgnoreCase)
             .ThenBy(p => BrojIzImena(Path.GetFileName(p.Putanja)))
             .ThenBy(p => Path.GetFileName(p.Putanja), StringComparer.CurrentCultureIgnoreCase)
             .ToList();

    /// <summary>The album's folder; a song outside the library falls back to its own directory.</summary>
    public static string FolderOd(Pesma pesma) =>
        !string.IsNullOrEmpty(pesma.Album?.Folder) ? pesma.Album!.Folder : Path.GetDirectoryName(pesma.Putanja) ?? "";

    /// <summary>Numbers the rows of a list: „1“, „2“, … in the order shown.</summary>
    public static void Numerisi<T>(IReadOnlyList<T> redovi, Action<T, string> postavi)
    {
        for (int i = 0; i < redovi.Count; i++) postavi(redovi[i], (i + 1).ToString());
    }

    [GeneratedRegex(@"^\s*(\d{1,3})\s*[.\-–—)]")]
    private static partial Regex BrojNaPocetku();
}
