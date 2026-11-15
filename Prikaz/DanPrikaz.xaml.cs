using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Bronxamp.Podaci;
using Bronxamp.Vm;
using Microsoft.Win32;

namespace Bronxamp.Prikaz;

public sealed record VrstaIzbor(VrstaJingla Vrsta, string Naziv)
{
    public override string ToString() => Naziv;
}

public partial class DanPrikaz : UserControl
{
    public static IReadOnlyList<VrstaIzbor> Vrste { get; } = DanStrana.VrsteJinglova.Select(v => new VrstaIzbor(v, DanStrana.NazivVrste(v))).ToList();

    public DanPrikaz()
    {
        InitializeComponent();
        ListaDana.SizeChanged += (_, _) => Rastegni();
        ListaDana.SelectionChanged += (_, _) => App.Vm.Izabrana.Izaberi(
            ListaDana.SelectedItem is DanRed red ? DanStrana.Privremena(red.Stavka.Fajl, red.VrstaNaziv) : null);
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is DanStrana stara) stara.TekucaPromenjena -= Prati;
            if (e.NewValue is DanStrana nova) nova.TekucaPromenjena += Prati;
        };
    }

    private void Prati(DanRed red) => ListaDana.ScrollIntoView(red);

    private DanStrana? Strana => DataContext as DanStrana;

    /// <summary>Artist and title share what the fixed columns leave.</summary>
    private void Rastegni()
    {
        double ostalo = ListaDana.ActualWidth - 72 - 96 - 66 - 72 - 82 - 84 - 16;
        if (ostalo < 300) ostalo = 300;
        KolonaIzvodjac.Width = ostalo * 0.45;
        KolonaNaslov.Width = ostalo * 0.55;
    }

    private const string FormatPrevlacenja = "bronxamp-stavka-dana";

    private Point? _pocetakPrevlacenja;
    private DanRed? _vucen;

    private static DanRed? RedPod(object? izvor)
    {
        for (var e = izvor as DependencyObject; e != null; e = VisualTreeHelper.GetParent(e))
        {
            if (e is Button or GridViewColumnHeader) return null;
            if (e is ListBoxItem { DataContext: DanRed red }) return red;
        }
        return null;
    }

    private void NaPocetakPrevlacenja(object sender, MouseButtonEventArgs e)
    {
        _vucen = RedPod(e.OriginalSource);
        _pocetakPrevlacenja = _vucen == null ? null : e.GetPosition(ListaDana);
    }

    private void NaPrevlacenje(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pocetakPrevlacenja is not { } pocetak || _vucen is not { } vucen) return;
        var pomak = e.GetPosition(ListaDana) - pocetak;
        if (Math.Abs(pomak.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _pocetakPrevlacenja = null;
        DragDrop.DoDragDrop(ListaDana, new DataObject(FormatPrevlacenja, vucen), DragDropEffects.Move);
    }

    private void NaSpustanje(object sender, DragEventArgs e)
    {
        if (Strana is not { } strana || e.Data.GetData(FormatPrevlacenja) is not DanRed red) return;
        var cilj = RedPod(e.OriginalSource);
        strana.Premesti(red, cilj == null ? strana.Stavke.Count - 1 : strana.Stavke.IndexOf(cilj));
    }

    private void NaDesniKlik(object sender, MouseButtonEventArgs e)
    {
        if (Strana is not { } strana || RedPod(e.OriginalSource) is not { } red) return;

        // Right click inside a selection works on the whole selection, as in Winamp and AIMP.
        if (!ListaDana.SelectedItems.Contains(red))
        {
            ListaDana.SelectedItems.Clear();
            ListaDana.SelectedItems.Add(red);
        }
        var izabrani = ListaDana.SelectedItems.Cast<DanRed>().ToList();

        var meni = new ContextMenu();
        if (izabrani.Count == 1 && App.Vm.Plejer.MozePreslusavanje)
            meni.Items.Add(KontekstMeni.Stavka("Preslušaj na slušalicama", () => strana.Preslusaj(red)));
        meni.Items.Add(KontekstMeni.Stavka(izabrani.Count > 1 ? $"Obriši izabrano ({izabrani.Count})" : "Obriši iz plejliste",
            () => strana.UkloniVise(izabrani)));
        meni.Items.Add(KontekstMeni.Stavka("Obriši sve", strana.UkloniSve));
        meni.Items.Add(new Separator());
        meni.Items.Add(KontekstMeni.Stavka("Dodaj jingl posle ovoga...", () => DodajJingl(strana, red)));
        meni.Items.Add(KontekstMeni.Stavka("Dodaj pesmu posle ovoga...", () => DodajPesmu(strana, red)));
        meni.PlacementTarget = ListaDana;
        meni.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>Ctrl+A selects everything, Delete removes the selection — as in a classic player.</summary>
    private void NaTasterDana(object sender, KeyEventArgs e)
    {
        if (Strana is not { } strana) return;
        if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ListaDana.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            var izabrani = ListaDana.SelectedItems.Cast<DanRed>().ToList();
            if (izabrani.Count == 0) return;
            strana.UkloniVise(izabrani);
            e.Handled = true;
        }
    }

    private void NaObrisiSve(object sender, RoutedEventArgs e) => Strana?.UkloniSve();

    private void DodajJingl(DanStrana strana, DanRed posle)
    {
        var izbor = new IzborJingla(strana.SviJinglovi) { Owner = Window.GetWindow(this) };
        if (izbor.ShowDialog() == true && izbor.Izabran?.Putanja is { } putanja)
            strana.Ubaci(posle, strana.ProcitajJingl(putanja), VrstaStavke.Jingl);
    }

    private void DodajPesmu(DanStrana strana, DanRed posle)
    {
        var dijalog = new OpenFileDialog { Title = "Izaberi pesme", Filter = FilterMuzike, InitialDirectory = strana.FolderSema, Multiselect = true };
        if (dijalog.ShowDialog() != true) return;

        // Each goes right after the one before it, so they keep the order they were picked in.
        var red = posle;
        foreach (var putanja in dijalog.FileNames)
        {
            strana.Ubaci(red, strana.ProcitajPesmu(putanja), VrstaStavke.Pesma);
            red = strana.Stavke[Math.Min(strana.Stavke.IndexOf(red) + 1, strana.Stavke.Count - 1)];
        }
    }

    private void NaIznova(object sender, RoutedEventArgs e)
    {
        if (Strana is not { } strana) return;
        if (!Potvrda.Pitaj(Window.GetWindow(this)!, "Složiti dan iznova?",
                "Ručne izmene u listi za izabrani dan se brišu, a dan se slaže iz foldera iznova. Muzika na disku se ne dira.", "Složi iznova"))
            return;
        strana.Ocisti();
        strana.Osvezi();
        _ = strana.Slozi();
    }

    private void NaIsprazni(object sender, RoutedEventArgs e)
    {
        if (Strana is not { } strana) return;
        if (!Potvrda.Pitaj(Window.GetWindow(this)!, "Isprazniti dan?",
                "Briše sve što je u danu za izabrani datum: emisije, plejliste i hit. Spisak ostaje prazan da ga sastaviš iznova. Muzika na disku se ne dira.",
                "Isprazni"))
            return;
        strana.Ocisti();
        strana.Stanje = "Dan je ispražnjen. Dodaj šta ide u dan — spisak se slaže sam.";
    }

    private void NaBlok(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is BlokDana { JeKraj: false } blok) blok.Otvoren = !blok.Otvoren;
    }

    private void NaNapredno(object sender, RoutedEventArgs e)
    {
        if (Strana is { } strana) strana.NaprednoOtvoreno = !strana.NaprednoOtvoreno;
    }

    private void NaOznakeJinglova(object sender, RoutedEventArgs e)
    {
        if (Strana is { } strana) strana.Tab = TabDana.Jinglovi;
    }

    /// <summary>A folder or a file dragged in from Explorer joins the day, as "+ Dodaj" would add it.</summary>
    private static string[] Prevuceno(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] putanje
            ? putanje.Where(p => Directory.Exists(p) || (File.Exists(p) && Tagovi.JeMuzika(p))).ToArray()
            : [];

    private void NaPrevlacenjeFoldera(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(FormatPrevlacenja)) return;
        e.Effects = Strana?.MozeIzmena == true && Prevuceno(e).Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void NaSpustanjeFoldera(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(FormatPrevlacenja) || Strana is not { MozeIzmena: true } strana) return;
        foreach (var putanja in Prevuceno(e)) strana.DodajEmisiju(putanja);
        e.Handled = true;
    }

    private void NaZaustavi(object sender, RoutedEventArgs e)
    {
        if (Strana is not { } strana) return;
        var prozor = Window.GetWindow(this)!;
        if (!strana.Aktivan || Potvrda.Pitaj(prozor, "Zaustaviti dan?", "Muzika prestaje da svira.", "Zaustavi")) strana.Zaustavi();
    }

    private void NaIzaberiHit(object sender, RoutedEventArgs e)
    {
        if (Strana is not { } strana) return;
        var dijalog = new OpenFileDialog { Title = "Izaberi hit dana", Filter = FilterMuzike, InitialDirectory = strana.FolderSema };
        if (dijalog.ShowDialog() != true) return;
        var izbor = new IzborFajla(dijalog.FileName, Path.GetFileNameWithoutExtension(dijalog.FileName));
        strana.Hitovi.Add(izbor);
        strana.IzabranHit = izbor;
    }

    private const string FilterMuzike = "Muzika|*.mp3;*.flac;*.ogg;*.opus;*.m4a;*.wav";

    private void NaDodajEmisiju(object sender, RoutedEventArgs e)
    {
        if (Strana is not { } strana) return;

        var meni = new ContextMenu();
        foreach (var h in strana.Hitovi.Where(h => h != strana.IzabranHit).ToList())
            meni.Items.Add(KontekstMeni.Stavka(h.Naziv, () => strana.DodajEmisiju(h.Putanja!)));
        foreach (var p in strana.Plejliste.ToList())
            meni.Items.Add(KontekstMeni.Stavka(p.Naziv, () => strana.DodajEmisiju(p.Folder)));
        if (meni.Items.Count > 0) meni.Items.Add(new Separator());
        meni.Items.Add(KontekstMeni.Stavka("Izaberi fajl...", () =>
        {
            var dijalog = new OpenFileDialog { Title = "Izaberi emisiju", Filter = FilterMuzike, InitialDirectory = strana.FolderSema };
            if (dijalog.ShowDialog() == true) strana.DodajEmisiju(dijalog.FileName);
        }));
        meni.Items.Add(KontekstMeni.Stavka("Izaberi folder...", () =>
        {
            var dijalog = new OpenFolderDialog { Title = "Izaberi folder sa emisijom", InitialDirectory = strana.FolderSema };
            if (dijalog.ShowDialog() == true) strana.DodajEmisiju(dijalog.FolderName);
        }));
        meni.PlacementTarget = DugmeEmisija;
        meni.IsOpen = true;
    }

    private void NaUkloniEmisiju(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is EmisijaRed red) Strana?.UkloniEmisiju(red);
    }

    private void NaPlejlistaGore(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PlejlistaRed red) Strana?.PomeriPlejlistu(red, -1);
    }

    private void NaPlejlistaDole(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PlejlistaRed red) Strana?.PomeriPlejlistu(red, 1);
    }

    private void NaGore(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DanRed red) Strana?.Pomeri(red, -1);
    }

    private void NaDole(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DanRed red) Strana?.Pomeri(red, 1);
    }

    private void NaUkloni(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DanRed red) Strana?.Ukloni(red);
    }

    private void NaFolderSema(object sender, RoutedEventArgs e)
    {
        var dijalog = new OpenFolderDialog { Title = "Folder sa šemom dana", InitialDirectory = Strana?.FolderSema };
        if (dijalog.ShowDialog() == true) Strana?.PostaviFolderSema(dijalog.FolderName);
    }

    private void NaFolderJinglovi(object sender, RoutedEventArgs e)
    {
        var dijalog = new OpenFolderDialog { Title = "Folder sa jinglovima", InitialDirectory = Strana?.FolderJinglovi };
        if (dijalog.ShowDialog() == true) Strana?.PostaviFolderJinglovi(dijalog.FolderName);
    }
}
