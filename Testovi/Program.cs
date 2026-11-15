using Bronxamp.Testovi;

Console.OutputEncoding = System.Text.Encoding.UTF8;


// Everything temporary stays inside the test's own output folder.
if (args.FirstOrDefault() is not ("--ekran" or "--pravo" or "--mp3" or "--trajanja" or "--spektar"))
{
    var radni = Path.Combine(AppContext.BaseDirectory, "privremeno");
    if (Directory.Exists(radni)) Directory.Delete(radni, true);
    Directory.CreateDirectory(radni);
    Environment.SetEnvironmentVariable("PLEJER_PODACI", Path.Combine(radni, "podaci"));
    Spisak.Radni = radni;
}

// Timing run on real folders: Testovi --pravo <folder> <baza>. Only reads the music.
if (args.Length == 3 && args[0] == "--pravo")
    return await BibliotekaTestovi.PravoSkeniranje(args[1], args[2]);

// Live stream smoke-test: Testovi --stream <wav>  (ton kroz StreamEmisija na lokalni Icecast)
if (args.Length >= 2 && args[0] == "--stream")
    return await StreamTest.Pokreni(args[1]);

// Long run on the test server: Testovi --dugo <fajl> [sekundi]  (320 kbps, keeps watching the state)
if (args.Length >= 2 && args[0] == "--dugo")
    return await StreamTest.Dugo(args[1], args.Length >= 3 && int.TryParse(args[2], out int s) ? s : 40);

// Screens: Testovi --ekran <folder za slike> <scenario> --bez-zvuka, with PLEJER_PODACI set by the caller.
if (args.Length >= 3 && args[0] == "--ekran")
    return EkranTest.Pokreni(args[1], args[2]);

if (args.Length >= 2 && args[0] == "--trajanja")
    return ZvukTestovi.UporediTrajanja(args.Skip(1));

if (args.Length >= 2 && args[0] == "--spektar")
    return SpektarTestovi.Izmeri(args[1], args.Length >= 3 && int.TryParse(args[2], out int n) ? n : 31);

if (args.Length >= 2 && args[0] == "--mp3")
{
    foreach (var fajl in args.Skip(1)) ZvukTestovi.ProveriMp3(fajl);
    return 0;
}

var filter = args.FirstOrDefault();
int pali = 0, ukupno = 0;

foreach (var (ime, test) in Spisak.Svi)
{
    if (filter != null && !ime.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
    ukupno++;
    try
    {
        await test();
        Console.WriteLine($"OK    {ime}");
    }
    catch (Exception ex)
    {
        pali++;
        Console.WriteLine($"PAO   {ime}: {ex.Message}");
    }
}

Console.WriteLine($"\n{ukupno - pali}/{ukupno} prošlo");
return pali == 0 ? 0 : 1;
