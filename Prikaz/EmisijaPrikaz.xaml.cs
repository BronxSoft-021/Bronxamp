using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Bronxamp.Vm;

namespace Bronxamp.Prikaz;

public partial class EmisijaPrikaz : UserControl
{
    private const string FormatPrevlacenja = "plejer-red-emisije";

    private Point? _pocetak;
    private PesmaRed? _vucen;

    public EmisijaPrikaz()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is EmisijaStrana stara) stara.Stavke.CollectionChanged -= NaIzmenu;
            if (e.NewValue is EmisijaStrana nova) nova.Stavke.CollectionChanged += NaIzmenu;
            OsveziPrazno();
        };
    }

    private EmisijaStrana? Strana => DataContext as EmisijaStrana;

    private void NaIzmenu(object? sender, NotifyCollectionChangedEventArgs e) => OsveziPrazno();

    private void OsveziPrazno() =>
        Prazna.Visibility = Strana is { Stavke.Count: 0, PrikaziIstoriju: false } ? Visibility.Visible : Visibility.Collapsed;

    private void NaPusti(object sender, RoutedEventArgs e)
    {
        if (Strana is { } s) App.Vm.PustiEmisiju(s.Emisija);
    }

    private void NaUkloni(object sender, RoutedEventArgs e)
    {
        if (Lista.SelectedItems.Count == 0)
        {
            App.Vm.PrikaziPoruku("Prvo izaberi pesme. Ctrl+A bira sve.");
            return;
        }
        Ukloni();
    }

    private void NaIsprazni(object sender, RoutedEventArgs e)
    {
        if (Strana is not { Stavke.Count: > 0 } s) return;
        if (!Potvrda.Pitaj(Window.GetWindow(this)!, $"Isprazniti emisiju „{s.Emisija.Ime}“?",
                "Sve pesme izlaze iz spiska. Emisija i istorija emitovanja ostaju.", "Isprazni"))
            return;
        s.Stavke.Clear();
        App.Vm.SacuvajEmisiju(s);
    }

    private void UkloniAlbum(Podaci.Album album)
    {
        if (Strana is not { } s) return;
        foreach (var red in s.Stavke.Where(r => r.Pesma.AlbumId == album.Id).ToList()) s.Stavke.Remove(red);
        App.Vm.SacuvajEmisiju(s);
    }

    private void NaIzmeni(object sender, RoutedEventArgs e)
    {
        if (Strana is not { } s) return;
        var dijalog = new EmisijaDijalog(s.Emisija.Ime, s.Emisija.Boja) { Owner = Window.GetWindow(this) };
        if (dijalog.ShowDialog() == true) App.Vm.IzmeniEmisiju(s.Emisija, dijalog.Ime, dijalog.Boja);
    }

    private void NaObrisi(object sender, RoutedEventArgs e)
    {
        if (Strana is not { } s) return;
        if (Potvrda.Pitaj(Window.GetWindow(this)!, $"Obrisati emisiju „{s.Emisija.Ime}“?",
                "Spisak pesama i istorija emitovanja nestaju. Puštanja ostaju u statistici."))
            App.Vm.ObrisiEmisiju(s.Emisija);
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

    private void NaUkloniRed(object sender, RoutedEventArgs e)
    {
        if (Strana is not { } s || ((FrameworkElement)sender).DataContext is not PesmaRed red) return;
        s.Stavke.Remove(red);
        App.Vm.SacuvajEmisiju(s);
    }

    private void NaDvoklik(object sender, MouseButtonEventArgs e)
    {
        if (Strana is { } s && RedPod(e.OriginalSource) is { } red)
            App.Vm.PustiEmisiju(s.Emisija, s.Stavke.IndexOf(red));
    }

    private void NaTaster(object sender, KeyEventArgs e)
    {
        if (Strana is not { } s) return;
        if (e.Key == Key.Delete) Ukloni();
        else if (e.Key == Key.Enter && Lista.SelectedItem is PesmaRed red) App.Vm.PustiEmisiju(s.Emisija, s.Stavke.IndexOf(red));
    }

    private void Ukloni()
    {
        if (Strana is not { } s) return;
        foreach (var red in Lista.SelectedItems.Cast<PesmaRed>().ToList()) s.Stavke.Remove(red);
        App.Vm.SacuvajEmisiju(s);
    }

    private void Pomeri(PesmaRed red, int za)
    {
        if (Strana is not { } s) return;
        int indeks = s.Stavke.IndexOf(red), novi = indeks + za;
        if (indeks < 0 || novi < 0 || novi >= s.Stavke.Count) return;
        s.Stavke.Move(indeks, novi);
        App.Vm.SacuvajEmisiju(s);
    }

    private void NaDesniKlik(object sender, MouseButtonEventArgs e)
    {
        if (Strana is not { } s || RedPod(e.OriginalSource) is not { } red) return;
        if (!Lista.SelectedItems.Contains(red))
        {
            Lista.SelectedItems.Clear();
            Lista.SelectedItems.Add(red);
        }

        var pesme = Lista.SelectedItems.Cast<PesmaRed>().Select(r => r.Pesma).ToList();
        var meni = KontekstMeni.ZaPesme(pesme, () => App.Vm.PustiEmisiju(s.Emisija, s.Stavke.IndexOf(red)));
        meni.Items.Add(new Separator());
        meni.Items.Add(KontekstMeni.Stavka("Pomeri gore", () => Pomeri(red, -1)));
        meni.Items.Add(KontekstMeni.Stavka("Pomeri dole", () => Pomeri(red, 1)));
        meni.Items.Add(KontekstMeni.Stavka("Ukloni iz emisije", Ukloni));
        meni.Items.Add(KontekstMeni.Stavka("Ukloni ceo album iz emisije", () => UkloniAlbum(red.Pesma.Album)));
        meni.PlacementTarget = Lista;
        meni.IsOpen = true;
        e.Handled = true;
    }

    private void NaPocetakPrevlacenja(object sender, MouseButtonEventArgs e)
    {
        _vucen = RedPod(e.OriginalSource);
        _pocetak = _vucen == null ? null : e.GetPosition(Lista);
    }

    private void NaPrevlacenje(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pocetak is not Point pocetak || _vucen == null) return;
        var pomak = e.GetPosition(Lista) - pocetak;
        if (Math.Abs(pomak.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var vucen = _vucen;
        _pocetak = null;
        DragDrop.DoDragDrop(Lista, new DataObject(FormatPrevlacenja, vucen), DragDropEffects.Move);
    }

    private void NaSpustanje(object sender, DragEventArgs e)
    {
        if (Strana is not { } s || e.Data.GetData(FormatPrevlacenja) is not PesmaRed vucen) return;
        int odakle = s.Stavke.IndexOf(vucen);
        int dokle = RedPod(e.OriginalSource) is { } cilj ? s.Stavke.IndexOf(cilj) : s.Stavke.Count - 1;
        if (odakle < 0 || dokle < 0 || odakle == dokle) return;
        s.Stavke.Move(odakle, dokle);
        App.Vm.SacuvajEmisiju(s);
    }
}
