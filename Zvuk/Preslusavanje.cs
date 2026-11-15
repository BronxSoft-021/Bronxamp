using ManagedBass;

namespace Bronxamp.Zvuk;

/// <summary>Plays one track on a second output device, independently of the main player (headphones cue).</summary>
public sealed class Preslusavanje : IDisposable
{
    private int _kanal;

    public bool Svira => _kanal != 0 && Bass.ChannelIsActive(_kanal) == PlaybackState.Playing;

    public double Pozicija => _kanal == 0 ? 0 : Bass.ChannelBytes2Seconds(_kanal, Bass.ChannelGetPosition(_kanal));
    public double Trajanje => _kanal == 0 ? 0 : Bass.ChannelBytes2Seconds(_kanal, Bass.ChannelGetLength(_kanal));

    public bool Pusti(string putanja, int uredjaj)
    {
        Zaustavi();
        int prethodni = Bass.CurrentDevice;
        if (!Bass.Init(uredjaj, 44100, DeviceInitFlags.Default, IntPtr.Zero, IntPtr.Zero) && Bass.LastError != Errors.Already)
        {
            Dnevnik.Upisi($"Preslušavanje, uređaj {uredjaj}: {Bass.LastError}");
            return false;
        }

        Bass.CurrentDevice = uredjaj;
        _kanal = Bass.CreateStream(putanja, 0, 0, BassFlags.Float | BassFlags.AutoFree);
        Bass.CurrentDevice = prethodni;

        if (_kanal == 0)
        {
            Dnevnik.Upisi($"Preslušavanje \"{putanja}\": {Bass.LastError}");
            return false;
        }
        return Bass.ChannelPlay(_kanal);
    }

    public void Premotaj(double sekunde)
    {
        if (_kanal != 0) Bass.ChannelSetPosition(_kanal, Bass.ChannelSeconds2Bytes(_kanal, sekunde));
    }

    public void Zaustavi()
    {
        if (_kanal != 0) Bass.StreamFree(_kanal);
        _kanal = 0;
    }

    public void Dispose() => Zaustavi();
}
