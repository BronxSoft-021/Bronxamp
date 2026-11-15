using System.ComponentModel;
using System.Windows.Threading;
using Bronxamp.Podaci;
using Bronxamp.Vm;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Bronxamp.Sistem;

/// <summary>
/// System media controls: lock screen, the volume flyout and the media keys when another window has focus.
/// A desktop program gets them through a MediaPlayer that never plays anything. The sound stays with BASS.
/// </summary>
public sealed class WindowsMediji : IDisposable
{
    private readonly PlejerVm _plejer;
    private readonly Omoti _omoti;
    private readonly Dispatcher _ui;
    private readonly MediaPlayer? _nosac;
    private readonly SystemMediaTransportControls? _kontrole;
    private long _poslednjaPesma = -1;
    private bool _imaOmot;

    public WindowsMediji(PlejerVm plejer, Omoti omoti, Dispatcher ui)
    {
        _plejer = plejer;
        _omoti = omoti;
        _ui = ui;

        try
        {
            _nosac = new MediaPlayer();
            _nosac.CommandManager.IsEnabled = false;
            _kontrole = _nosac.SystemMediaTransportControls;
            _kontrole.IsEnabled = true;
            _kontrole.IsPlayEnabled = true;
            _kontrole.IsPauseEnabled = true;
            _kontrole.IsNextEnabled = true;
            _kontrole.IsPreviousEnabled = true;
            _kontrole.IsStopEnabled = true;
            _kontrole.ButtonPressed += NaDugme;

            plejer.Promenjeno += Osvezi;
            Osvezi();
        }
        catch (Exception ex)
        {
            // Without these the player still works, so this is not worth stopping for.
            Dnevnik.Greska("Kontrole Windowsa", ex);
            _kontrole = null;
        }
    }

    private void NaDugme(SystemMediaTransportControls izvor, SystemMediaTransportControlsButtonPressedEventArgs e)
    {
        _ui.BeginInvoke(() =>
        {
            switch (e.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    if (!_plejer.Svira) _plejer.PustiPauza();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                case SystemMediaTransportControlsButton.Stop:
                    if (_plejer.Svira) _plejer.PustiPauza();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    _plejer.Sledeca();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    _plejer.Prethodna();
                    break;
            }
        });
    }

    private void Osvezi()
    {
        if (_kontrole == null) return;

        _kontrole.PlaybackStatus = _plejer.Tekuca == null
            ? MediaPlaybackStatus.Closed
            : _plejer.Svira ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;

        if (_plejer.Tekuca is not { } pesma) return;

        // The event fires again once the cover arrives, so a song is sent twice at most.
        bool stigaoOmot = !_imaOmot && _plejer.Omot != null;
        if (pesma.Id == _poslednjaPesma && !stigaoOmot) return;
        if (pesma.Id != _poslednjaPesma) _imaOmot = false;
        _poslednjaPesma = pesma.Id;
        _ = PosaljiPodatke(pesma);
    }

    private async Task PosaljiPodatke(Pesma pesma)
    {
        try
        {
            var ekran = _kontrole!.DisplayUpdater;
            ekran.Type = MediaPlaybackType.Music;
            ekran.MusicProperties.Title = pesma.Naslov;
            ekran.MusicProperties.Artist = pesma.Izvodjac;
            ekran.MusicProperties.AlbumTitle = pesma.Album.Naziv;
            ekran.Thumbnail = null;

            // A cover that fails must not cost Windows the title of the song.
            try
            {
                var putanja = await Task.Run(() => _omoti.PutanjaZa(pesma.Album));
                if (putanja != null)
                {
                    var fajl = await StorageFile.GetFileFromPathAsync(putanja);
                    ekran.Thumbnail = RandomAccessStreamReference.CreateFromFile(fajl);
                    _imaOmot = true;
                }
            }
            catch (Exception ex)
            {
                Dnevnik.Greska("Omot za Windows", ex);
            }

            if (_poslednjaPesma == pesma.Id) ekran.Update();
        }
        catch (Exception ex)
        {
            Dnevnik.Greska("Podaci za Windows", ex);
        }
    }

    public void Dispose()
    {
        _plejer.Promenjeno -= Osvezi;
        if (_kontrole != null) _kontrole.ButtonPressed -= NaDugme;
        _nosac?.Dispose();
    }
}
