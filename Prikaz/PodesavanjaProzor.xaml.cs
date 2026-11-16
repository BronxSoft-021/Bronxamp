using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Bronxamp.Vm;
using Microsoft.Win32;

namespace Bronxamp.Prikaz;

public partial class PodesavanjaProzor : Window
{
    private const string Podrazumevani = "Podrazumevani izlaz";
    private const string Automatski = "Automatski (prvi koji nije glavni)";

    private readonly GlavniVm _vm = App.Vm;

    private bool _jezikSpreman;

    public PodesavanjaProzor()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Sistem.ProzorStil.ZaobljeneIvice(this);

        var p = _vm.Podesavanja;
        Jezik.SelectedItem = Jezik.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string?)i.Tag == p.Jezik) ?? Jezik.Items[0];
        _jezikSpreman = true;
        OsveziFoldere();
        Prelaz.Value = p.Prelaz;
        PrelazUAlbumima.IsChecked = p.PrelazUAlbumima;
        LimiterPolje.IsChecked = p.Limiter;
        Nedelja.Text = p.UpozorenjeNedelja.ToString();

        var uredjaji = PlejerVm.StvarniUredjaji().Select(u => u.Naziv).ToList();
        Izlaz.ItemsSource = uredjaji.Prepend(Podrazumevani).ToList();
        Izlaz.SelectedItem = p.Izlaz != null && uredjaji.Contains(p.Izlaz) ? p.Izlaz : Podrazumevani;
        Slusalice.ItemsSource = uredjaji.Prepend(Automatski).ToList();
        Slusalice.SelectedItem = p.Slusalice != null && uredjaji.Contains(p.Slusalice) ? p.Slusalice : Automatski;

        StreamServer.Text = p.StreamServer;
        StreamPort.Text = p.StreamPort.ToString();
        StreamMount.Text = p.StreamMount;
        StreamKorisnik.Text = p.StreamKorisnik;
        LozinkaSkrivena.Password = Sistem.Zastita.Desifruj(p.StreamLozinka);
        StreamFormat.ItemsSource = new[] { "MP3", "AAC" };
        StreamFormat.SelectedItem = p.StreamFormat;
        StreamKbps.ItemsSource = new[] { 96, 128, 160, 192, 256, 320 };
        StreamKbps.SelectedItem = p.StreamKbps;
        Probno.IsChecked = p.StreamProbno;

        Verzija.Text = Verzije.Trenutna;
    }

    private void NaJezik(object sender, SelectionChangedEventArgs e)
    {
        if (!_jezikSpreman || Jezik.SelectedItem is not ComboBoxItem { Tag: string jezik }) return;
        _vm.Podesavanja.Jezik = jezik;
        _vm.Podesavanja.Sacuvaj();
        Rec.Jezik = jezik;
    }

    private void NaPrikaziLozinku(object sender, RoutedEventArgs e)
    {
        bool prikazi = LozinkaVidljiva.Visibility != Visibility.Visible;
        if (prikazi) LozinkaVidljiva.Text = LozinkaSkrivena.Password;
        else LozinkaSkrivena.Password = LozinkaVidljiva.Text;
        LozinkaVidljiva.Visibility = prikazi ? Visibility.Visible : Visibility.Collapsed;
        DugmeLozinka.Content = prikazi ? "Sakrij" : "Prikaži";
    }

    private void OsveziFoldere()
    {
        Folderi.ItemsSource = _vm.Baza.Folderi();
        int uklonjenih = _vm.Baza.BrojIskljucenihPesama();
        DugmeVrati.Content = $"Vrati uklonjeno ({Podaci.Vreme.BrojPesama(uklonjenih)})";
        DugmeVrati.Visibility = uklonjenih > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NaVratiAlbume(object sender, RoutedEventArgs e)
    {
        _vm.VratiAlbume();
        OsveziFoldere();
    }

    private void NaPrevlacenje(object sender, MouseButtonEventArgs e) => DragMove();

    private void NaTaster(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) DialogResult = false;
    }

    private void NaPrelaz(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        PrelazTekst.Text = e.NewValue == 0 ? "bez" : $"{e.NewValue:0.#} s";

    private void NaDodajFolder(object sender, RoutedEventArgs e)
    {
        var dijalog = new OpenFolderDialog { Title = "Izaberi folder sa muzikom" };
        if (dijalog.ShowDialog(this) != true) return;
        _vm.DodajFolder(dijalog.FolderName);
        OsveziFoldere();
    }

    private void NaUkloniFolder(object sender, RoutedEventArgs e)
    {
        if (Folderi.SelectedItem is not string folder) return;
        if (!Potvrda.Pitaj(this, "Ukloniti folder iz biblioteke?", $"{folder}\nFajlovi na disku se ne diraju, samo pesme nestaju iz plejera.")) return;
        _vm.UkloniFolder(folder);
        OsveziFoldere();
    }

    private void NaSkeniraj(object sender, RoutedEventArgs e) => _ = _vm.Skener.SkenirajAsync();

    private void NaOtkazi(object sender, RoutedEventArgs e) => DialogResult = false;

    private void NaSacuvaj(object sender, RoutedEventArgs e)
    {
        var p = _vm.Podesavanja;
        p.Prelaz = Math.Clamp(Prelaz.Value, 0, 12);
        p.Limiter = LimiterPolje.IsChecked == true;
        p.PrelazUAlbumima = PrelazUAlbumima.IsChecked == true;
        if (int.TryParse(Nedelja.Text, out int nedelja) && nedelja is >= 1 and <= 520) p.UpozorenjeNedelja = nedelja;

        var izlaz = Izlaz.SelectedItem as string;
        var noviIzlaz = izlaz == Podrazumevani ? null : izlaz;
        if (noviIzlaz != p.Izlaz)
        {
            p.Izlaz = noviIzlaz;
            _vm.Plejer.PostaviIzlaz(noviIzlaz ?? "Default");
        }
        var slusalice = Slusalice.SelectedItem as string;
        p.Slusalice = slusalice == Automatski ? null : slusalice;

        PrimeniEmitovanje();

        p.Sacuvaj();
        _vm.Plejer.PripremiSledecu();
        DialogResult = true;
    }

    /// <summary>Copies the stream fields from the window into settings; used by Save and Test veze.</summary>
    private void PrimeniEmitovanje()
    {
        var p = _vm.Podesavanja;
        p.StreamServer = StreamServer.Text.Trim();
        if (int.TryParse(StreamPort.Text, out int port) && port is > 0 and < 65536) p.StreamPort = port;
        p.StreamMount = StreamMount.Text.Trim();
        p.StreamKorisnik = StreamKorisnik.Text.Trim();
        p.StreamLozinka = Sistem.Zastita.Sifruj(LozinkaVidljiva.Visibility == Visibility.Visible ? LozinkaVidljiva.Text : LozinkaSkrivena.Password);
        p.StreamFormat = StreamFormat.SelectedItem as string ?? p.StreamFormat;
        p.StreamKbps = StreamKbps.SelectedItem as int? ?? p.StreamKbps;
        p.StreamProbno = Probno.IsChecked == true;
    }

    private void NaTestVeze(object sender, RoutedEventArgs e)
    {
        PrimeniEmitovanje();
        _vm.Podesavanja.Sacuvaj();
        var (uspeo, poruka) = _vm.Plejer.TestirajEmitovanje();
        MessageBox.Show(this, poruka, "Test veze", MessageBoxButton.OK,
            uspeo ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
