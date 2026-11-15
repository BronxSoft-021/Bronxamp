using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Bronxamp.Vm;

namespace Bronxamp.Prikaz;

public partial class ZidPrikaz : UserControl
{
    private const double SirinaKlizaca = 14;

    public ZidPrikaz() => InitializeComponent();

    private void NaPromenuVelicine(object sender, SizeChangedEventArgs e) => JaviSirinu();

    private void NaPromenuPodataka(object sender, DependencyPropertyChangedEventArgs e) => JaviSirinu();

    private void JaviSirinu()
    {
        if (DataContext is ZidStrana zid && ActualWidth > 0)
            zid.PromeniSirinu(ActualWidth - SirinaKlizaca);
    }

    private void NaKlikPlocice(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is AlbumKartica k)
            App.Vm.PustiAlbum(k.Album);
    }

    private void NaAlat(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AlbumKartica k, Tag: string radnja }) return;
        var vm = App.Vm;
        switch (radnja)
        {
            case "pusti": vm.PustiAlbum(k.Album); break;
            case "sledece": vm.DodajSledece(k.Album.Pesme); break;
            case "red": vm.DodajURed(k.Album.Pesme); break;
            case "pesme": vm.OtvoriAlbum(k.Album); break;
            case "ukloni": KontekstMeni.UkloniAlbum(k.Album); break;
        }
    }

    private void NaDesniKlikPlocice(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is AlbumKartica k)
        {
            var meni = KontekstMeni.ZaAlbum(k.Album);
            meni.PlacementTarget = (UIElement)sender;
            meni.IsOpen = true;
            e.Handled = true;
        }
    }
}
