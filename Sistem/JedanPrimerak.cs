using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Bronxamp.Sistem;

/// <summary>
/// Keeps one player running per data folder. A later start passes its arguments here over a named pipe.
/// The name comes from the data folder, so a test run with its own data does not collide with the real player.
/// </summary>
public sealed class JedanPrimerak : IDisposable
{
    private readonly string _ime;
    private readonly Mutex _brava;
    private readonly CancellationTokenSource _gasenje = new();

    public bool Prvi { get; }

    public event Action<string>? Stiglo;

    public JedanPrimerak()
    {
        var hes = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Putanje.Podaci.ToLowerInvariant())))[..16];
        _ime = $"BronxSoftBronxamp-{hes}";
        _brava = new Mutex(true, _ime, out bool prvi);
        Prvi = prvi;
    }

    public void Slusaj() => _ = Task.Run(Petlja);

    private async Task Petlja()
    {
        while (!_gasenje.IsCancellationRequested)
        {
            try
            {
                await using var cev = new NamedPipeServerStream(_ime, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await cev.WaitForConnectionAsync(_gasenje.Token);
                using var citac = new StreamReader(cev, Encoding.UTF8);
                Stiglo?.Invoke(await citac.ReadToEndAsync(_gasenje.Token));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException ex)
            {
                Dnevnik.Greska("Poruka drugog pokretanja", ex);
            }
        }
    }

    public void Posalji(string poruka)
    {
        try
        {
            using var cev = new NamedPipeClientStream(".", _ime, PipeDirection.Out);
            cev.Connect(2000);
            using var pisac = new StreamWriter(cev, Encoding.UTF8);
            pisac.Write(poruka);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            Dnevnik.Greska("Slanje poruke prvom pokretanju", ex);
        }
    }

    public void Dispose()
    {
        _gasenje.Cancel();
        if (Prvi) _brava.ReleaseMutex();
        _brava.Dispose();
    }
}
