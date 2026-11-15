using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Bronxamp.Podaci;
using Bronxamp.Vm;

namespace Bronxamp.Prikaz;

/// <summary>A column the user can switch on. <see cref="Polje"/> is a property of <see cref="PesmaRed"/>.</summary>
public sealed record KolonaPesme(string Kljuc, string Naziv, string Polje, double Sirina, bool Desno = false);

public partial class PesmeLista : UserControl
{
    private const double NajuzaGlavna = 200;

    /// <summary>The order of the menu. On screen the order is the user's: <see cref="Podaci.Podesavanja.KoloneSpiska"/>.</summary>
    public static readonly KolonaPesme[] Izbor =
    [
        new("Izvodjac", "Izvođač", nameof(PesmaRed.Izvodjac), 280),
        new("Naslov", "Naslov", nameof(PesmaRed.Naslov), 300),
        new("Trajanje", "Trajanje", nameof(PesmaRed.Trajanje), 72, true),
        new("Album", "Album", nameof(PesmaRed.Album), 220),
        new("Godina", "Godina", nameof(PesmaRed.Godina), 60),
        new("Zanr", "Žanr", nameof(PesmaRed.Zanr), 130),
        new("Broj", "Broj pesme", nameof(PesmaRed.Broj), 76),
        new("Pustano", "Puštano", nameof(PesmaRed.Pustano), 70, true),
        new("Poslednji", "Poslednji put", nameof(PesmaRed.Poslednji), 104),
        new("Dodato", "Dodato", nameof(PesmaRed.Dodato), 96),
        new("Omiljeno", "Omiljeno", nameof(PesmaRed.OmiljenoZnak), 70),
        new("Format", "Format", nameof(PesmaRed.Format), 62),
        new("Bitrate", "Bitrate", nameof(PesmaRed.Bitrate), 80, true),
        new("Frekvencija", "Frekvencija", nameof(PesmaRed.Frekvencija), 90, true),
        new("Velicina", "Veličina", nameof(PesmaRed.Velicina), 80, true),
        new("Fajl", "Naziv fajla", nameof(PesmaRed.Fajl), 240),
        new("Folder", "Folder", nameof(PesmaRed.Folder), 280),
    ];

    private readonly Dictionary<GridViewColumn, string> _kljucevi = [];
    private GridViewColumn? _glavna;
    private GridViewColumn? _prva;
    private GridViewColumn? _alati;

    public PesmeLista()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => NapraviKolone();
        Lista.SizeChanged += (_, _) => Rastegni();
        Lista.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(NaKrajPovlacenja));
        Kolone.CollectionChanged += NaPomeranjeKolone;
        Lista.SelectionChanged += (_, _) => App.Vm.Izabrana.Izaberi((Lista.SelectedItem as PesmaRed)?.Pesma);
    }

    private GridViewColumnCollection Kolone => ((GridView)Lista.View).Columns;

    private PesmeStrana? Strana => DataContext as PesmeStrana;

    private static Podaci.Podesavanja Podesavanja => App.Vm.Podesavanja;

    private void NapraviKolone()
    {
        if (Strana is not { } strana) return;

        var kolone = Kolone;
        kolone.Clear();
        _kljucevi.Clear();

        _prva = new GridViewColumn { Header = "#", Width = 34, CellTemplate = (DataTemplate)Resources["CelijaRb"] };
        kolone.Add(_prva);

        foreach (var kljuc in Podesavanja.KoloneSpiska)
            if (Izbor.FirstOrDefault(k => k.Kljuc == kljuc) is { } k)
                kolone.Add(Kolona(k));
        if (strana.PrikaziDodatak)
            kolone.Add(Kolona(new KolonaPesme("Dodatak", "Slušano", nameof(PesmaRed.Dodatak), 70)));

        // The title takes what is left, or the artist when the title is switched off.
        _glavna = Kolona("Naslov") ?? Kolona("Izvodjac");

        var izbor = new Button { Style = (Style)Resources["AlatReda"], Content = "", ToolTip = "Izaberi šta se vidi u spisku" };
        izbor.Click += (_, _) => OtvoriMeniKolona();
        _alati = new GridViewColumn { Header = izbor, Width = 84, CellTemplate = (DataTemplate)Resources["CelijaAlati"] };
        kolone.Add(_alati);

        Rastegni();
    }

    private GridViewColumn? Kolona(string kljuc) => _kljucevi.FirstOrDefault(k => k.Value == kljuc).Key;

    /// <summary>
    /// A column dragged to a new place. The arrow column stays first and the buttons last, because the grid
    /// itself cannot pin a column; it does not allow changes during this event, hence the invoke.
    /// </summary>
    private void NaPomeranjeKolone(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Move) return;

        Dispatcher.BeginInvoke(() =>
        {
            var kolone = Kolone;
            if (_prva != null && kolone.IndexOf(_prva) > 0) kolone.Move(kolone.IndexOf(_prva), 0);
            if (_alati != null && kolone.IndexOf(_alati) is var i && i >= 0 && i < kolone.Count - 1) kolone.Move(i, kolone.Count - 1);

            Podesavanja.KoloneSpiska = kolone
                .Where(k => _kljucevi.TryGetValue(k, out var kljuc) && Izbor.Any(x => x.Kljuc == kljuc))
                .Select(k => _kljucevi[k])
                .ToList();
            Podesavanja.Sacuvaj();
        });
    }

    private GridViewColumn Kolona(KolonaPesme k)
    {
        var tekst = new FrameworkElementFactory(typeof(TextBlock));
        tekst.SetBinding(TextBlock.TextProperty, new Binding(k.Polje));
        tekst.SetResourceReference(TextBlock.ForegroundProperty, "Mutan");
        tekst.SetValue(TextBlock.FontSizeProperty, 12.5);
        tekst.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        tekst.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        if (k.Desno) tekst.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Right);

        var kolona = new GridViewColumn
        {
            Header = k.Naziv,
            Width = Podesavanja.SirineKolona.GetValueOrDefault(k.Kljuc, k.Sirina),
            CellTemplate = k.Kljuc switch
            {
                "Naslov" => (DataTemplate)Resources["CelijaNaslov"],
                "Izvodjac" => (DataTemplate)Resources["CelijaIzvodjac"],
                _ => new DataTemplate { VisualTree = tekst },
            },
        };
        if (k.Desno) kolona.HeaderContainerStyle = (Style)Application.Current.FindResource("ZaglavljeDesno");
        _kljucevi[kolona] = k.Kljuc;
        return kolona;
    }

    /// <summary>One column takes whatever the others leave.</summary>
    private void Rastegni()
    {
        if (_glavna == null || Lista.ActualWidth <= 0) return;

        // Next to the folder tree the list is narrow: the artist gives way, unless the user set its width.
        if (Kolona("Izvodjac") is { } izvodjac && izvodjac != _glavna && !Podesavanja.SirineKolona.ContainsKey("Izvodjac"))
            izvodjac.Width = Math.Clamp(Lista.ActualWidth * 0.32, 140, 280);

        double ostale = Kolone.Where(k => k != _glavna).Sum(k => k.ActualWidth > 0 ? k.ActualWidth : k.Width);
        _glavna.Width = Math.Max(NajuzaGlavna, Lista.ActualWidth - ostale - 16);
    }

    private void NaKrajPovlacenja(object sender, DragCompletedEventArgs e)
    {
        foreach (var (kolona, kljuc) in _kljucevi)
            Podesavanja.SirineKolona[kljuc] = kolona.ActualWidth;
        Podesavanja.Sacuvaj();
        Rastegni();
    }

    /// <summary>The menu stays open, so several columns can be ticked in one go.</summary>
    public ContextMenu MeniKolona()
    {
        var meni = new ContextMenu();
        foreach (var k in Izbor)
        {
            var stavka = new MenuItem { Header = k.Naziv, IsCheckable = true, StaysOpenOnClick = true, IsChecked = Podesavanja.KoloneSpiska.Contains(k.Kljuc) };
            stavka.Click += (_, _) =>
            {
                var izabrane = Podesavanja.KoloneSpiska;
                if (!stavka.IsChecked && izabrane.Count == 1)
                {
                    stavka.IsChecked = true;
                    return;
                }

                // A new column goes to the end, and from there the user drags it where it belongs.
                izabrane.Remove(k.Kljuc);
                if (stavka.IsChecked) izabrane.Add(k.Kljuc);
                Podesavanja.Sacuvaj();
                NapraviKolone();
            };
            meni.Items.Add(stavka);
        }
        meni.Items.Add(new Separator());
        meni.Items.Add(KontekstMeni.Stavka("Vrati podrazumevano", () =>
        {
            Podesavanja.KoloneSpiska = [.. Podaci.Podesavanja.PodrazumevaneKolone];
            Podesavanja.SirineKolona.Clear();
            Podesavanja.Sacuvaj();
            NapraviKolone();
        }));
        return meni;
    }

    private void OtvoriMeniKolona()
    {
        var meni = MeniKolona();
        meni.PlacementTarget = Lista;
        meni.IsOpen = true;
    }

    private void NaPustiAlbum(object sender, RoutedEventArgs e)
    {
        if (Strana?.Album is { } album) App.Vm.PustiAlbum(album);
    }

    private void NaAlbumSledece(object sender, RoutedEventArgs e)
    {
        if (Strana?.Album is { } album) App.Vm.DodajSledece(album.Pesme);
    }

    private void NaAlbumURed(object sender, RoutedEventArgs e)
    {
        if (Strana?.Album is { } album) App.Vm.DodajURed(album.Pesme);
    }

    private void NaAlbumUkloni(object sender, RoutedEventArgs e)
    {
        if (Strana?.Album is { } album) KontekstMeni.UkloniAlbum(album);
    }

    private void NaSledece(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PesmaRed red) App.Vm.DodajSledece([red.Pesma]);
    }

    private void NaURed(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PesmaRed red) App.Vm.DodajURed([red.Pesma]);
    }

    /// <summary>On a selected row the button means the whole selection, like the right-click menu.</summary>
    private void NaUkloni(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not PesmaRed red || Strana?.MozeUklanjanje != true) return;
        KontekstMeni.UkloniPesme(Lista.SelectedItems.Contains(red) ? Izabrane() : [red.Pesma]);
    }

    private List<Pesma> Izabrane() => Lista.SelectedItems.Cast<PesmaRed>().Select(r => r.Pesma).ToList();

    private void Pusti(PesmaRed red)
    {
        if (Strana is not { } strana) return;
        int indeks = strana.Stavke.ToList().IndexOf(red);
        if (indeks < 0) return;

        if (strana.Album is { } album && album.Pesme.Contains(red.Pesma))
            App.Vm.PustiAlbum(album, album.Pesme.IndexOf(red.Pesma));
        else
            App.Vm.PustiPesme(strana.Stavke.Select(s => s.Pesma).ToList(), indeks, strana.NazivReda);
    }

    private static PesmaRed? RedPod(object? izvor)
    {
        for (var e = izvor as DependencyObject; e != null; e = VisualTreeHelper.GetParent(e))
        {
            if (e is Button) return null;
            if (e is ListBoxItem { DataContext: PesmaRed red }) return red;
        }
        return null;
    }

    private static bool NaZaglavlju(object? izvor)
    {
        for (var e = izvor as DependencyObject; e != null; e = VisualTreeHelper.GetParent(e))
            if (e is GridViewColumnHeader) return true;
        return false;
    }

    private void NaDvoklik(object sender, MouseButtonEventArgs e)
    {
        if (RedPod(e.OriginalSource) is { } red) Pusti(red);
    }

    private void NaTaster(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Lista.SelectedItem is PesmaRed red)
        {
            Pusti(red);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && Lista.SelectedItems.Count > 0 && Strana?.MozeUklanjanje == true)
        {
            KontekstMeni.UkloniPesme(Izabrane());
            e.Handled = true;
        }
    }

    private void NaDesniKlik(object sender, MouseButtonEventArgs e)
    {
        if (NaZaglavlju(e.OriginalSource))
        {
            OtvoriMeniKolona();
            e.Handled = true;
            return;
        }

        if (RedPod(e.OriginalSource) is not { } red) return;

        if (!Lista.SelectedItems.Contains(red))
        {
            Lista.SelectedItems.Clear();
            Lista.SelectedItems.Add(red);
        }

        var meni = KontekstMeni.ZaPesme(Izabrane(), () => Pusti(red), saUklanjanjem: Strana?.MozeUklanjanje == true);
        meni.PlacementTarget = Lista;
        meni.IsOpen = true;
        e.Handled = true;
    }
}
