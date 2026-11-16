using System.Collections.Concurrent;

namespace Bronxamp.Podaci;

public sealed record NapredakSkeniranja(int Obradjeno, int Ukupno, bool Gotovo);

/// <summary>How far the loudness measurement has come: <see cref="Ukupno"/> is 0 when nothing needed measuring.</summary>
public sealed record NapredakMerenja(int Obradjeno, int Ukupno, bool Gotovo = false);

/// <summary>
/// Scans the library folders in the background and keeps watching them. Only new or changed files are read.
/// A root folder that is not reachable (unplugged disk) is skipped: its songs must not be marked as gone.
/// </summary>
public sealed class Skener : IDisposable
{
    private const int Paket = 200;
    private static readonly TimeSpan OdlaganjePosleIzmene = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan NajcesceOsvezavanje = TimeSpan.FromSeconds(3);

    /// <summary>How many files one automatic measurement pass takes on, so a first scan of a big library does
    /// not keep the disk busy for hours. The button in Settings measures everything.</summary>
    private const int NajviseAutomatski = 400;

    private readonly Baza _baza;
    private readonly Katalog _katalog;
    private readonly Func<bool>? _meri;
    private readonly List<FileSystemWatcher> _posmatraci = [];
    private readonly SemaphoreSlim _jedanPoJedan = new(1, 1);
    private readonly System.Threading.Timer _odlaganje;
    private readonly CancellationTokenSource _gasenje = new();

    public event Action<NapredakSkeniranja>? Napredak;
    public event Action<NapredakMerenja>? NapredakMerenja;

    /// <param name="meri">Asked after every scan: should new songs be measured for loudness now?</param>
    public Skener(Baza baza, Katalog katalog, Func<bool>? meri = null)
    {
        _baza = baza;
        _katalog = katalog;
        _meri = meri;
        _odlaganje = new System.Threading.Timer(_ => _ = SkenirajAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void PratiFoldere()
    {
        lock (_posmatraci)
        {
            foreach (var p in _posmatraci) p.Dispose();
            _posmatraci.Clear();

            foreach (var folder in _baza.Folderi())
            {
                if (!Directory.Exists(folder)) continue;
                try
                {
                    var p = new FileSystemWatcher(folder)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        InternalBufferSize = 64 * 1024,
                    };
                    p.Created += NaIzmenu;
                    p.Deleted += NaIzmenu;
                    p.Changed += NaIzmenu;
                    p.Renamed += NaIzmenu;
                    p.Error += (_, e) => { Dnevnik.Greska("Praćenje foldera", e.GetException()); ZakaziSkeniranje(); };
                    p.EnableRaisingEvents = true;
                    _posmatraci.Add(p);
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
                {
                    Dnevnik.Greska($"Praćenje foldera \"{folder}\"", ex);
                }
            }
        }
    }

    private void NaIzmenu(object sender, FileSystemEventArgs e)
    {
        // Directory events have no extension, and a moved folder of songs must trigger a scan too.
        if (Tagovi.JeMuzika(e.FullPath) || !Path.HasExtension(e.FullPath))
            ZakaziSkeniranje();
    }

    /// <summary>Restarts the delay on every event, so copying a whole album causes one scan, not hundreds.</summary>
    private void ZakaziSkeniranje() => _odlaganje.Change(OdlaganjePosleIzmene, Timeout.InfiniteTimeSpan);

    /// <summary>
    /// Adds songs picked one by one. They need no library folder: a scan only marks as gone what lies under
    /// a library folder, so these stay until the user removes them.
    /// </summary>
    public async Task DodajFajloveAsync(IReadOnlyList<string> putanje)
    {
        var token = _gasenje.Token;
        await _jedanPoJedan.WaitAsync(token);
        try
        {
            await Task.Run(() =>
            {
                var poznati = _baza.PoznatiFajlovi();
                var pesme = putanje
                    .Where(p => File.Exists(p) && Tagovi.JeMuzika(p))
                    .Select(p => Tagovi.Procitaj(new FileInfo(p), !poznati.ContainsKey(p)))
                    .ToList();
                _baza.Upisi(pesme);
                _baza.UkljuciPutanje(putanje);
                _katalog.Ucitaj();
            }, token);
        }
        finally
        {
            _jedanPoJedan.Release();
        }

        if (_meri?.Invoke() == true) _ = IzmeriAsync();
    }

    /// <summary>
    /// Measures loudness for the songs that have none, or for the whole library when the people asked for it
    /// from Settings. Two at a time keeps the disk and the player happy; the catalogue is reloaded at the end,
    /// so playback sees the new values.
    /// </summary>
    public async Task<int> IzmeriAsync(bool sve = false, int najvise = int.MaxValue, CancellationToken spolja = default)
    {
        using var spoj = CancellationTokenSource.CreateLinkedTokenSource(_gasenje.Token, spolja);
        var token = spoj.Token;

        List<(long Id, string Putanja)> pesme;
        try
        {
            pesme = _baza.PesmeZaMerenje(sve, sve ? int.MaxValue : Math.Min(najvise, NajviseAutomatski));
        }
        catch (Exception ex)
        {
            Dnevnik.Greska("Spisak za merenje", ex);
            return 0;
        }

        NapredakMerenja?.Invoke(new NapredakMerenja(0, pesme.Count));
        if (pesme.Count == 0)
        {
            NapredakMerenja?.Invoke(new NapredakMerenja(0, 0, true));
            return 0;
        }

        int obradjeno = 0, izmereno = 0;
        try
        {
            await Parallel.ForEachAsync(pesme, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = token },
                (pesma, ct) =>
                {
                    var sada = Zvuk.Glasnoca.Izmeri(pesma.Putanja, ct);
                    if (sada is { } m)
                    {
                        _baza.UpisiGlasnocu(pesma.Id, m.JacinaDb, m.VrhDb);
                        Interlocked.Increment(ref izmereno);
                    }

                    int koliko = Interlocked.Increment(ref obradjeno);
                    if (koliko % 10 == 0 || koliko == pesme.Count)
                        NapredakMerenja?.Invoke(new NapredakMerenja(koliko, pesme.Count));
                    return ValueTask.CompletedTask;
                });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Dnevnik.Greska("Merenje glasnoće", ex);
        }

        _katalog.Ucitaj();
        NapredakMerenja?.Invoke(new NapredakMerenja(obradjeno, pesme.Count, true));
        return izmereno;
    }

    public async Task SkenirajAsync()
    {
        var token = _gasenje.Token;
        try
        {
            await _jedanPoJedan.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        bool biloNovih = false;
        try
        {
            biloNovih = await Task.Run(() => Skeniraj(token), token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Dnevnik.Greska("Skeniranje", ex);
        }
        finally
        {
            _jedanPoJedan.Release();
        }

        // The scan is reported as finished first: measuring decodes every new file and must not hold it up.
        if (biloNovih && _meri?.Invoke() == true) _ = IzmeriAsync();
    }

    private bool Skeniraj(CancellationToken token)
    {
        var poznati = _baza.PoznatiFajlovi();
        var zaCitanje = new List<(FileInfo Fajl, bool Nova)>();
        var pronadjeni = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var vracene = new List<string>();
        var dostupniKoreni = new List<string>();

        foreach (var koren in _baza.Folderi())
        {
            if (!Directory.Exists(koren)) continue;
            dostupniKoreni.Add(koren.TrimEnd('\\') + "\\");

            var opcije = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System };
            foreach (var fajl in new DirectoryInfo(koren).EnumerateFiles("*", opcije))
            {
                token.ThrowIfCancellationRequested();
                if (!Tagovi.Ekstenzije.Contains(fajl.Extension)) continue;
                if (!pronadjeni.Add(fajl.FullName)) continue;

                if (!poznati.TryGetValue(fajl.FullName, out var stari))
                    zaCitanje.Add((fajl, true));
                else if (stari.Velicina != fajl.Length || stari.Izmenjeno != fajl.LastWriteTimeUtc.Ticks)
                    zaCitanje.Add((fajl, false));
                else if (stari.Nestala)
                    vracene.Add(fajl.FullName);
            }
        }

        var nestale = poznati
            .Where(p => !p.Value.Nestala && !pronadjeni.Contains(p.Key) &&
                        dostupniKoreni.Exists(k => p.Key.StartsWith(k, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Key)
            .ToList();

        // Marked before new files are written, so a moved file finds its old row.
        if (nestale.Count > 0) _baza.OznaciNestale(nestale);
        if (vracene.Count > 0) _baza.OznaciPrisutne(vracene);

        bool izmenjeno = nestale.Count > 0 || vracene.Count > 0 || zaCitanje.Count > 0;
        if (zaCitanje.Count == 0)
        {
            if (izmenjeno) _katalog.Ucitaj();
            Napredak?.Invoke(new NapredakSkeniranja(0, 0, true));
            return false;
        }

        // Sorted by path, so albums arrive whole and tiles look complete during a long first scan.
        zaCitanje.Sort((a, b) => string.Compare(a.Fajl.FullName, b.Fajl.FullName, StringComparison.OrdinalIgnoreCase));

        int obradjeno = 0;
        var sledeceOsvezavanje = DateTime.UtcNow + NajcesceOsvezavanje;
        for (int od = 0; od < zaCitanje.Count; od += Paket)
        {
            token.ThrowIfCancellationRequested();
            var deo = zaCitanje.Skip(od).Take(Paket).ToList();
            var rezultat = new ConcurrentBag<SkeniranaPesma>();

            Parallel.ForEach(deo, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token }, stavka =>
            {
                try
                {
                    stavka.Fajl.Refresh();
                    if (stavka.Fajl.Exists) rezultat.Add(Tagovi.Procitaj(stavka.Fajl, stavka.Nova));
                }
                catch (Exception ex)
                {
                    Dnevnik.Greska($"Čitanje \"{stavka.Fajl.FullName}\"", ex);
                }
            });

            _baza.Upisi(rezultat.OrderBy(p => p.Putanja, StringComparer.OrdinalIgnoreCase).ToList());
            obradjeno += deo.Count;
            Napredak?.Invoke(new NapredakSkeniranja(obradjeno, zaCitanje.Count, false));

            if (DateTime.UtcNow >= sledeceOsvezavanje)
            {
                // A reload of a big library is not free, so it never takes more than a tenth of the time.
                var pocetak = DateTime.UtcNow;
                _katalog.Ucitaj();
                var trajalo = DateTime.UtcNow - pocetak;
                sledeceOsvezavanje = DateTime.UtcNow + (trajalo * 10 > NajcesceOsvezavanje ? trajalo * 10 : NajcesceOsvezavanje);
            }
        }

        _katalog.Ucitaj();
        Napredak?.Invoke(new NapredakSkeniranja(obradjeno, zaCitanje.Count, true));
        return true;
    }

    public void Dispose()
    {
        _gasenje.Cancel();
        _odlaganje.Dispose();
        lock (_posmatraci)
        {
            foreach (var p in _posmatraci) p.Dispose();
            _posmatraci.Clear();
        }
    }
}
