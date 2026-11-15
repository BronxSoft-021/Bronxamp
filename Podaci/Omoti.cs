using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Bronxamp.Podaci;

/// <summary>
/// Album covers. A small JPEG per album is kept on disk; the file itself is opened only the first time.
/// Requests go on a stack, so while scrolling the tiles on screen are served before those already passed.
/// </summary>
public sealed class Omoti
{
    private const int VelicinaNaDisku = 320;
    private const int VelicinaUMemoriji = 240;
    private const int NajviseUMemoriji = 500;
    private const int Radnika = 3;

    private static readonly string[] ImenaSlika = ["cover", "folder", "front", "albumart", "album"];
    private static readonly string[] EkstenzijeSlika = [".jpg", ".jpeg", ".png"];

    private readonly string _folder;
    private readonly ConcurrentStack<Album> _zahtevi = new();
    private readonly Dictionary<long, List<Action>> _cekaju = [];
    private readonly ConcurrentDictionary<long, object> _pravljenje = new();
    private readonly SemaphoreSlim _ima = new(0);
    private readonly Dictionary<long, LinkedListNode<(long Id, ImageSource Slika)>> _kes = [];
    private readonly LinkedList<(long Id, ImageSource Slika)> _redosled = new();

    public Omoti(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(folder);
        for (int i = 0; i < Radnika; i++)
            new Thread(Radnik) { IsBackground = true, Name = "Omoti", Priority = ThreadPriority.BelowNormal }.Start();
    }

    private string Fajl(long albumId) => Path.Combine(_folder, $"{albumId}.jpg");
    private string BezOmota(long albumId) => Path.Combine(_folder, $"{albumId}.nema");

    public ImageSource? IzKesa(long albumId)
    {
        lock (_kes)
        {
            if (!_kes.TryGetValue(albumId, out var cvor)) return null;
            _redosled.Remove(cvor);
            _redosled.AddFirst(cvor);
            return cvor.Value.Slika;
        }
    }

    /// <summary>Asks for the cover in the background. <paramref name="gotovo"/> runs off the UI thread.</summary>
    public void Zatrazi(Album album, Action gotovo)
    {
        // The tile and the "now playing" bar can ask for the same cover, and both must hear back.
        lock (_cekaju)
        {
            if (_cekaju.TryGetValue(album.Id, out var vecCekaju))
            {
                vecCekaju.Add(gotovo);
                return;
            }
            _cekaju[album.Id] = [gotovo];
        }
        _zahtevi.Push(album);
        _ima.Release();
    }

    private void Radnik()
    {
        while (true)
        {
            _ima.Wait();
            if (!_zahtevi.TryPop(out var album)) continue;
            BitmapImage? slika = null;
            try
            {
                slika = Ucitaj(album);
                if (slika != null) Zapamti(album.Id, slika);
            }
            catch (Exception ex)
            {
                Dnevnik.Greska($"Omot \"{album.Naziv}\"", ex);
            }

            List<Action>? cekaju;
            lock (_cekaju)
            {
                _cekaju.Remove(album.Id, out cekaju);
            }
            if (slika != null && cekaju != null)
                foreach (var gotovo in cekaju)
                    gotovo();
        }
    }

    private void Zapamti(long id, ImageSource slika)
    {
        lock (_kes)
        {
            if (_kes.ContainsKey(id)) return;
            _kes[id] = _redosled.AddFirst((id, slika));
            while (_kes.Count > NajviseUMemoriji)
            {
                var poslednji = _redosled.Last!;
                _redosled.RemoveLast();
                _kes.Remove(poslednji.Value.Id);
            }
        }
    }

    private BitmapImage? Ucitaj(Album album)
    {
        var fajl = Fajl(album.Id);
        if (!File.Exists(fajl))
        {
            if (File.Exists(BezOmota(album.Id)) || !Napravi(album)) return null;
        }

        var slika = new BitmapImage();
        slika.BeginInit();
        slika.CacheOption = BitmapCacheOption.OnLoad;
        slika.DecodePixelWidth = VelicinaUMemoriji;
        slika.UriSource = new Uri(fajl);
        slika.EndInit();
        slika.Freeze();
        return slika;
    }

    /// <summary>Path of the cover on disk, creating it if needed. For Windows, which wants a file.</summary>
    public string? PutanjaZa(Album album)
    {
        var fajl = Fajl(album.Id);
        if (File.Exists(fajl)) return fajl;
        return !File.Exists(BezOmota(album.Id)) && Napravi(album) ? fajl : null;
    }

    /// <summary>The tiles and the Windows controls can both need the same cover at the same moment.</summary>
    private bool Napravi(Album album)
    {
        lock (_pravljenje.GetOrAdd(album.Id, _ => new object()))
        {
            if (File.Exists(Fajl(album.Id))) return true;
            if (File.Exists(BezOmota(album.Id))) return false;

            var bajtovi = IzFajla(album) ?? IzFoldera(album);
            if (bajtovi == null || !Sacuvaj(bajtovi, Fajl(album.Id)))
            {
                File.WriteAllBytes(BezOmota(album.Id), []);
                return false;
            }
            return true;
        }
    }

    private static byte[]? IzFajla(Album album)
    {
        // A few songs, not only the first: some albums have art on just part of the tracks.
        foreach (var pesma in album.Pesme.Take(3))
        {
            try
            {
                using var f = TagLib.File.Create(pesma.Putanja);
                var slike = f.Tag.Pictures;
                if (slike.Length == 0) continue;
                var prednja = slike.FirstOrDefault(p => p.Type == TagLib.PictureType.FrontCover) ?? slike[0];
                return prednja.Data.Data;
            }
            catch (Exception ex) when (ex is TagLib.CorruptFileException or TagLib.UnsupportedFormatException or IOException
                                           or UnauthorizedAccessException or ArgumentException or NullReferenceException)
            {
            }
        }
        return null;
    }

    private static byte[]? IzFoldera(Album album)
    {
        var folderi = album.Pesme.Select(p => Path.GetDirectoryName(p.Putanja)!).Distinct().Prepend(album.Folder).Distinct();
        foreach (var folder in folderi)
            foreach (var ime in ImenaSlika)
                foreach (var ekstenzija in EkstenzijeSlika)
                {
                    var putanja = Path.Combine(folder, ime + ekstenzija);
                    try
                    {
                        if (File.Exists(putanja)) return File.ReadAllBytes(putanja);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                    }
                }
        return null;
    }

    private static bool Sacuvaj(byte[] bajtovi, string odrediste)
    {
        try
        {
            using var ulaz = new MemoryStream(bajtovi);
            var slika = new BitmapImage();
            slika.BeginInit();
            slika.CacheOption = BitmapCacheOption.OnLoad;
            slika.DecodePixelWidth = VelicinaNaDisku;
            slika.StreamSource = ulaz;
            slika.EndInit();
            slika.Freeze();

            var koder = new JpegBitmapEncoder { QualityLevel = 88 };
            koder.Frames.Add(BitmapFrame.Create(slika));
            using var izlaz = File.Create(odrediste);
            koder.Save(izlaz);
            return true;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException
                                       or InvalidOperationException or IOException or OverflowException)
        {
            return false;
        }
    }

    /// <summary>The cover's strongest color, as a base for the "now playing" bar. Null without a cover.</summary>
    public static Color? DominantnaBoja(ImageSource? izvor)
    {
        if (izvor is not BitmapSource bitmapa) return null;

        var mala = new TransformedBitmap(bitmapa, new ScaleTransform(32.0 / bitmapa.PixelWidth, 32.0 / bitmapa.PixelHeight));
        var bgra = new FormatConvertedBitmap(mala, PixelFormats.Bgra32, null, 0);
        int sirina = bgra.PixelWidth, visina = bgra.PixelHeight;
        var pikseli = new byte[sirina * visina * 4];
        bgra.CopyPixels(pikseli, sirina * 4, 0);

        // Averaging every pixel gives gray-brown mud, so saturated pixels count far more.
        double r = 0, g = 0, b = 0, tezina = 0;
        for (int i = 0; i < pikseli.Length; i += 4)
        {
            double pb = pikseli[i], pg = pikseli[i + 1], pr = pikseli[i + 2];
            double max = Math.Max(pr, Math.Max(pg, pb)), min = Math.Min(pr, Math.Min(pg, pb));
            if (max < 24) continue;
            double zasicenost = (max - min) / max;
            double t = 0.02 + zasicenost * zasicenost * (max / 255);
            r += pr * t; g += pg * t; b += pb * t; tezina += t;
        }
        if (tezina <= 0) return null;
        return Color.FromRgb((byte)(r / tezina), (byte)(g / tezina), (byte)(b / tezina));
    }
}
