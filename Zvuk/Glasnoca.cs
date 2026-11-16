using Bronxamp.Podaci;
using ManagedBass;

namespace Bronxamp.Zvuk;

/// <summary>
/// Loudness of a song: measured once and kept with it (<see cref="Pesma.JacinaDb"/>). The ReplayGain tag is
/// read first, because the file already carries the answer and nothing has to be decoded; where there is none
/// the file is decoded and its RMS and peak are measured. Nothing is ever written to the file and its length
/// never changes: playback only gets a different gain, so a day planned by the clock stays on the clock.
/// </summary>
public static class Glasnoca
{
    /// <summary>ReplayGain 1.0 is defined against −18 LUFS, so a tag's gain already tells us the loudness.</summary>
    public const double Referenca = -18;

    /// <summary>A peak is never pushed above this, in dBFS. The limiter is the net below it.</summary>
    public const double PlafonVrha = -1;

    /// <summary>Quieter than this is silence, and treating it as a number keeps the gain finite.</summary>
    public const double Najtisa = -60;

    public const double Najglasnija = 0;
    public const double NajvecePojacanje = 20;

    /// <summary>The BASS device to decode on. The player sets it when it starts; BASS keeps it per thread.</summary>
    public static int Uredjaj { get; set; } = -1;

    /// <summary>The loudness and peak a ReplayGain tag carries, or null when the tag has none.</summary>
    public static (double JacinaDb, double? VrhDb)? IzReplayGain(double pojacanjeDb, double vrh)
    {
        if (double.IsNaN(pojacanjeDb)) return null;
        double? vrhDb = double.IsNaN(vrh) || vrh <= 0 ? null : UDb(vrh);
        return (Referenca - pojacanjeDb, vrhDb);
    }

    /// <summary>
    /// Decodes the whole file and measures it: RMS as approximate LUFS (for a sine the two agree) and the
    /// highest sample. Null when the file will not open.
    /// </summary>
    public static (double JacinaDb, double VrhDb)? Izmeri(string putanja, CancellationToken token = default)
    {
        // A pool thread may remember a device that no longer exists.
        Bass.CurrentDevice = Uredjaj;
        int ruckica = Bass.CreateStream(putanja, 0, 0, BassFlags.Decode | BassFlags.Float | BassFlags.Prescan);
        if (ruckica == 0)
        {
            Dnevnik.Upisi($"Merenje glasnoće \"{putanja}\": {Bass.LastError}");
            return null;
        }

        try
        {
            var bafer = new float[16384 * 2];
            double zbir = 0, vrh = 0;
            long sempala = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int procitano = Bass.ChannelGetData(ruckica, bafer, bafer.Length * 4 | (int)DataFlags.Float);
                if (procitano <= 0) break;
                int koliko = procitano / 4;
                for (int i = 0; i < koliko; i++)
                {
                    double v = bafer[i];
                    zbir += v * v;
                    double apsolutno = Math.Abs(v);
                    if (apsolutno > vrh) vrh = apsolutno;
                }
                sempala += koliko;
            }
            if (sempala == 0) return null;
            return (Math.Clamp(UDb(Math.Sqrt(zbir / sempala)), Najtisa, Najglasnija), Math.Clamp(UDb(vrh), Najtisa, Najglasnija));
        }
        finally
        {
            Bass.StreamFree(ruckica);
        }
    }

    /// <summary>
    /// How much a song has to move to sit on the target. Zero when normalization is off, when the song was
    /// never measured, or when it is already within the threshold. A peak that is already close to the ceiling
    /// holds the gain back: the limiter is only a net, not something to lean on.
    /// </summary>
    public static double PojacanjeDb(double? jacinaDb, double? vrhDb, Podesavanja p)
    {
        if (!p.Normalizuj || jacinaDb is not { } jacina) return 0;
        double pojacanje = p.CiljLufs - jacina;
        if (Math.Abs(pojacanje) < p.PragDb) return 0;
        if (vrhDb is { } vrh) pojacanje = Math.Min(pojacanje, PlafonVrha - vrh);
        return Math.Clamp(pojacanje, -NajvecePojacanje, NajvecePojacanje);
    }

    private static double UDb(double v) => v <= 0 ? double.NegativeInfinity : 20 * Math.Log10(v);
}
