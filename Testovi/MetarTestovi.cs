using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Bronxamp.Prikaz;

namespace Bronxamp.Testovi;

public static class MetarTestovi
{
    private static readonly Color Zelena = Color.FromRgb(0x3F, 0xBF, 0x6F);
    private static readonly Color Zuta = Color.FromRgb(0xE8, 0xB3, 0x3A);
    private static readonly Color Crvena = Color.FromRgb(0xE2, 0x50, 0x4E);

    /// <summary>Scale −60…0 dB, red zone −3…0 dB, and the colour fades into red instead of cutting.</summary>
    public static Task Skala()
    {
        Tvrdi(Math.Abs(NivoMetar.Deo(-60)) < 1e-9, "−60 dB mora da bude leva ivica");
        Tvrdi(Math.Abs(NivoMetar.Deo(-30) - 0.5) < 1e-9, "−30 dB mora da bude na pola");
        Tvrdi(Math.Abs(NivoMetar.Deo(0) - 1) < 1e-9, "0 dB mora da bude desna ivica");
        Tvrdi(Math.Abs(NivoMetar.Deo(-3) - 0.95) < 1e-6, "−3 dB mora da bude na 95 % skale");

        var (od, kraj) = NivoMetar.CrvenaZona();
        Tvrdi(Math.Abs(od - 0.95) < 1e-6 && Math.Abs(kraj - 1) < 1e-9, "crvena zona mora da bude od −3 do 0 dB");

        Tvrdi(NivoMetar.BojaNa(0.5, Zelena, Zuta, Crvena) == Zelena, "u sredini skale još je zelena");
        Tvrdi(NivoMetar.BojaNa(od, Zelena, Zuta, Crvena) == Zuta, "na −3 dB je žuta");
        Tvrdi(NivoMetar.BojaNa(1, Zelena, Zuta, Crvena) == Crvena, "na 0 dB je crvena");

        var prePrelaza = NivoMetar.BojaNa(od - 0.04, Zelena, Zuta, Crvena);
        var poslePrelaza = NivoMetar.BojaNa(od + 0.02, Zelena, Zuta, Crvena);
        Tvrdi(prePrelaza.G > Zelena.G * 0.5 && prePrelaza != Zelena, "pre −3 dB boja već blago ide ka žutoj");
        Tvrdi(poslePrelaza.G < Zuta.G && poslePrelaza.B > Zuta.B, "posle −3 dB boja blago prelazi u crveno");
        Tvrdi(poslePrelaza != Crvena, "prelaz u crveno nije nagao (nije odmah crvena)");
        return Task.CompletedTask;
    }

    /// <summary>Draws the meter at a few levels into PNGs, so the look can be checked by eye.</summary>
    public static Task Slika()
    {
        var folder = Path.Combine(Spisak.Radni, "metar");
        Directory.CreateDirectory(folder);

        double[] nivoi = [0.02, 0.35, 0.65, 0.885, 0.955, 0.985, 1.0];   // ≈ −48, −18, −8, −3.5, −1.5, −0.5, 0 dB
        NaSta(() =>
        {
            for (int i = 0; i < nivoi.Length; i++)
            {
                var metar = new NivoMetar { Width = 460, Height = 22 };
                metar.PostaviNivo(nivoi[i], Math.Min(1, nivoi[i] * 0.92));
                metar.Measure(new Size(460, 22));
                metar.Arrange(new Rect(0, 0, 460, 22));
                metar.UpdateLayout();

                var slika = new RenderTargetBitmap(460, 22, 96, 96, PixelFormats.Pbgra32);
                slika.Render(metar);
                var koder = new PngBitmapEncoder();
                koder.Frames.Add(BitmapFrame.Create(slika));
                using var f = File.Create(Path.Combine(folder, $"metar_{i:00}_{nivoi[i]:0.000}.png"));
                koder.Save(f);
            }
        });

        Tvrdi(Directory.GetFiles(folder, "*.png").Length == nivoi.Length, "slike merača nisu napravljene");
        return Task.CompletedTask;
    }

    private static void NaSta(Action posao)
    {
        Exception? greska = null;
        var nit = new Thread(() =>
        {
            try { posao(); }
            catch (Exception e) { greska = e; }
        });
        nit.SetApartmentState(ApartmentState.STA);
        nit.Start();
        nit.Join();
        if (greska != null) throw greska;
    }

    private static void Tvrdi(bool uslov, string opis)
    {
        if (!uslov) throw new InvalidOperationException(opis);
    }
}
