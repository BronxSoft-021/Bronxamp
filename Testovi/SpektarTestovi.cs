using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Bronxamp.Prikaz;
using Bronxamp.Zvuk;
using ManagedBass;

namespace Bronxamp.Testovi;

public static class SpektarTestovi
{
    /// <summary>Draws the wide on-air spectrum to PNGs so the colours can be checked by eye.</summary>
    public static Task Slika()
    {
        var folder = Path.Combine(Spisak.Radni, "spektar");
        Directory.CreateDirectory(folder);

        NaSta(() =>
        {
            var stubici = new List<double>();
            for (int i = 0; i < 32; i++)
                stubici.Add(0.18 + 0.82 * Math.Pow(Math.Abs(Math.Sin(i / 3.0)), 1.4) * (0.65 + 0.35 * Math.Abs(Math.Cos(i / 1.7))));

            var spektar = new Spektar
            {
                Width = 900,
                Height = 46,
                BrojStubica = 32,
                Boja = new SolidColorBrush(Color.FromRgb(0x3F, 0xAF, 0x6D)),
                BojaVrha = new SolidColorBrush(Color.FromRgb(0xE8, 0xB3, 0x3A)),
            };
            spektar.PostaviStubice(stubici);
            spektar.Measure(new Size(900, 46));
            spektar.Arrange(new Rect(0, 0, 900, 46));
            spektar.UpdateLayout();

            var slika = new RenderTargetBitmap(900, 46, 96, 96, PixelFormats.Pbgra32);
            slika.Render(spektar);
            var koder = new PngBitmapEncoder();
            koder.Frames.Add(BitmapFrame.Create(slika));
            using var f = File.Create(Path.Combine(folder, "spektar_900x46.png"));
            koder.Save(f);
        });

        if (!File.Exists(Path.Combine(folder, "spektar_900x46.png")))
            throw new InvalidOperationException("slika spektra nije napravljena");
        return Task.CompletedTask;
    }

    /// <summary>A pure tone lights the bar that holds its frequency, from the bass to the top.</summary>
    public static Task TonUPravomStubicu()
    {
        using var p = new ZvukPlejer(dekodiranje: true);
        Spisak.Tvrdi(p.Pokreni(0), "BASS nije pokrenut");
        foreach (double hz in new[] { 40.0, 100, 1000, 8000, 16000 })
        {
            var stubici = Prosek(ZvukTestovi.NapisiWav(Spisak.Radni, $"ton{hz}.wav", hz, 0, 44100), 31);
            int najvisi = Array.IndexOf(stubici, stubici.Max());
            double od = Spektar.GranicaHz(najvisi, 31), @do = Spektar.GranicaHz(najvisi + 1, 31);
            Spisak.Tvrdi(hz >= od * 0.97 && hz <= @do * 1.03, $"ton {hz} Hz je u stubiću {najvisi} ({od:0}-{@do:0} Hz)");
        }
        return Task.CompletedTask;
    }

    /// <summary>Average bar heights over a whole file: Testovi --spektar <fajl> [stubića].</summary>
    public static int Izmeri(string putanja, int n)
    {
        using var p = new ZvukPlejer(dekodiranje: true);
        if (!p.Pokreni(0)) return 1;
        var stubici = Prosek(putanja, n);
        for (int i = 0; i < n; i++)
            Console.WriteLine($"{Spektar.GranicaHz(i, n),7:0}-{Spektar.GranicaHz(i + 1, n),5:0} Hz  {stubici[i]:0.00}  {new string('#', (int)(stubici[i] * 40))}");
        return 0;
    }

    private static float[] Prosek(string putanja, int n)
    {
        int h = Bass.CreateStream(putanja, 0, 0, BassFlags.Decode | BassFlags.Float);
        Spisak.Tvrdi(h != 0, $"ne otvara se: {Bass.LastError}");
        int frekvencija = Bass.ChannelGetInfo(h).Frequency;
        var fft = new float[Spektar.VelicinaFft];
        var jedan = new float[n];
        var zbir = new float[n];
        int koliko = 0;
        long korak = Bass.ChannelSeconds2Bytes(h, 0.1);
        var smece = new byte[korak];
        while (Bass.ChannelGetData(h, fft, (int)DataFlags.FFT4096) > 0)
        {
            Spektar.Pojasevi(fft, frekvencija, jedan);
            for (int i = 0; i < n; i++) zbir[i] += jedan[i];
            koliko++;
            if (Bass.ChannelGetData(h, smece, smece.Length) <= 0) break;
        }
        Bass.StreamFree(h);
        return zbir.Select(z => z / Math.Max(1, koliko)).ToArray();
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
}
