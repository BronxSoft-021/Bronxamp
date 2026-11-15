using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Bronxamp.Podaci;
using Bronxamp.Prikaz;
using Bronxamp.Vm;

namespace Bronxamp.Testovi;

/// <summary>
/// Runs the real window in this process without sound, drives it through its view models and saves what it
/// renders. Lets a person (or Claude) look at every screen without touching the mouse.
/// </summary>
public static class EkranTest
{
    public static int Pokreni(string folder, string scenario)
    {
        int rezultat = 1;
        var nit = new Thread(() =>
        {
            var app = new App();
            app.InitializeComponent();
            app.Startup += (_, _) => app.Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    await Scenario(folder, scenario);
                    rezultat = 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PAO: {ex}");
                }
                app.Shutdown();
            });
            app.Run();
        });
        nit.SetApartmentState(ApartmentState.STA);
        nit.Start();
        nit.Join();
        return rezultat;
    }

    public static void Slikaj(FrameworkElement element, string putanja)
    {
        element.UpdateLayout();
        var slika = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        slika.Render(element);
        var koder = new PngBitmapEncoder();
        koder.Frames.Add(BitmapFrame.Create(slika));
        using var izlaz = File.Create(putanja);
        koder.Save(izlaz);
        Console.WriteLine($"slika: {putanja}");
    }

    private static async Task Cekaj(Func<bool> uslov, int ms = 15000)
    {
        var kraj = DateTime.Now.AddMilliseconds(ms);
        while (!uslov() && DateTime.Now < kraj) await Task.Delay(50);
        if (!uslov()) throw new TimeoutException("uslov nije ispunjen");
    }

    private sealed class GreskeVezivanja : TraceListener
    {
        public readonly HashSet<string> Poruke = [];
        public override void Write(string? poruka) { }
        public override void WriteLine(string? poruka)
        {
            if (poruka != null) Poruke.Add(poruka.Length > 220 ? poruka[..220] : poruka);
        }
    }

    private static async Task Scenario(string folder, string scenario)
    {
        var greske = new GreskeVezivanja();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(greske);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

        try
        {
            await Koraci(folder, scenario);
        }
        finally
        {
            Console.WriteLine($"greške vezivanja: {greske.Poruke.Count}");
            foreach (var g in greske.Poruke.Take(12)) Console.WriteLine("  " + g);
        }
    }

    private static async Task Koraci(string folder, string scenario)
    {
        var vm = App.Vm;
        var prozor = (Window)Application.Current.MainWindow!;
        var sadrzaj = (FrameworkElement)prozor.Content;
        void Slika(string ime) => Slikaj(sadrzaj, Path.Combine(folder, ime + ".png"));

        var sat = Stopwatch.StartNew();
        if (scenario == "dodavanje")
        {
            await ProveriDodavanje(vm, prozor, folder);
            return;
        }
        await Cekaj(() => vm.Katalog.Albumi.Count > 0, 60000);
        Console.WriteLine($"biblioteka učitana za {sat.ElapsedMilliseconds} ms: {vm.Katalog.Albumi.Count} albuma, {vm.Katalog.Pesme.Count} pesama");
        if (scenario == "dan")
        {
            await ProveriDan(vm, prozor, folder);
            return;
        }
        if (scenario == "lista")
        {
            await ProveriListu(vm, prozor, folder);
            return;
        }
        if (scenario == "dugmad")
        {
            await ProveriDugmad(vm, prozor, folder);
            return;
        }
        if (scenario == "omoti")
        {
            await ProveriOmote(vm, prozor, folder);
            return;
        }
        if (scenario == "veliko")
        {
            await Veliko(vm, prozor, folder);
            return;
        }
        await Task.Delay(1500);
        Slika("01_omoti");

        // Play right after start, with an empty queue, plays what is on screen: the first album on the wall...
        Spisak.Tvrdi(vm.Plejer.Red.Ukupno == 0, "red nije prazan na početku");
        ((Button)prozor.FindName("DugmePusti")).Command.Execute(null);
        await Cekaj(() => vm.Plejer.Svira);
        Console.WriteLine($"Pusti sa praznim redom (zid): {vm.Plejer.Red.Naziv}");
        // ...and on the song list, the song picked with a click (not the first one).
        vm.Plejer.Stop();
        vm.Plejer.IsprazniRed();
        vm.Prikaz = PrikazBiblioteke.Lista;
        await Cekaj(() => vm.Strana is PesmeStrana { Stavke.Count: > 5 });
        var lista = (PesmeStrana)vm.Strana!;
        vm.Izabrana.Izaberi(lista.Stavke[4].Pesma);
        ((Button)prozor.FindName("DugmePusti")).Command.Execute(null);
        await Cekaj(() => vm.Plejer.Svira);
        Spisak.Tvrdi(vm.Plejer.Red.Tekuca?.Id == lista.Stavke[4].Pesma.Id, "Pusti nije krenuo od izabrane pesme");
        Console.WriteLine($"Pusti sa praznim redom (lista): {vm.Plejer.Red.Tekuca?.Naslov}, u redu {vm.Plejer.Red.Ukupno}");
        vm.Izabrana.Izaberi(null);
        vm.Prikaz = PrikazBiblioteke.Omoti;
        await Task.Delay(800);

        vm.PustiAlbum(vm.Katalog.Albumi[0]);
        await Cekaj(() => vm.Plejer.Svira);
        await Task.Delay(2500);
        Console.WriteLine($"svira: {vm.Plejer.Naslov} ({vm.Plejer.VremeTekst} / {vm.Plejer.UkupnoTekst}), kraj {vm.Plejer.KrajSat}, {vm.Plejer.KrajOpis}");
        Slika("02_svira_album");

        vm.Upit = "love";
        await Task.Delay(1200);
        Slika("03_pretraga");
        vm.Upit = "";
        await Task.Delay(500);

        vm.Prikaz = PrikazBiblioteke.Lista;
        await Task.Delay(1200);
        Slika("04_lista");

        vm.Prikaz = PrikazBiblioteke.AS;
        await Task.Delay(1200);
        Slika("05_as");

        vm.Prikaz = PrikazBiblioteke.Godina;
        await Task.Delay(1200);
        Slika("06_godina");
        vm.Prikaz = PrikazBiblioteke.Omoti;

        vm.OtvoriAlbum(vm.Katalog.Albumi[1]);
        await Task.Delay(800);
        Slika("07_album");

        Spisak.Tvrdi(vm.NemaEmisija, "emisije ne smeju biti unapred upisane");
        vm.NovaEmisija("Probna", "#6C5CE7");
        var emisija = vm.NavEmisije[0].Emisija!;
        var pesme = vm.Katalog.Pesme.Take(14).ToList();
        vm.Baza.ZabeleziPustanje(pesme[2].Id, emisija.Id, DateTime.Now.AddDays(-20));
        vm.DodajUEmisiju(emisija, pesme);
        vm.OtvoriEmisiju(emisija);
        await Task.Delay(600);
        Slika("08_emisija");

        // The list of shows is a pill now, and the back arrow on a show leads to it.
        Spisak.Tvrdi(vm.Pilule == vm.PiluleBiblioteke && vm.PiluleBiblioteke[4].Aktivna && vm.NavMuzika[0].Aktivna, "emisija nije pod Bibliotekom");
        vm.NazadCommand.Execute(null);
        await Task.Delay(300);
        Spisak.Tvrdi(vm.Strana is EmisijeStrana, "strelica sa emisije ne vodi na spisak emisija");
        Slika("08a_emisije");

        vm.PustiEmisiju(emisija);
        await Task.Delay(2500);
        Console.WriteLine($"emisija: {vm.Plejer.SlediNaslov}; kraj {vm.Plejer.KrajSat}; {vm.Plejer.KrajOpis}");
        Slika("09_emisija_svira");

        vm.NavIzvestaj[1].OtvoriCommand.Execute(null);
        await Task.Delay(600);
        Slika("10_najslusanije");
        Spisak.Tvrdi(vm.Pilule == vm.PiluleStatistike && vm.PiluleStatistike[0].Aktivna && !vm.PiluleBiblioteke[4].Aktivna, "statistika nema svoje pilule");

        vm.PiluleStatistike[1].OtvoriCommand.Execute(null);
        await Task.Delay(1200);
        Slika("11_nije_pustano");

        vm.PiluleBiblioteke[1].OtvoriCommand.Execute(null);
        await Task.Delay(600);
        Slika("12_izvodjaci");

        vm.PiluleBiblioteke[2].OtvoriCommand.Execute(null);
        await Task.Delay(1000);
        Slika("13_nedavno");

        // Walls without a title must not stay when the albums come back.
        vm.PiluleStatistike[1].OtvoriCommand.Execute(null);
        await Task.Delay(300);
        vm.NavMuzika[0].OtvoriCommand.Execute(null);
        await Task.Delay(800);
        Spisak.Tvrdi(vm.Strana is ZidStrana { Podnaslov: "" } && vm.PiluleBiblioteke[0].Aktivna, "Albumi su zadržali podnaslov druge strane");

        vm.OtvoriDan(TabDana.Dan);
        await Task.Delay(300);
        Spisak.Tvrdi(vm.Pilule == null, "Na etru ne sme da ima pilule");
        var skrol = (ScrollViewer)prozor.FindName("MeniSkrol");
        Console.WriteLine($"levi meni: visina prozora {prozor.ActualHeight:0}, ostaje za skrolovanje {skrol.ScrollableHeight:0}");
        vm.OtvoriAlbume();

        // Queue: remove from the middle, remove a whole album, keep it as a new show, clear it.
        int uRedu = vm.Plejer.Red.Ukupno;
        vm.Plejer.UkloniIzReda([vm.Plejer.Sledi[3]]);
        Spisak.Tvrdi(vm.Plejer.Red.Ukupno == uRedu - 1 && vm.Plejer.Svira, "brisanje pesme iz reda");
        vm.SacuvajRedKaoEmisiju("Iz reda", "#3FB27F");
        Spisak.Tvrdi(vm.NavEmisije.Count == 2 && vm.PesmeEmisije(vm.NavEmisije[1].Emisija!).Count == uRedu - 1, "red nije sačuvan kao emisija");
        await Task.Delay(300);
        Slika("19_red");

        foreach (var ime in new[] { "uvod", "logo_bronxamp", "logo_bronxsoft" })
            Spisak.Tvrdi(Application.GetResourceStream(new Uri($"pack://application:,,,/Bronxamp;component/assets/{ime}.png")) != null, $"nema slike {ime}");

        var verzije = new VerzijeProzor();
        verzije.Show();
        await Task.Delay(300);
        Slikaj((FrameworkElement)verzije.Content, Path.Combine(folder, "24_sta_je_novo.png"));
        verzije.Close();

        // Help and About: the left-menu buttons really open them, and every topic has text.
        foreach (var (ime, tip) in new[] { ("DugmePomoc", typeof(PomocProzor)), ("DugmeOProgramu", typeof(OProgramuProzor)) })
        {
            Window? otvoren = null;
            _ = prozor.Dispatcher.BeginInvoke(() =>
            {
                otvoren = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.GetType() == tip);
                otvoren?.Close();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Klikni((Button)prozor.FindName(ime));
            Spisak.Tvrdi(otvoren != null, $"{ime} ne otvara prozor");
        }
        // F1 goes through the same key handler the user presses
        Window? f1 = null;
        _ = prozor.Dispatcher.BeginInvoke(() =>
        {
            f1 = Application.Current.Windows.OfType<PomocProzor>().FirstOrDefault();
            f1?.Close();
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        prozor.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(prozor)!, 0, System.Windows.Input.Key.F1)
        {
            RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
        });
        Spisak.Tvrdi(f1 != null, "F1 ne otvara pomoć");
        Spisak.Tvrdi(File.Exists(OProgramuProzor.PutanjaLicenci), "Licence.txt nije uz program");
        var licence = File.ReadAllText(OProgramuProzor.PutanjaLicenci);
        foreach (var t in OProgramuProzor.Tehnologije)
            Spisak.Tvrdi(licence.Contains(t.Ime.Split(',')[0].Replace(" i WPF", "").Replace(" fontovi", "")), $"Licence.txt nema {t.Ime}");
        Spisak.Tvrdi(PomocProzor.Teme.All(t => t.Stavke.Length > 0), "prazna tema u pomoći");
        var oProgramu = new OProgramuProzor();
        oProgramu.Show();
        await Task.Delay(300);
        Slikaj(oProgramu, Path.Combine(folder, "27_o_programu.png"));
        oProgramu.Close();
        var pomoc = new PomocProzor();
        pomoc.Show();
        for (int i = 0; i < PomocProzor.Teme.Length; i++)
        {
            pomoc.Prikazi(i);
            await Task.Delay(150);
            Slikaj(pomoc, Path.Combine(folder, $"28_pomoc_{i}.png"));
        }
        pomoc.Close();

        var thumb = prozor.TaskbarItemInfo.ThumbButtonInfos;
        Spisak.Tvrdi(thumb.Count == 3 && thumb.All(t => t.Command != null), "dugmad na sličici nemaju komande");

        var podesavanja = new PodesavanjaProzor();
        podesavanja.Show();
        await Task.Delay(300);
        Slikaj((FrameworkElement)podesavanja.Content, Path.Combine(folder, "16_podesavanja.png"));
        podesavanja.Close();

        var dijalog = new EmisijaDijalog();
        dijalog.Show();
        await Task.Delay(200);
        Slikaj((FrameworkElement)dijalog.Content, Path.Combine(folder, "17_nova_emisija.png"));
        dijalog.Close();

        var album = vm.Katalog.Albumi[2];
        var meni = KontekstMeni.ZaAlbum(album);
        meni.PlacementTarget = prozor;
        meni.IsOpen = true;
        await Task.Delay(300);
        Slikaj(meni, Path.Combine(folder, "18_meni.png"));
        meni.IsOpen = false;

        await ProveriWindows(vm);
        await ProveriKraj(vm, prozor, folder);
    }

    private static IEnumerable<T> Svi<T>(DependencyObject koren) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(koren); i++)
        {
            var dete = VisualTreeHelper.GetChild(koren, i);
            if (dete is T nadjeno) yield return nadjeno;
            foreach (var dublje in Svi<T>(dete)) yield return dublje;
        }
    }

    /// <summary>Starts with an empty library, as the user's was, and adds music the way the buttons do.</summary>
    private static async Task ProveriDodavanje(GlavniVm vm, Window prozor, string folder)
    {
        await Task.Delay(1200);
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "50_prazno.png"));
        foreach (var ime in new[] { "DugmeDodajAlbum", "DugmeDodajPesme" })
            Spisak.Tvrdi(prozor.FindName(ime) is Button { IsVisible: true }, $"{ime} se ne vidi");

        var d = Path.Combine(folder, "muzika");
        if (Directory.Exists(d)) Directory.Delete(d, true);
        var album = Path.Combine(d, "Izvođač - Album (1999)");
        Directory.CreateDirectory(album);
        var izvor = new byte[44 + 44100 * 4];
        void Wav(string put)
        {
            using var w = new BinaryWriter(File.Create(put));
            w.Write("RIFF"u8.ToArray()); w.Write(36 + 44100 * 4); w.Write("WAVE"u8.ToArray());
            w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)2);
            w.Write(44100); w.Write(44100 * 4); w.Write((short)4); w.Write((short)16);
            w.Write("data"u8.ToArray()); w.Write(44100 * 4); w.Write(new byte[44100 * 4 * 5]);
        }
        Wav(Path.Combine(album, "01 - Prva.wav"));
        Wav(Path.Combine(album, "02 - Druga.wav"));
        var sama = Path.Combine(d, "Moby - Sama.wav");
        Wav(sama);

        // A folder on an empty queue: plays right away, as an album.
        await vm.DodajMuzikuAsync([album]);
        await Cekaj(() => vm.Plejer.Svira, 5000);
        Console.WriteLine($"Dodaj album: svira {vm.Plejer.Naslov}, red {vm.Plejer.Red.Ukupno}, album {vm.Plejer.Red.JeAlbum}");
        Spisak.Tvrdi(vm.Plejer.Red.Ukupno == 2 && vm.Plejer.Red.JeAlbum, "album nije pušten");

        // A single song from outside the library goes to the end of the queue.
        await vm.DodajMuzikuAsync([sama, Path.Combine(d, "nije.txt")]);
        await Task.Delay(300);
        Console.WriteLine($"Dodaj pesme: {vm.Poruka}, red {vm.Plejer.Red.Ukupno}");
        Spisak.Tvrdi(vm.Plejer.Red.Ukupno == 3 && vm.Plejer.Red.Preostale.Last().Naslov == "Sama", "pesma nije dodata u red");
        Spisak.Tvrdi(vm.Plejer.Svira, "dodavanje je prekinulo reprodukciju");
        await Task.Delay(800);
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "51_dodato.png"));

        await vm.DodajMuzikuAsync([Path.Combine(d, "nema.txt")]);
        Console.WriteLine($"pogrešan fajl: {vm.Poruka}");
        vm.Plejer.IsprazniRed();
    }

    private static void Klikni(Button dugme) => dugme.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, dugme));

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Tacka
    {
        public int X, Y;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Tacka tacka);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Tacka tacka);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void mouse_event(uint radnja, int x, int y, uint podatak, UIntPtr dodatak);

    /// <summary>
    /// A drag with the real mouse: WPF reads the button state from the device, so posted messages do not start
    /// one. The window goes on top first, and nothing is pressed unless it really is under the pointer.
    /// </summary>
    private static async Task Prevuci(Window prozor, Point od, Point @do)
    {
        const uint Dole = 0x0002, Gore = 0x0004;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(prozor).Handle;
        GetCursorPos(out var bilo);
        prozor.Topmost = true;
        prozor.Activate();
        await Task.Delay(300);

        var a = prozor.PointToScreen(od);
        var b = prozor.PointToScreen(@do);
        bool pritisnuto = false;
        try
        {
            SetCursorPos((int)a.X, (int)a.Y);
            await Task.Delay(120);
            GetCursorPos(out var sada);
            Spisak.Tvrdi(WindowFromPoint(sada) == hwnd, "prozor nije ispod miša, prevlačenje nije probano");

            mouse_event(Dole, 0, 0, 0, UIntPtr.Zero);
            pritisnuto = true;
            await Task.Delay(120);
            for (int i = 1; i <= 16; i++)
            {
                SetCursorPos((int)(a.X + (b.X - a.X) * i / 16), (int)(a.Y + (b.Y - a.Y) * i / 16));
                await Task.Delay(30);
            }
            await Task.Delay(120);
        }
        finally
        {
            if (pritisnuto) mouse_event(Gore, 0, 0, 0, UIntPtr.Zero);
            await Task.Delay(150);
            SetCursorPos(bilo.X, bilo.Y);
            prozor.Topmost = false;
        }
    }

    /// <summary>
    /// The real folders, read only: a day for tomorrow is built from them. Then a short day of generated
    /// sound plays by the real clock, with a crash in the middle.
    /// </summary>
    private static async Task ProveriDan(GlavniVm vm, Window prozor, string folder)
    {
        vm.OtvoriSastaviDan();
        var dan = vm.Dan;
        await Task.Delay(1200);
        Console.WriteLine($"plejlista {dan.Plejliste.Count}, jinglova {dan.Jinglovi.Count}: {dan.OpisJinglova}");
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "70_priprema.png"));

        // The user's own folder comes and goes with the days, so this part runs only when a day is prepared there.
        if (dan.Plejliste.Count >= 5)
        {
            dan.IzabranDan = dan.Datumi[1];
            await Task.Delay(800);
            Console.WriteLine($"emisije za {dan.IzabranDan.Naziv}: {string.Join(", ", dan.Emisije.Select(e => e.Naziv))}");
            Console.WriteLine($"u korenu: {string.Join(" | ", dan.Hitovi.Select(h => h.Naziv))}");
            // A show in one file at the top of the folder: it waits below with a box for its time.
            Console.WriteLine($"ostali fajlovi: {string.Join(" | ", dan.OstaliFajlovi.Select(f => f.Naziv))}");
            if (dan.OstaliFajlovi.Count > 0)
            {
                var fajl = dan.OstaliFajlovi.FirstOrDefault(f => f.Naziv.Contains("Metronom")) ?? dan.OstaliFajlovi[^1];
                Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "70b_ostali_fajlovi.png"));
                fajl.Vreme = "19:0";
                Spisak.Tvrdi(dan.OstaliFajlovi.Contains(fajl), "pola vremena ne pomera fajl");
                fajl.Vreme = "19:05";
                Spisak.Tvrdi(!dan.OstaliFajlovi.Contains(fajl) && dan.Emisije.Any(e => e.Izvor == fajl.Folder && e.Vreme == "19:05"),
                    "fajl sa upisanim vremenom postaje emisija");
            }
            // A folder without a time gets one: it becomes a block at that time.
            if (dan.Plejliste.Count > 0)
            {
                var plejlista = dan.Plejliste[^1];
                plejlista.Vreme = "15:00";
                Spisak.Tvrdi(!dan.Plejliste.Contains(plejlista) && dan.Emisije.Any(e => e.Izvor == plejlista.Folder && e.Vreme == "15:00"),
                    "folder sa upisanim vremenom postaje blok");
            }
            if (dan.Hitovi.Count == 2)
            {
                // Of two files at the top, one is a show: once it is taken as one, the other is the hit.
                dan.DodajEmisiju(dan.Hitovi[^1].Putanja!);
                Spisak.Tvrdi(dan.Hitovi.Count == 1 && dan.IzabranHit == dan.Hitovi[0], "preostali fajl treba da postane hit dana");
            }

            // The user's own day from 4. 11., with the same names and times.
            var raspored = new (string Ime, string Vreme)[]
            {
                ("Alternative", "09:05"), ("Focus 054", "17:05"), ("Alter Bridge", "18:00"), ("Balkan Beats", "20:05"),
                ("Tribute to Glam Rock", "22:00"), ("Reggae Tunes", "23:05"),
            };
            foreach (var (ime, _) in raspored)
                if (dan.Plejliste.FirstOrDefault(p => p.Naziv.StartsWith(ime)) is { } p) dan.DodajEmisiju(p.Folder);
            foreach (var e in dan.Emisije.Where(e => e.Vreme.Length == 0))
                e.Vreme = raspored.FirstOrDefault(r => e.Naziv.StartsWith(r.Ime)).Vreme ?? "19:05";
            Spisak.Tvrdi(dan.Spica == null && dan.PocetniJinglHita == null && dan.PocetniJingl == null, "program sam ne bira početak dana");
            dan.Spica = dan.SviJinglovi.First(j => j.Naziv.Contains("Pocetak programa"));
            dan.PocetniJinglHita = dan.SviJinglovi.First(j => j.Naziv.Contains("Hit dana"));
            dan.PocetniJingl = dan.SviJinglovi.First(j => j.Naziv.Contains("Radio sa dusom"));
            if (dan.IzabranHit == null && dan.Plejliste.Count > 0)
            {
                var hit = IzvorDana.Fajlovi(dan.Plejliste[0].Folder)[0];
                dan.IzabranHit = new IzborFajla(hit, Path.GetFileNameWithoutExtension(hit));
            }
            Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "71_priprema_emisija.png"));

            // Leaving the page and coming back must not lose a single choice.
            var izabranJingl = dan.JingloviEmisija.Skip(1).First();
            dan.Emisije[0].Jingl = izabranJingl;
            vm.OtvoriAlbume();
            await Task.Delay(300);
            vm.OtvoriSastaviDan();
            await Task.Delay(500);
            Spisak.Tvrdi(dan.Spica != null && dan.PocetniJinglHita != null && dan.PocetniJingl != null, "izbor početka dana se izgubio");
            Spisak.Tvrdi(dan.Emisije[0].Jingl == izabranJingl, "jingl emisije se izgubio");

            var sat = Stopwatch.StartNew();
            await dan.Slozi();
            Console.WriteLine($"složeno za {sat.ElapsedMilliseconds} ms: {dan.OpisDana}");
            foreach (var p in dan.Problemi) Console.WriteLine("  problem: " + p);
            await Task.Delay(700);
            Spisak.Tvrdi(dan.Stavke.Count > 50 && dan.NaPripremi, "dan nije složen");
            Spisak.Tvrdi(dan.Blokovi.Count > 8 && dan.Blokovi[^1].JeKraj, "dan po satima");
            Console.WriteLine($"semafor: {dan.Semafor} - {dan.NaslovSemafora}");
            foreach (var b in dan.Blokovi) Console.WriteLine($"  {b.Vreme,-8} {b.Naziv,-34} {b.Opis} {b.Visak} {b.Upozorenje}");
            dan.Blokovi[1].Otvoren = true;
            await Task.Delay(400);
            Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "71b_sastavi_dan.png"));
            var priprema = (ScrollViewer)Nadji<DanPrikaz>(prozor)!.FindName("Priprema");
            priprema.ScrollToVerticalOffset(520);
            await Task.Delay(400);
            Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "71c_dan_po_satima.png"));
            priprema.ScrollToVerticalOffset(2000);
            await Task.Delay(400);
            Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "71d_pokreni.png"));
            priprema.ScrollToVerticalOffset(0);
            dan.Tab = TabDana.Dan;
            await Task.Delay(500);
            var pustenih = dan.Stavke.Where(r => r.Stavka.Vrsta is VrstaStavke.Jingl or VrstaStavke.JinglHit).Select(r => r.Stavka.Fajl.Putanja).ToList();
            Console.WriteLine($"jinglova {pustenih.Count}, različitih {pustenih.Distinct().Count()}");
            Spisak.Tvrdi(pustenih.Distinct().Count() == pustenih.Count, "jingl se ponovio");
            foreach (var r in dan.Stavke.Where(r => r.Stavka.Zadato != null)) Console.WriteLine($"  {r.Zadato} {r.VrstaNaziv,-13} {r.Odstupanje}");
            Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "72_dan.png"));
            Spisak.Tvrdi(Nadji<DanPrikaz>(prozor)!.FindName("DugmeIsprazni") is Button { IsEnabled: true }, "nema dugmeta Isprazni dan");
            Spisak.Tvrdi(dan.Stavke[0].Vrsta == VrstaStavke.Spica, "dan počinje špicom");
            Spisak.Tvrdi(dan.PrikaziOstatak == !vm.Plejer.ImaRed, "složen dan desno, dok ništa drugo nije u redu");
            Spisak.Tvrdi(dan.Stavke.Count(r => r.Vrsta == VrstaStavke.HitDana) == 8, "hit dana nije u svim terminima");
            Spisak.Tvrdi(dan.Stavke.Count(r => r.Vrsta == VrstaStavke.Pesma) > 100, "folderi moraju da budu blokovi");

            // A real drag with the mouse, row 4 up to row 2. The pointer moves on its own thread, because
            // drag and drop holds the window's thread for as long as it lasts.
            var listaDana = (ListView)Nadji<DanPrikaz>(prozor)!.FindName("ListaDana");
            Spisak.Tvrdi(listaDana.SelectionMode == SelectionMode.Extended, "lista dana ne dozvoljava više redova");
            Spisak.Tvrdi(Nadji<DanPrikaz>(prozor)!.FindName("DugmeObrisiSve") is Button, "nema dugmeta Obriši sve");
            listaDana.ScrollIntoView(dan.Stavke[0]);
            await Task.Delay(400);
            var redovi = Svi<ListViewItem>(listaDana).Where(r => r.IsVisible).OrderBy(r => r.TransformToAncestor(prozor).Transform(new Point()).Y).ToList();
            var vucena = (DanRed)redovi[4].DataContext;
            int ciljnoMesto = dan.Stavke.IndexOf((DanRed)redovi[2].DataContext);
            var odR = redovi[4].TransformToAncestor(prozor).Transform(new Point(200, redovi[4].ActualHeight / 2));
            var doR = redovi[2].TransformToAncestor(prozor).Transform(new Point(200, redovi[2].ActualHeight / 2));
            await PrevuciUPozadini(prozor, odR, doR);
            await Task.Delay(400);
            Console.WriteLine($"prevučena stavka je sada na mestu {dan.Stavke.IndexOf(vucena)}");
            Spisak.Tvrdi(dan.Stavke.IndexOf(vucena) == ciljnoMesto, $"prevlačenje mišem: mesto {dan.Stavke.IndexOf(vucena)}, a treba {ciljnoMesto}");
            Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "72b_prevuceno.png"));

            var datum = dan.IzabranDan!.Datum;
            Spisak.Tvrdi(SlozenDan.Ucitaj(Putanje.Dani, datum) != null, "dan nije sačuvan");
            dan.Ocisti();
            await Task.Delay(300);
            Spisak.Tvrdi(dan.Stavke.Count == 0 && dan.Emisije.All(e => !e.JeFolder) && dan.NaPripremi && SlozenDan.Ucitaj(Putanje.Dani, datum) == null, "počni iznova");
            Spisak.Tvrdi(dan.Spica != null, "početak dana ostaje posle čišćenja");
            Spisak.Tvrdi(dan.Plejliste.Count >= 4, $"folderi se vraćaju među plejliste: {dan.Plejliste.Count}");
            dan.Tab = TabDana.Jinglovi;
            await Task.Delay(500);
            Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "73_jinglovi.png"));

        }
        else
        {
            Console.WriteLine("šema dana je prazna, deo sa pravim folderima je preskočen");
        }

        // A short day: ten 3-second files, a fixed item, and a clock that is the real one.
        var radni = Path.Combine(folder, "kratak_dan");
        Directory.CreateDirectory(radni);
        var fajlovi = new List<FajlDana>();
        for (int i = 0; i < 10; i++)
        {
            var put = Path.Combine(radni, $"{i}.wav");
            NapraviTon(put, 3);
            fajlovi.Add(new FajlDana(put, "Izvođač", $"Stavka {i}", 3));
        }
        vm.Podesavanja.Prelaz = 0;
        var pocetak = DateTime.Now.AddSeconds(2);
        var kratak = new SlozenDan
        {
            Pocetak = pocetak,
            Kraj = pocetak.AddSeconds(30),
            Stavke = fajlovi.Select((f, i) => new StavkaDana
            {
                Vrsta = i == 0 ? VrstaStavke.Spica : i == 5 ? VrstaStavke.HitDana : VrstaStavke.Pesma,
                Fajl = f,
                Zadato = i == 5 ? pocetak.AddSeconds(9) : null,
            }).ToList(),
        };
        // Classic playlist editing on the composed day: several rows at once, then "clear".
        if (dan.Stavke.Count > 3)
        {
            int preBrisanja = dan.Stavke.Count;
            dan.UkloniVise(dan.Stavke.Skip(1).Take(2).ToList());
            Spisak.Tvrdi(dan.Stavke.Count == preBrisanja - 2, "brisanje više redova ne radi");
            dan.UkloniSve();
            Spisak.Tvrdi(dan.Stavke.Count == 0, "obriši sve ne prazni listu");
        }

        TokDana.Preracunaj(kratak.Stavke, 0, pocetak, 0);
        dan.Prikazi(kratak);
        dan.Tab = TabDana.Dan;
        await dan.Pokreni();
        Spisak.Tvrdi(dan.Ceka && !dan.Aktivan, "dan treba da čeka početak");
        await Cekaj(() => dan.Aktivan && vm.Plejer.Svira, 5000);
        double kasni = (DateTime.Now - pocetak).TotalSeconds;
        Console.WriteLine($"krenuo {kasni:0.00} s posle zadatog");
        Spisak.Tvrdi(Math.Abs(kasni) < 1, "dan nije krenuo na vreme");

        // Item 5 is due at +9 s, and at 3 s per item the song before it has to go.
        await Cekaj(() => vm.Plejer.Tekuca?.Naslov == "Stavka 5", 15000);
        double hit5 = (DateTime.Now - pocetak).TotalSeconds;
        Console.WriteLine($"zadata stavka krenula u +{hit5:0.0} s, u danu ostalo {dan.Stavke.Count} stavki");
        Spisak.Tvrdi(Math.Abs(hit5 - 9) <= 2, $"zadata stavka je krenula u +{hit5:0.0} s");
        Spisak.Tvrdi(dan.Stavke.Count < 10, "ništa nije preskočeno");
        // Changes while it plays: what comes next follows the list at once.
        var sledeci = dan.Stavke[^1];
        dan.Premesti(sledeci, dan.Stavke.IndexOf(dan.Stavke.First(r => r.Tekuca)) + 1);
        Spisak.Tvrdi(vm.Plejer.Red.Sledeca?.Naslov == sledeci.Naslov, $"posle pomeranja sledi {vm.Plejer.Red.Sledeca?.Naslov}");
        dan.Ukloni(sledeci);
        Spisak.Tvrdi(vm.Plejer.Red.Sledeca?.Naslov != sledeci.Naslov, "obrisana stavka je ostala u redu");
        dan.Ubaci(dan.Stavke.First(r => r.Tekuca), fajlovi[9], VrstaStavke.Jingl);
        Spisak.Tvrdi(vm.Plejer.Red.Sledeca?.Naslov == "Stavka 9", "dodata stavka nije sledeća");
        int odsvirane = dan.Stavke.IndexOf(dan.Stavke.First(r => r.Tekuca));
        int broj = dan.Stavke.Count;
        dan.Ukloni(dan.Stavke[0]);
        Spisak.Tvrdi(dan.Stavke.Count == broj && odsvirane > 0, "odsvirano ne može da se menja");
        await Task.Delay(1200);
        Spisak.Tvrdi(dan.Tab == TabDana.Dan, "dan se gleda na ekranu Na etru");
        vm.OtvoriDan(TabDana.Dan);
        await Task.Delay(500);
        Spisak.Tvrdi(((FrameworkElement)prozor.FindName("OstatakDana")).IsVisible, "ostatak dana se ne vidi dok dan traje");
        Spisak.Tvrdi(vm.Plejer.KarticaSledi?.Contains("Stavka") == true, $"kartica Sledi: {vm.Plejer.KarticaSledi}");
        Spisak.Tvrdi(vm.Plejer.NaEtruNaslov.Contains("Stavka"), $"Na etru: {vm.Plejer.NaEtruNaslov}");
        Spisak.Tvrdi(vm.Izabrana.Naslov.Length > 0, "panel izabrane pesme je prazan");
        await Task.Delay(400);
        Console.WriteLine($"panel: {vm.Izabrana.Naslov} | {vm.Izabrana.Kvalitet}");
        Spisak.Tvrdi(dan.NatpisStanja == "Dan svira", dan.NatpisStanja);
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "74_dan_svira.png"));

        // Crash: the program comes back knowing only the file and the clock.
        var sacuvan = SlozenDan.Ucitaj(Putanje.Dani, pocetak.Date)!;
        Spisak.Tvrdi(sacuvan.Pokrenut, "dan mora da piše da je pokrenut");
        dan.Zaustavi();
        Spisak.Tvrdi(!vm.Plejer.Svira, "zaustavi");
        sacuvan.Pokrenut = true;
        sacuvan.Sacuvaj(Putanje.Dani);
        await dan.NastaviAkoTreba();
        await Task.Delay(800);
        Console.WriteLine($"posle pada svira: {vm.Plejer.Tekuca?.Naslov}, aktivan {dan.Aktivan}");
        Spisak.Tvrdi(dan.Aktivan && vm.Plejer.Svira, "dan nije nastavljen");

        await Cekaj(() => !dan.Aktivan, 30000);
        Console.WriteLine($"stanje na kraju: {dan.Stanje}");
        await Task.Delay(300);
        Spisak.Tvrdi(!((FrameworkElement)prozor.FindName("OstatakDana")).IsVisible, "ostatak dana ostao posle kraja");

        vm.OtvoriFoldere();
        await Task.Delay(500);
        var folderi = (FolderiStrana)vm.Strana!;
        Console.WriteLine($"koreni: {string.Join(", ", folderi.Koreni.Select(k => k.Naziv))}; u prvom {folderi.Koreni[0].Deca.Count} foldera");
        var saPesmama = folderi.Koreni.SelectMany(k => k.Deca.Prepend(k)).FirstOrDefault(c => IzvorDana.Fajlovi(c.Putanja).Count > 0);
        if (saPesmama != null)
        {
            await folderi.Otvori(saPesmama);
            await Task.Delay(500);
            Console.WriteLine($"u folderu: {folderi.Podnaslov}");
            Spisak.Tvrdi(folderi.Stavke.Count > 0 && !folderi.MozeUklanjanje, "folder nije pročitan");
        }
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "75_folderi.png"));

        vm.OtvoriEmitovano();
        await Task.Delay(500);
        var emitovano = (EmitovanoStrana)vm.Strana!;
        Spisak.Tvrdi(emitovano.Linije.Count > 0, "dnevnik je prazan");
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "76_emitovano.png"));
        var dnevnik = Path.Combine(Putanje.Emitovano, $"{DateTime.Now:yyyy-MM-dd}.txt");
        Spisak.Tvrdi(File.Exists(dnevnik), "nema dnevnika");
        foreach (var l in File.ReadAllLines(dnevnik).TakeLast(8)) Console.WriteLine("  " + l);
    }

    private static void NapraviTon(string put, int sekundi)
    {
        const int Hz = 44100;
        using var w = new BinaryWriter(File.Create(put));
        int bajtova = Hz * sekundi * 2;
        w.Write("RIFF"u8);
        w.Write(36 + bajtova);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(Hz);
        w.Write(Hz * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(bajtova);
        for (int i = 0; i < Hz * sekundi; i++) w.Write((short)(Math.Sin(i * 0.02) * 600));
    }

    private static async Task PrevuciUPozadini(Window prozor, Point od, Point @do)
    {
        const uint Dole = 0x0002, Gore = 0x0004;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(prozor).Handle;
        GetCursorPos(out var bilo);
        prozor.Topmost = true;
        prozor.Activate();
        await Task.Delay(300);
        var a = prozor.PointToScreen(od);
        var b = prozor.PointToScreen(@do);

        await Task.Run(() =>
        {
            bool pritisnuto = false;
            try
            {
                SetCursorPos((int)a.X, (int)a.Y);
                Thread.Sleep(150);
                GetCursorPos(out var sada);
                if (WindowFromPoint(sada) != hwnd) { Console.WriteLine($"prozor nije ispod miša u {sada.X},{sada.Y}"); return; }
                mouse_event(Dole, 0, 0, 0, UIntPtr.Zero);
                pritisnuto = true;
                Thread.Sleep(150);
                for (int i = 1; i <= 20; i++)
                {
                    SetCursorPos((int)(a.X + (b.X - a.X) * i / 20), (int)(a.Y + (b.Y - a.Y) * i / 20));
                    Thread.Sleep(30);
                }
                Thread.Sleep(200);
            }
            finally
            {
                if (pritisnuto) mouse_event(Gore, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(200);
                SetCursorPos(bilo.X, bilo.Y);
            }
        });
        prozor.Topmost = false;
    }

    /// <summary>The song list: what it shows by default, the column menu, and removing selected songs.</summary>
    private static async Task ProveriListu(GlavniVm vm, Window prozor, string folder)
    {
        vm.Prikaz = PrikazBiblioteke.Lista;
        await Cekaj(() => vm.Strana is PesmeStrana { Stavke.Count: > 0 });
        await Task.Delay(800);

        var pogled = Nadji<PesmeLista>(prozor)!;
        var lista = Nadji<ListView>(pogled)!;
        var kolone = ((GridView)lista.View).Columns;
        string Redosled() => string.Join(" | ", kolone.Skip(1).SkipLast(1).Select(k => (string)k.Header));
        Spisak.Tvrdi(kolone.Count == 5 && Redosled() == "Izvođač | Naslov | Trajanje", $"podrazumevano: {Redosled()}");
        var red = Svi<ListViewItem>(lista).First();
        Spisak.Tvrdi(red.ActualHeight <= 26, $"red je visok {red.ActualHeight}");
        Spisak.Tvrdi(Svi<GridViewColumnHeader>(lista).Any(h => h.IsVisible && Equals(h.Content, "Izvođač")), "zaglavlje se ne vidi");
        Console.WriteLine($"lista: red {red.ActualHeight} px, širina naslova {kolone[1].ActualWidth:0}, {lista.Items.Count} pesama");
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "60_lista.png"));

        // Column menu: ticking works one by one, and the columns land between the title and the duration.
        var meni = pogled.MeniKolona();
        var stavke = meni.Items.OfType<MenuItem>().ToList();
        Spisak.Tvrdi(stavke.Count == PesmeLista.Izbor.Length + 1, $"meni kolona ima {stavke.Count} stavki");
        foreach (var ime in new[] { "Album", "Godina", "Žanr", "Puštano", "Poslednji put", "Format", "Bitrate", "Frekvencija", "Veličina" })
        {
            var stavka = stavke.Single(s => (string)s.Header == ime);
            stavka.IsChecked = true;
            stavka.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, stavka));
        }
        await Task.Delay(500);
        Spisak.Tvrdi(Redosled() == "Izvođač | Naslov | Trajanje | Album | Godina | Žanr | Puštano | Poslednji put | Format | Bitrate | Frekvencija | Veličina",
            $"nove kolone idu posle trajanja: {Redosled()}");
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "61_kolone.png"));

        // With more columns than room, the header has to follow the rows sideways.
        var skrol = Nadji<ScrollViewer>(lista)!;
        var levo = Svi<GridViewColumnHeader>(lista).First(h => h.IsVisible && Equals(h.Content, "Album"));
        double pre = levo.TransformToAncestor(prozor).Transform(new Point()).X;
        skrol.ScrollToHorizontalOffset(120);
        await Task.Delay(300);
        double posle = levo.TransformToAncestor(prozor).Transform(new Point()).X;
        Spisak.Tvrdi(skrol.HorizontalOffset == 120 && Math.Abs(pre - posle - 120) < 2, $"zaglavlje ne prati skrol: {pre:0} -> {posle:0}");
        skrol.ScrollToHorizontalOffset(0);
        await Task.Delay(200);

        foreach (var ime in new[] { "Godina", "Žanr", "Puštano", "Poslednji put", "Format", "Bitrate", "Frekvencija", "Veličina" })
        {
            var stavka = stavke.Single(s => (string)s.Header == ime);
            stavka.IsChecked = false;
            stavka.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, stavka));
        }
        await Task.Delay(500);

        // A real drag with the mouse: "Album" goes in front of "Izvođač".
        GridViewColumnHeader Zaglavlje(string ime) => Svi<GridViewColumnHeader>(lista).First(h => h.IsVisible && Equals(h.Content, ime));
        var iz = Zaglavlje("Album");
        var u = Zaglavlje("Izvođač");
        var pocetak = iz.TransformToAncestor(prozor).Transform(new Point(iz.ActualWidth / 2, iz.ActualHeight / 2));
        var kraj = u.TransformToAncestor(prozor).Transform(new Point(6, u.ActualHeight / 2));
        await Prevuci(prozor, pocetak, kraj);
        await Task.Delay(400);
        Console.WriteLine($"posle prevlačenja: {Redosled()}");
        Spisak.Tvrdi(Redosled() == "Album | Izvođač | Naslov | Trajanje", $"prevlačenje nije pomerilo kolonu: {Redosled()}");
        Spisak.Tvrdi(string.Join(",", vm.Podesavanja.KoloneSpiska) == "Album,Izvodjac,Naslov,Trajanje", "redosled nije zapamćen");
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "63_pomereno.png"));

        // The arrow column and the buttons do not move, whatever is dropped on them.
        var prvaKolona = kolone[0];
        var poslednja = kolone[^1];
        kolone.Move(2, 0);
        await Task.Delay(200);
        kolone.Move(1, kolone.Count - 1);
        await Task.Delay(300);
        Spisak.Tvrdi(kolone[0] == prvaKolona && kolone[^1] == poslednja, "strelica i dugmad moraju da ostanu na mestu");

        var vrati = stavke.Single(s => (string)s.Header == "Vrati podrazumevano");
        vrati.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, vrati));
        await Task.Delay(300);
        Spisak.Tvrdi(Redosled() == "Izvođač | Naslov | Trajanje", $"podrazumevane kolone nisu vraćene: {Redosled()}");

        // Everything selected, as in the user's picture: the menu offers removal, and the songs can come back.
        // A copy of an older library is being read again, and a refresh of the list drops the selection.
        await Cekaj(() => string.IsNullOrEmpty(vm.StatusSkeniranja), 120000);
        await Task.Delay(500);
        int pesama = vm.Katalog.Pesme.Count;
        lista.SelectAll();
        await Task.Delay(300);
        var izabrane = lista.SelectedItems.Cast<PesmaRed>().Select(r => r.Pesma).ToList();
        var desni = KontekstMeni.ZaPesme(izabrane, () => { }, saUklanjanjem: true);
        Spisak.Tvrdi(desni.Items.OfType<MenuItem>().Any(s => (string)s.Header == $"Ukloni iz spiska ({izabrane.Count})"), "meni nema uklanjanje");
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "62_sve_izabrano.png"));

        var sat = Stopwatch.StartNew();
        vm.UkloniPesme(izabrane.Take(3).ToList());
        await Task.Delay(500);
        Spisak.Tvrdi(vm.Katalog.Pesme.Count == pesama - 3 && vm.Baza.BrojIskljucenihPesama() >= 3, "tri pesme nisu uklonjene");
        Spisak.Tvrdi(izabrane.Take(3).All(p => File.Exists(p.Putanja)), "fajl na disku mora da ostane");
        vm.PustiPesme(izabrane.Skip(3).Take(5).ToList(), 0, "Lista");
        await Task.Delay(400);
        vm.UkloniPesme(izabrane.Skip(3).ToList());
        await Cekaj(() => vm.Katalog.Pesme.Count == 0 || vm.Strana is PesmeStrana { Stavke.Count: 0 });
        Console.WriteLine($"uklonjeno {izabrane.Count} pesama za {sat.ElapsedMilliseconds} ms");
        Spisak.Tvrdi(!vm.Plejer.ImaRed && vm.Plejer.Tekuca == null, "uklonjene pesme su ostale u redu");
        vm.VratiAlbume();
        await Cekaj(() => vm.Katalog.Pesme.Count >= pesama);
        await Cekaj(() => vm.Strana is PesmeStrana { Stavke.Count: > 0 });
        Console.WriteLine($"vraćeno: {vm.Katalog.Pesme.Count} pesama");
    }

    /// <summary>Shows the hover buttons of a tile or row and presses them with a real click event.</summary>
    private static async Task ProveriDugmad(GlavniVm vm, Window prozor, string folder)
    {
        await Task.Delay(1500);
        var red = vm.Plejer.Red;

        FrameworkElement Alati(int plocica) =>
            Svi<FrameworkElement>(Nadji<ZidPrikaz>(prozor)!).Where(e => e.Name == "Alati").ElementAt(plocica);
        Button Dugme(FrameworkElement alati, string radnja)
        {
            alati.Visibility = Visibility.Visible;
            alati.UpdateLayout();
            return Svi<Button>(alati).Single(b => (string)b.Tag == radnja);
        }

        var plocice = Svi<FrameworkElement>(Nadji<ZidPrikaz>(prozor)!).Where(e => e.Name == "Plocica").ToList();
        AlbumKartica Kartica(int i) => (AlbumKartica)plocice[i].DataContext;

        Klikni(Dugme(Alati(0), "pusti"));
        await Cekaj(() => vm.Plejer.Svira);
        Spisak.Tvrdi(vm.Plejer.Tekuca?.Album.Id == Kartica(0).Album.Id, "Pusti na pločici");
        int prvi = red.Ukupno;

        Klikni(Dugme(Alati(1), "sledece"));
        await Task.Delay(200);
        var drugi = Kartica(1).Album;
        Spisak.Tvrdi(red.Sledeca?.Id == drugi.Pesme[0].Id && red.Ukupno == prvi + drugi.Pesme.Count, "Pusti sledeće na pločici");
        Console.WriteLine($"Pusti sledeće: {vm.Poruka}");

        Klikni(Dugme(Alati(2), "red"));
        await Task.Delay(200);
        var treci = Kartica(2).Album;
        Spisak.Tvrdi(red.Preostale.Last().Id == treci.Pesme[^1].Id, "Dodaj u red na pločici");
        Console.WriteLine($"Dodaj u red: {vm.Poruka}");
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "43_plocica_dugmad.png"));

        // Shuffle: the caption shows it, and an album no longer always starts at its first song.
        var mesanje = (Button)prozor.FindName("DugmeMesanje");
        Spisak.Tvrdi(Equals(mesanje.ToolTip, "Nasumično: isključeno"), $"natpis: {mesanje.ToolTip}");
        var veliki = vm.Katalog.Albumi.First(a => a.Pesme.Count >= 8);
        vm.Plejer.PromeniMesanjeCommand.Execute(null);
        Spisak.Tvrdi(Equals(mesanje.ToolTip, "Nasumično: uključeno"), $"natpis posle klika: {mesanje.ToolTip}");
        var pocetci = new HashSet<long>();
        for (int i = 0; i < 12; i++)
        {
            vm.PustiAlbum(veliki);
            pocetci.Add(red.Tekuca!.Id);
            await Task.Delay(30);
        }
        Console.WriteLine($"nasumično: 12 puštanja, {pocetci.Count} različitih prvih pesama, u redu {red.Ukupno}");
        Spisak.Tvrdi(pocetci.Count > 1 && red.Ukupno == veliki.Pesme.Count, "album uvek kreće od iste pesme");
        vm.PustiAlbum(veliki, 2);
        Spisak.Tvrdi(red.Tekuca!.Id == veliki.Pesme[2].Id, "izabrana pesma mora da krene i kad je nasumično");
        await Task.Delay(300);
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "45_nasumicno.png"));
        vm.Plejer.PromeniMesanjeCommand.Execute(null);
        Klikni(Dugme(Alati(0), "pusti"));
        await Task.Delay(300);
        Klikni(Dugme(Alati(1), "sledece"));
        Klikni(Dugme(Alati(2), "red"));
        await Task.Delay(300);

        // X on a queue row
        var lista = (ListBox)prozor.FindName("ListaSledi");
        int pre = red.Ukupno;
        var naslov = red.Preostale.Skip(1).First().Naslov;
        await Task.Delay(300);
        var x = Svi<Button>(lista).Where(b => b.Name == "DugmeUkloni").ElementAt(2);
        Klikni(x);
        await Task.Delay(200);
        Spisak.Tvrdi(red.Ukupno == pre - 1 && red.Preostale.All(p => p.Naslov != naslov || drugi.Pesme.Count == 0), "X u redu");
        Console.WriteLine($"X u redu: {pre} -> {red.Ukupno} (uklonjena {naslov})");

        // Song rows on the album page
        var cetvrti = Kartica(3).Album.Id;
        Klikni(Dugme(Alati(3), "pesme"));
        await Task.Delay(500);
        var strana = (PesmeStrana)vm.Strana!;
        Spisak.Tvrdi(strana.Album?.Id == cetvrti, "Prikaži pesme na pločici");
        var redovi = Svi<FrameworkElement>(prozor).Where(e => e.Name == "Alati").ToList();
        redovi[1].Visibility = Visibility.Visible;
        redovi[1].UpdateLayout();
        var dugmad = Svi<Button>(redovi[1]).ToList();
        var pesma = strana.Stavke[1].Pesma;
        Klikni(dugmad[0]);
        await Task.Delay(200);
        Spisak.Tvrdi(red.Sledeca?.Id == pesma.Id, "Pusti sledeće na pesmi");
        Klikni(dugmad[1]);
        await Task.Delay(200);
        Spisak.Tvrdi(red.Preostale.Last().Id == pesma.Id, "Dodaj u red na pesmi");
        Console.WriteLine($"pesma: {vm.Poruka}");
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "44_album_dugmad.png"));

        // Removing the album that is playing: it leaves the queue and the wall, the file stays
        var album = vm.Plejer.Tekuca!.Album;
        var fajl = album.Pesme[0].Putanja;
        int albuma = vm.Katalog.Albumi.Count;
        vm.UkloniAlbum(album);
        await Task.Delay(800);
        Spisak.Tvrdi(vm.Katalog.Albumi.Count == albuma - 1 && File.Exists(fajl), "uklanjanje albuma");
        Spisak.Tvrdi(red.SvePesme.All(p => p.AlbumId != album.Id), "uklonjen album je ostao u redu");
        Console.WriteLine($"uklonjen album koji svira, sad svira: {vm.Plejer.Naslov} ({vm.Plejer.Tekuca?.Album.Naziv})");
        vm.VratiAlbume();
        await Task.Delay(300);
        Spisak.Tvrdi(vm.Katalog.Albumi.Count >= albuma, "vraćanje albuma");
        vm.Plejer.IsprazniRed();
    }

    private static async Task ProveriOmote(GlavniVm vm, Window prozor, string folder)
    {
        void Izbroj(string kada)
        {
            prozor.UpdateLayout();
            var plocice = Svi<Border>(Nadji<ZidPrikaz>(prozor)!).Where(b => b.Name == "Slika").ToList();
            int saOmotom = plocice.Count(b => b.Background is ImageBrush { ImageSource: not null });
            int bezSlike = plocice.Count(b => b.DataContext is AlbumKartica k && vm.Omoti.IzKesa(k.Album.Id) != null
                                              && b.Background is not ImageBrush { ImageSource: not null });
            Console.WriteLine($"{kada}: {plocice.Count} pločica, sa omotom {saOmotom}, omot postoji a ne vidi se {bezSlike}");
            Spisak.Tvrdi(bezSlike == 0, "omot postoji, a pločica ga ne prikazuje");
        }

        await Task.Delay(2500);
        Izbroj("posle učitavanja");
        foreach (var sirina in new[] { 1200.0, 1500, 1100, 1440 })
        {
            prozor.Width = sirina;
            await Task.Delay(700);
            Izbroj($"širina {sirina}");
        }
        vm.Prikaz = PrikazBiblioteke.Lista;
        await Task.Delay(500);
        vm.Prikaz = PrikazBiblioteke.Omoti;
        await Task.Delay(900);
        Izbroj("posle promene prikaza");
        vm.Katalog.Ucitaj();
        await Task.Delay(900);
        Izbroj("posle osvežavanja biblioteke");
        prozor.WindowState = WindowState.Maximized;
        await Task.Delay(900);
        Izbroj("uvećan prozor");
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "26_omoti.png"));
    }

    private static T? Nadji<T>(DependencyObject koren) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(koren); i++)
        {
            var dete = VisualTreeHelper.GetChild(koren, i);
            if (dete is T nadjeno) return nadjeno;
            if (Nadji<T>(dete) is { } dublje) return dublje;
        }
        return null;
    }

    /// <summary>Speed with a big library: first paint, typing in search, and scrolling the wall.</summary>
    private static async Task Veliko(GlavniVm vm, Window prozor, string folder)
    {
        await Task.Delay(1500);
        var zid = Nadji<ZidPrikaz>(prozor)!;
        var klizac = Nadji<ScrollViewer>(zid)!;
        Console.WriteLine($"zid: visina sadržaja {klizac.ExtentHeight:N0} px, redova {((ZidStrana)vm.Strana!).Redovi.Count:N0}");
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "20_veliko_omoti.png"));

        // Scrolling: each step waits for layout and render, and the worst step is what the eye notices.
        var okviri = new List<double>();
        double korak = 240;
        for (int i = 0; i < 240; i++)
        {
            var sat = Stopwatch.StartNew();
            klizac.ScrollToVerticalOffset(klizac.VerticalOffset + korak);
            await prozor.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            await prozor.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            okviri.Add(sat.Elapsed.TotalMilliseconds);
        }
        okviri.Sort();
        Console.WriteLine($"skrol: prosek {okviri.Average():F1} ms po koraku, 95% ispod {okviri[(int)(okviri.Count * 0.95)]:F1} ms, najgori {okviri[^1]:F1} ms; pozicija {klizac.VerticalOffset:N0}");
        await Task.Delay(1500);
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "21_veliko_skrol.png"));

        klizac.ScrollToVerticalOffset(klizac.ExtentHeight / 2);
        await Task.Delay(1000);

        foreach (var upit in new[] { "a", "love 90-e", "metal", "ekv", "" })
        {
            var sat = Stopwatch.StartNew();
            var strana = (ZidStrana)vm.Strana!;
            var pre = strana.Redovi.Count;
            vm.Upit = upit;
            await Task.Delay(190);
            while (sat.ElapsedMilliseconds < 3000 && strana.Redovi.Count == pre) await Task.Delay(5);
            Console.WriteLine($"pretraga \"{upit}\": {strana.Redovi.Count:N0} redova posle {sat.ElapsedMilliseconds - 180} ms");
        }

        var lista = Stopwatch.StartNew();
        vm.Prikaz = PrikazBiblioteke.Lista;
        var pesme = (PesmeStrana)vm.Strana!;
        while (pesme.Stavke.Count == 0 && lista.ElapsedMilliseconds < 8000) await Task.Delay(10);
        Console.WriteLine($"lista: {pesme.Stavke.Count:N0} pesama za {lista.ElapsedMilliseconds} ms");
        await Task.Delay(800);
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "22_veliko_lista.png"));

        vm.Prikaz = PrikazBiblioteke.AS;
        await Task.Delay(1500);
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "23_veliko_as.png"));
        vm.Prikaz = PrikazBiblioteke.Omoti;
        Console.WriteLine($"memorija: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");
    }

    private static async Task ProveriWindows(GlavniVm vm)
    {
        var upravnik = await Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var sesije = upravnik.GetSessions();
        var nasa = sesije.FirstOrDefault(s => s.SourceAppUserModelId.Contains("Testovi", StringComparison.OrdinalIgnoreCase));
        Console.WriteLine($"SMTC sesije: {string.Join(", ", sesije.Select(s => s.SourceAppUserModelId))}");
        Spisak.Tvrdi(nasa != null, "plejer nije u kontrolama Windowsa");

        await Task.Delay(800);
        var podaci = await nasa!.TryGetMediaPropertiesAsync();
        Console.WriteLine($"Windows vidi: {podaci.Title} / {podaci.Artist} / {podaci.AlbumTitle}, omot: {podaci.Thumbnail != null}");
        Spisak.Tvrdi(podaci.Title == vm.Plejer.Naslov, $"Windows vidi naslov \"{podaci.Title}\", a svira \"{vm.Plejer.Naslov}\"");

        Spisak.Tvrdi(vm.Plejer.Svira, "pre pauze treba da svira");
        await nasa.TryPauseAsync();
        await Cekaj(() => !vm.Plejer.Svira, 3000);
        Console.WriteLine("Pauza iz Windowsa radi");
        await nasa.TryPlayAsync();
        await Cekaj(() => vm.Plejer.Svira, 3000);
        var prethodna = vm.Plejer.Naslov;
        await nasa.TrySkipNextAsync();
        await Cekaj(() => vm.Plejer.Naslov != prethodna, 3000);
        Console.WriteLine($"Sledeća iz Windowsa radi: {vm.Plejer.Naslov}");
    }

    private static async Task ProveriKraj(GlavniVm vm, Window prozor, string folder)
    {
        prozor.WindowState = WindowState.Minimized;
        await Task.Delay(800);
        Spisak.Tvrdi(Application.Current.Windows.OfType<Window>().Count(w => w.IsVisible && w != prozor) == 0, "umanjen program ne sme da otvara drugi prozor");
        prozor.WindowState = WindowState.Normal;
        await Task.Delay(500);

        vm.Plejer.IsprazniRed();
        await Task.Delay(300);
        Spisak.Tvrdi(!vm.Plejer.Svira && !vm.Plejer.ImaRed && vm.Plejer.Tekuca == null, "red nije ispražnjen");
        Slikaj((FrameworkElement)prozor.Content, Path.Combine(folder, "25_prazan_red.png"));
    }
}
