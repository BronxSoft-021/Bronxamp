using System.Windows.Controls;
using Bronxamp.Podaci;
using Bronxamp.Vm;

namespace Bronxamp.Prikaz;

/// <summary>Right-click menus, built when opened so the list of shows is always current.</summary>
public static class KontekstMeni
{
    public static MenuItem Stavka(string tekst, Action akcija, bool dostupno = true)
    {
        var s = new MenuItem { Header = tekst, IsEnabled = dostupno };
        s.Click += (_, _) => akcija();
        return s;
    }

    private static MenuItem PodmeniEmisija(IReadOnlyList<Pesma> pesme)
    {
        var vm = App.Vm;
        var podmeni = new MenuItem { Header = "Dodaj u emisiju" };
        foreach (var n in vm.NavEmisije)
            if (n.Emisija is { } emisija)
                podmeni.Items.Add(Stavka(emisija.Ime, () => vm.DodajUEmisiju(emisija, pesme)));

        if (vm.NavEmisije.Count > 0) podmeni.Items.Add(new Separator());
        podmeni.Items.Add(Stavka("Nova emisija...", () =>
        {
            var dijalog = new EmisijaDijalog { Owner = System.Windows.Application.Current.MainWindow };
            if (dijalog.ShowDialog() != true) return;
            vm.NovaEmisija(dijalog.Ime, dijalog.Boja);
            if (vm.NavEmisije.LastOrDefault()?.Emisija is { } nova) vm.DodajUEmisiju(nova, pesme);
        }));
        return podmeni;
    }

    public static ContextMenu ZaAlbum(Album album)
    {
        var vm = App.Vm;
        var meni = new ContextMenu();
        meni.Items.Add(Stavka("Pusti album", () => vm.PustiAlbum(album)));
        meni.Items.Add(Stavka("Prikaži pesme", () => vm.OtvoriAlbum(album)));
        meni.Items.Add(new Separator());
        meni.Items.Add(Stavka("Pusti sledeće", () => vm.DodajSledece(album.Pesme)));
        meni.Items.Add(Stavka("Dodaj u red", () => vm.DodajURed(album.Pesme)));
        meni.Items.Add(PodmeniEmisija(album.Pesme));
        meni.Items.Add(new Separator());
        meni.Items.Add(Stavka(album.Pesme.All(p => p.Omiljeno) ? "Ukloni iz omiljenog" : "Dodaj u omiljeno", () => vm.PromeniOmiljeno(album.Pesme)));
        meni.Items.Add(Stavka("Otvori folder", () => GlavniVm.OtvoriFolder(album.Pesme[0])));
        meni.Items.Add(new Separator());
        meni.Items.Add(Stavka("Ukloni iz biblioteke", () => UkloniAlbum(album)));
        return meni;
    }

    public static void UkloniAlbum(Album album)
    {
        var prozor = System.Windows.Application.Current.MainWindow!;
        if (Potvrda.Pitaj(prozor, $"Ukloniti album „{album.Naziv}“ iz biblioteke?",
                "Fajlovi na disku ostaju. Album možeš da vratiš u Podešavanjima.", "Ukloni"))
            App.Vm.UkloniAlbum(album);
    }

    /// <summary>Menu of the queue in the right panel.</summary>
    public static ContextMenu ZaRed(SlediStavka stavka, IReadOnlyCollection<SlediStavka> izabrane)
    {
        var plejer = App.Vm.Plejer;
        var meni = new ContextMenu();
        if (!stavka.Tekuca) meni.Items.Add(Stavka("Pusti", () => plejer.SkociNa(stavka.Unapred)));
        meni.Items.Add(Stavka(izabrane.Count > 1 ? $"Ukloni iz reda ({izabrane.Count})" : "Ukloni iz reda", () => plejer.UkloniIzReda(izabrane)));
        meni.Items.Add(Stavka("Ukloni ceo album iz reda", () => plejer.UkloniAlbumIzReda(stavka.Pesma.Album)));
        meni.Items.Add(new Separator());
        meni.Items.Add(Stavka("Isprazni red", plejer.IsprazniRed));
        return meni;
    }

    /// <summary>Hides songs from the library. The files stay on disk, so the question says so.</summary>
    public static void UkloniPesme(IReadOnlyList<Pesma> pesme)
    {
        var prozor = System.Windows.Application.Current.MainWindow!;
        var pitanje = pesme.Count == 1 ? $"Ukloniti pesmu „{pesme[0].Naslov}“ iz spiska?" : $"Ukloniti {Vreme.BrojPesama(pesme.Count)} iz spiska?";
        if (Potvrda.Pitaj(prozor, pitanje, "Fajlovi na disku ostaju. Vraćaju se u Podešavanjima, dugmetom „Vrati uklonjeno“.", "Ukloni"))
            App.Vm.UkloniPesme(pesme);
    }

    public static ContextMenu ZaPesme(IReadOnlyList<Pesma> pesme, Action pusti, bool saUklanjanjem = false)
    {
        var vm = App.Vm;
        var prva = pesme[0];
        var meni = new ContextMenu();
        meni.Items.Add(Stavka("Pusti", pusti));
        meni.Items.Add(Stavka("Pusti sledeće", () => vm.DodajSledece(pesme)));
        meni.Items.Add(Stavka("Dodaj u red", () => vm.DodajURed(pesme)));
        // Files that are only being browsed are not in the library, so shows and favourites cannot hold them.
        bool uBiblioteci = prva.Id > 0;
        if (uBiblioteci) meni.Items.Add(PodmeniEmisija(pesme));
        if (vm.Plejer.MozePreslusavanje)
            meni.Items.Add(Stavka("Preslušaj na slušalicama", () => vm.Plejer.Preslusaj(prva)));
        meni.Items.Add(new Separator());
        if (uBiblioteci)
        {
            meni.Items.Add(Stavka(pesme.All(p => p.Omiljeno) ? "Ukloni iz omiljenog" : "Dodaj u omiljeno", () => vm.PromeniOmiljeno(pesme)));
            meni.Items.Add(Stavka("Otvori album", () => vm.OtvoriAlbum(prva.Album)));
        }
        meni.Items.Add(Stavka("Otvori folder", () => GlavniVm.OtvoriFolder(prva)));
        if (saUklanjanjem)
        {
            meni.Items.Add(new Separator());
            meni.Items.Add(Stavka(pesme.Count > 1 ? $"Ukloni iz spiska ({pesme.Count})" : "Ukloni iz spiska", () => UkloniPesme(pesme)));
        }
        return meni;
    }
}
