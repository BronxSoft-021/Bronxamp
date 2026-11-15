using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Bronxamp.Zvuk;
using static Bronxamp.Testovi.Spisak;

namespace Bronxamp.Testovi;

/// <summary>Uživo provere emitovanja: ton kroz StreamEmisija i slušalac sa druge strane.</summary>
public static class StreamTest
{
    /// <summary>„Testovi --stream &lt;wav&gt;“: pošalje ton na Icecast i pročita ga nazad kao slušalac.</summary>
    public static async Task<int> Pokreni(string wav, string host = "127.0.0.1", int port = 8000,
        string mount = "/test.mp3", string korisnik = "source", string lozinka = "bronxamp-test")
    {
        using var zvuk = new ZvukPlejer();
        if (!zvuk.Pokreni(0))   // 0 = BASS "no sound" uređaj: ton se ne čuje, ali se enkoduje
        {
            Console.WriteLine("BASS Init(0) nije uspeo.");
            return 1;
        }

        using var stream = new StreamEmisija(zvuk);
        bool ok = stream.Povezi(host, port, mount, korisnik, lozinka, 128, "Bronxamp test");
        Console.WriteLine($"Povezi: {ok} | stanje={stream.Stanje} | adresa={stream.Adresa} | poruka={stream.Poruka}");
        if (!ok) return 2;

        stream.NaslovPesme("Test", "Ton 440 Hz");
        bool pustio = await zvuk.PustiAsync(new ZvukStavka(wav));
        Console.WriteLine($"Pustio ton: {pustio} | plejer={zvuk.Stanje}");

        await Task.Delay(4000);
        Console.WriteLine($"Posle 4s: stream={stream.Stanje} | plejer={zvuk.Stanje}");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        try
        {
            using var resp = await http.GetAsync($"http://{host}:{port}{mount}", HttpCompletionOption.ResponseHeadersRead);
            Console.WriteLine($"Slušalac HTTP {(int)resp.StatusCode} | content-type={resp.Content.Headers.ContentType}");

            await using var s = await resp.Content.ReadAsStreamAsync();
            var buf = new byte[16384];
            int total = 0;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            try
            {
                int n;
                while (total < buf.Length &&
                       (n = await s.ReadAsync(buf.AsMemory(total, buf.Length - total), cts.Token)) > 0)
                    total += n;
            }
            catch (OperationCanceledException) { }

            Console.WriteLine($"Pročitano sa streama: {total} bajtova");
            if (total >= 4) Console.WriteLine($"Prva 4 bajta: {BitConverter.ToString(buf, 0, 4)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Slušalac greška: " + ex.Message);
        }

        return stream.Stanje == StreamStanje.NaEtru ? 0 : 3;
    }


    /// <summary>Veza pukne, pa se program sam vrati na server (ide u Spisak, ne treba pravi Icecast).</summary>
    public static async Task VracanjeVeze()
    {
        var wav = ZvukTestovi.Wav(NoviFolder());
        using var zvuk = new ZvukPlejer();
        Tvrdi(zvuk.Pokreni(0), "BASS nije pokrenut");
        // Music first: the stream is not opened while nothing plays (see CekaMuziku).
        Tvrdi(await zvuk.PustiAsync(new ZvukStavka(wav)), "ton nije pušten");

        using var server = new LazniIcecast();
        using var stream = new StreamEmisija(zvuk);
        Tvrdi(stream.Povezi("127.0.0.1", server.Port, "/test.mp3", "source", "lozinka", 128, "Bronxamp test"),
            $"nije se povezao: {stream.Poruka}");
        Tvrdi(stream.Stanje == StreamStanje.NaEtru, $"stanje posle povezivanja: {stream.Stanje}");
        Tvrdi(stream.Adresa == $"127.0.0.1:{server.Port}/test.mp3", $"adresa: {stream.Adresa}");

        await Task.Delay(3000);   // nekoliko prolaza nadzora sa živom vezom
        Tvrdi(stream.Stanje == StreamStanje.NaEtru, $"veza je živa, a stanje je {stream.Stanje}");

        server.Spusti();   // kao da je server pao
        var cekanje = Stopwatch.StartNew();
        while (stream.Stanje == StreamStanje.NaEtru && cekanje.Elapsed < TimeSpan.FromSeconds(25))
            await Task.Delay(200);
        Tvrdi(stream.Stanje != StreamStanje.NaEtru, "prekid veze nije primećen");
        Tvrdi(stream.Zeljeno, "program je odustao umesto da se vraća");

        cekanje.Restart();
        while (stream.Stanje != StreamStanje.NaEtru && cekanje.Elapsed < TimeSpan.FromSeconds(60))
            await Task.Delay(250);
        Tvrdi(stream.Stanje == StreamStanje.NaEtru, $"program se nije vratio: {stream.Stanje} {stream.Poruka}");
    }

    /// <summary>„Testovi --dugo &lt;fajl&gt; [sekundi]“: veza na probni Icecast sa 320 kbps, praćena N sekundi.</summary>
    public static async Task<int> Dugo(string fajl, int sekundi = 40)
    {
        using var zvuk = new ZvukPlejer();
        if (!zvuk.Pokreni(0))
        {
            Console.WriteLine("BASS Init(0) nije uspeo.");
            return 1;
        }

        using var stream = new StreamEmisija(zvuk);
        if (!await zvuk.PustiAsync(new ZvukStavka(fajl)))
        {
            Console.WriteLine("Nije pustio fajl.");
            return 2;
        }

        await Task.Delay(700);
        bool ok = stream.Povezi("127.0.0.1", 8000, "/test.mp3", "source", "bronxamp-test", 320, "Bronxamp dugo");
        Console.WriteLine($"Povezi: {ok} | {stream.Stanje} | {stream.Poruka} | plejer={zvuk.Stanje} | mikser={zvuk.Frekvencija} Hz");
        Console.WriteLine($"lame: {StreamEmisija.LameArgumenti("lame.exe", zvuk.Frekvencija, 320)}");

        for (int i = 1; i <= sekundi; i++)
        {
            await Task.Delay(1000);
            if (stream.Stanje != StreamStanje.NaEtru)
                Console.WriteLine($"  {i,2} s: {stream.Stanje} — {stream.Poruka}");
        }

        Console.WriteLine($"Kraj posle {sekundi} s: {stream.Stanje} | {stream.Poruka}");
        return stream.Stanje == StreamStanje.NaEtru ? 0 : 3;
    }

    /// <summary>The encoder must be told the mixer's own rate; a fixed 44100 made the stream slow and deep.</summary>
    public static Task FrekvencijaLame()
    {
        string argumenti = StreamEmisija.LameArgumenti(@"C:\Bronxamp\lame.exe", 48000, 320);
        Tvrdi(argumenti.Contains("-s 48000"), $"nema frekvencije miksera: {argumenti}");
        Tvrdi(argumenti.Contains("-b 320"), $"nema bitrate-a: {argumenti}");
        Tvrdi(argumenti.StartsWith('"') && argumenti.Contains("lame.exe\""), $"putanja nije pod navodnicima: {argumenti}");

        string drugi = StreamEmisija.LameArgumenti(@"C:\Bronxamp\lame.exe", 44100, 128);
        Tvrdi(drugi.Contains("-s 44100") && drugi.Contains("-b 128"), $"pogrešni argumenti: {drugi}");
        return Task.CompletedTask;
    }

    /// <summary>Music stops (queue ran out or pause): the cast closes with the mixer, and that is not a failure.</summary>
    public static async Task StaneMuzika()
    {
        var wav = ZvukTestovi.Wav(NoviFolder());
        using var zvuk = new ZvukPlejer();
        Tvrdi(zvuk.Pokreni(0), "BASS nije pokrenut");
        Tvrdi(await zvuk.PustiAsync(new ZvukStavka(wav)), "ton nije pušten");

        using var server = new LazniIcecast();
        using var stream = new StreamEmisija(zvuk);
        Tvrdi(stream.Povezi("127.0.0.1", server.Port, "/test.mp3", "source", "lozinka", 128, "Bronxamp test"),
            $"nije se povezao: {stream.Poruka}");
        Tvrdi(stream.Stanje == StreamStanje.NaEtru, $"stanje posle povezivanja: {stream.Stanje}");

        zvuk.Zaustavi();

        var cekanje = Stopwatch.StartNew();
        while (stream.Stanje == StreamStanje.NaEtru && cekanje.Elapsed < TimeSpan.FromSeconds(12))
            await Task.Delay(200);

        Tvrdi(stream.Stanje != StreamStanje.Greska, $"kad muzika stane ne sme da bude greška: {stream.Stanje} {stream.Poruka}");
        Tvrdi(stream.CekaZvuk, "posle stajanja muzike program treba da čeka muziku");
        Tvrdi(stream.Zeljeno, "program je odustao umesto da čeka");
    }

    /// <summary>Nothing plays: the stream is not opened (a silent source gets dropped), and it opens once music starts.</summary>
    public static async Task CekaMuziku()
    {
        var wav = ZvukTestovi.Wav(NoviFolder());
        using var zvuk = new ZvukPlejer();
        Tvrdi(zvuk.Pokreni(0), "BASS nije pokrenut");

        using var server = new LazniIcecast();
        using var stream = new StreamEmisija(zvuk);

        Tvrdi(stream.Povezi("127.0.0.1", server.Port, "/test.mp3", "source", "lozinka", 128, "Bronxamp test"),
            "zahtev za emitovanje je odbijen");
        Tvrdi(stream.CekaZvuk, "ništa ne svira, a program nije u stanju čekanja");
        Tvrdi(stream.Stanje == StreamStanje.Povezuje, $"stanje dok ništa ne svira: {stream.Stanje}");

        await Task.Delay(2500);   // nekoliko prolaza nadzora (svake 2 s)
        Tvrdi(server.BrojIzvora == 0, $"server je dobio {server.BrojIzvora} veza dok ništa ne svira");
        Tvrdi(stream.CekaZvuk, "program je prestao da čeka iako ništa ne svira");

        Tvrdi(await zvuk.PustiAsync(new ZvukStavka(wav)), "ton nije pušten");
        var cekanje = Stopwatch.StartNew();
        while (stream.Stanje != StreamStanje.NaEtru && cekanje.Elapsed < TimeSpan.FromSeconds(20))
            await Task.Delay(200);
        Tvrdi(stream.Stanje == StreamStanje.NaEtru, $"posle puštanja nije na etru: {stream.Stanje} {stream.Poruka}");
        Tvrdi(!stream.CekaZvuk, "posle puštanja još čeka");
    }

    /// <summary>Midnight (faza 3): the cast goes off by itself and the server's Auto DJ takes over; a song is not cut.</summary>
    public static async Task Ponoc()
    {
        var ukljuceno = new DateTime(2026, 11, 8, 23, 0, 0);
        Tvrdi(!StreamEmisija.ProslaPonoc(default, new DateTime(2026, 11, 8, 23, 59, 59)),
            "kad emitovanje nije uključeno, ne sme da prekida");
        Tvrdi(!StreamEmisija.ProslaPonoc(ukljuceno, new DateTime(2026, 11, 8, 23, 59, 59)),
            "isti dan ne sme da prekida");
        Tvrdi(StreamEmisija.ProslaPonoc(ukljuceno, new DateTime(2026, 11, 9, 0, 0, 1)),
            "posle ponoći treba da prekine");

        var wav = ZvukTestovi.Wav(NoviFolder());
        using var zvuk = new ZvukPlejer();
        Tvrdi(zvuk.Pokreni(0), "BASS nije pokrenut");
        Tvrdi(await zvuk.PustiAsync(new ZvukStavka(wav)), "ton nije pušten");

        using var server = new LazniIcecast();
        using var stream = new StreamEmisija(zvuk);
        Tvrdi(stream.Povezi("127.0.0.1", server.Port, "/test.mp3", "source", "lozinka", 128, "Bronxamp test"),
            $"nije se povezao: {stream.Poruka}");
        Tvrdi(stream.Stanje == StreamStanje.NaEtru, $"stanje posle povezivanja: {stream.Stanje}");

        stream.Sada = () => DateTime.Now.AddDays(1);   // ponoć je prošla, a pesma još svira
        await Task.Delay(3000);                        // nekoliko prolaza nadzora
        Tvrdi(stream.Stanje == StreamStanje.NaEtru, $"pesma koja svira ne sme da se prekine: {stream.Stanje}");
        Tvrdi(stream.Zeljeno, "program je odustao dok pesma svira");

        zvuk.Zaustavi();                               // poslednja pesma dana se završila

        var cekanje = Stopwatch.StartNew();
        while (!stream.Ponoc && cekanje.Elapsed < TimeSpan.FromSeconds(15))
            await Task.Delay(200);

        Tvrdi(stream.Ponoc, $"u ponoć emitovanje nije prekinuto: {stream.Stanje} {stream.Poruka}");
        Tvrdi(stream.Stanje == StreamStanje.Iskljucen, $"stanje posle ponoći: {stream.Stanje}");
        Tvrdi(!stream.Zeljeno, "program je ostao uključen posle ponoći");
        Tvrdi(stream.Poruka == StreamEmisija.PorukaPonoc, $"poruka posle ponoći: {stream.Poruka}");

        await Task.Delay(2500);   // nadzor više ne sme da vrati vezu na server
        Tvrdi(stream.Stanje == StreamStanje.Iskljucen, $"posle ponoći se ipak vraća na server: {stream.Stanje}");
    }

    /// <summary>Icecast koji prihvati izvor i odgovori 200; zvuk čita i baca, ništa ne pušta.</summary>
    private sealed class LazniIcecast : IDisposable
    {
        private readonly TcpListener _slusalac;
        private readonly List<TcpClient> _izvori = [];
        private readonly CancellationTokenSource _kraj = new();

        public int Port { get; }

        /// <summary>How many source connections arrived so far (the test checks that nothing is opened too early).</summary>
        public int BrojIzvora
        {
            get { lock (_izvori) return _izvori.Count; }
        }

        public LazniIcecast()
        {
            _slusalac = new TcpListener(IPAddress.Loopback, 0);
            _slusalac.Start();
            Port = ((IPEndPoint)_slusalac.LocalEndpoint).Port;
            _ = Prihvati();
        }

        private async Task Prihvati()
        {
            while (!_kraj.IsCancellationRequested)
            {
                TcpClient izvor;
                try { izvor = await _slusalac.AcceptTcpClientAsync(_kraj.Token); }
                catch { return; }
                lock (_izvori) _izvori.Add(izvor);
                _ = Usluzi(izvor);
            }
        }

        private async Task Usluzi(TcpClient izvor)
        {
            try
            {
                var tok = izvor.GetStream();
                var bafer = new byte[8192];
                if (await tok.ReadAsync(bafer, _kraj.Token) <= 0) return;   // SOURCE zahtev izvora
                await tok.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.0 200 OK\r\n\r\n"), _kraj.Token);
                while (!_kraj.IsCancellationRequested && await tok.ReadAsync(bafer, _kraj.Token) > 0) { }
            }
            catch { }
        }

        /// <summary>Zatvori vezu kao da je server pao; slušalac ostaje da primi ponovni dolazak.</summary>
        public void Spusti()
        {
            lock (_izvori)
            {
                foreach (var izvor in _izvori)
                {
                    try { izvor.Close(); } catch { }
                }
                _izvori.Clear();
            }
        }

        public void Dispose()
        {
            _kraj.Cancel();
            try { _slusalac.Stop(); } catch { }
            Spusti();
            _kraj.Dispose();
        }
    }
}

