using ManagedBass;
using ManagedBass.DirectX8;

namespace Bronxamp.Zvuk;

/// <summary>
/// A near-brickwall limiter on the mixer: peaks above −1,5 dB are pressed down hard, so a loud song plus a
/// jingle cannot clip the stream. It changes loudness only, never the length of a song.
/// </summary>
public static class Limiter
{
    /// <summary>Puts the limiter on the mixer; returns the effect handle (0 when it could not be added).</summary>
    public static int Postavi(int mikser)
    {
        if (mikser == 0) return 0;
        int fx = Bass.ChannelSetFX(mikser, EffectType.DXCompressor, 1);
        if (fx == 0) return 0;

        var p = new DXCompressorParameters
        {
            fGain = 0f,          // no make-up gain: we only stop the peaks
            fThreshold = -1.5f,  // dB, just below full scale
            fRatio = 20f,        // close enough to a brickwall
            fAttack = 1f,        // ms
            fRelease = 60f,      // ms
            fPredelay = 4f,      // ms, so the first peak is caught
        };
        Bass.FXSetParameters(fx, p);
        return fx;
    }

    public static void Ukloni(int mikser, int fx)
    {
        if (mikser != 0 && fx != 0) Bass.ChannelRemoveFX(mikser, fx);
    }
}
