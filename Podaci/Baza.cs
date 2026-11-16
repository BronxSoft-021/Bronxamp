using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Bronxamp.Podaci;

/// <summary>
/// SQLite storage. Songs whose file is gone stay in the table flagged "nestala": their play history and
/// place in shows must survive a disk that is unplugged or a file that was moved.
/// </summary>
public sealed class Baza : IDisposable
{
    private const string FormatVremena = "yyyy-MM-dd HH:mm:ss";

    private readonly SqliteConnection _veza;
    private readonly object _brava = new();

    public Baza(string putanja)
    {
        _veza = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = putanja }.ToString());
        _veza.Open();
        Izvrsi("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=OFF;");
        Izvrsi("""
            CREATE TABLE IF NOT EXISTS folderi(
                id INTEGER PRIMARY KEY, putanja TEXT UNIQUE NOT NULL);
            CREATE TABLE IF NOT EXISTS albumi(
                id INTEGER PRIMARY KEY, kljuc TEXT UNIQUE NOT NULL, naziv TEXT NOT NULL, izvodjac TEXT NOT NULL,
                godina INTEGER NOT NULL, folder TEXT NOT NULL, dodato TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS pesme(
                id INTEGER PRIMARY KEY, putanja TEXT UNIQUE NOT NULL, velicina INTEGER NOT NULL, izmenjeno INTEGER NOT NULL,
                naslov TEXT NOT NULL, izvodjac TEXT NOT NULL, album_id INTEGER NOT NULL, godina INTEGER NOT NULL,
                zanr TEXT NOT NULL, broj INTEGER NOT NULL, disk INTEGER NOT NULL, trajanje REAL NOT NULL,
                omiljeno INTEGER NOT NULL DEFAULT 0, nestala INTEGER NOT NULL DEFAULT 0);
            CREATE INDEX IF NOT EXISTS pesme_album ON pesme(album_id);
            CREATE INDEX IF NOT EXISTS pesme_nestale ON pesme(velicina) WHERE nestala=1;
            CREATE TABLE IF NOT EXISTS emisije(
                id INTEGER PRIMARY KEY, ime TEXT NOT NULL, boja TEXT NOT NULL, redosled INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS emisija_pesme(
                emisija_id INTEGER NOT NULL, rb INTEGER NOT NULL, pesma_id INTEGER NOT NULL,
                PRIMARY KEY(emisija_id, rb));
            CREATE TABLE IF NOT EXISTS pustanja(
                id INTEGER PRIMARY KEY, pesma_id INTEGER NOT NULL, vreme TEXT NOT NULL, emisija_id INTEGER);
            CREATE INDEX IF NOT EXISTS pustanja_vreme ON pustanja(vreme);
            CREATE INDEX IF NOT EXISTS pustanja_pesma ON pustanja(pesma_id);
            CREATE INDEX IF NOT EXISTS pustanja_emisija ON pustanja(emisija_id, pesma_id);
            """);
        Nadogradi();
    }

    /// <summary>Adds what newer versions need to a library made by an older one.</summary>
    private void Nadogradi()
    {
        using var k = Komanda("SELECT COUNT(*) FROM pragma_table_info('pesme') WHERE name='iskljucena'");
        if ((long)k.ExecuteScalar()! == 0)
            Izvrsi("ALTER TABLE pesme ADD COLUMN iskljucena INTEGER NOT NULL DEFAULT 0");

        using var b = Komanda("SELECT COUNT(*) FROM pragma_table_info('pesme') WHERE name='bitrate'");
        if ((long)b.ExecuteScalar()! == 0)
        {
            Izvrsi("ALTER TABLE pesme ADD COLUMN bitrate INTEGER NOT NULL DEFAULT 0");
            Izvrsi("ALTER TABLE pesme ADD COLUMN frekvencija INTEGER NOT NULL DEFAULT 0");

            // Makes the next scan read every file once more, and that is where the new values come from.
            Izvrsi("UPDATE pesme SET izmenjeno=0");
        }

        // Nullable on purpose: NULL means "not measured yet", and the measurement fills it in later. No scan
        // is forced here, since measuring is a separate, slower job that can be started on its own.
        using var g = Komanda("SELECT COUNT(*) FROM pragma_table_info('pesme') WHERE name='jacina_db'");
        if ((long)g.ExecuteScalar()! == 0)
        {
            Izvrsi("ALTER TABLE pesme ADD COLUMN jacina_db REAL");
            Izvrsi("ALTER TABLE pesme ADD COLUMN vrh_db REAL");
        }
    }

    private void Izvrsi(string sql)
    {
        using var k = _veza.CreateCommand();
        k.CommandText = sql;
        k.ExecuteNonQuery();
    }

    private SqliteCommand Komanda(string sql, params (string Ime, object? Vrednost)[] parametri)
    {
        var k = _veza.CreateCommand();
        k.CommandText = sql;
        foreach (var (ime, vrednost) in parametri)
            k.Parameters.AddWithValue(ime, vrednost ?? DBNull.Value);
        return k;
    }

    private static string Vreme(DateTime v) => v.ToString(FormatVremena, CultureInfo.InvariantCulture);
    private static DateTime Vreme(string s) => DateTime.ParseExact(s, FormatVremena, CultureInfo.InvariantCulture);

    // ---- folders ----

    public List<string> Folderi()
    {
        lock (_brava)
        {
            using var k = Komanda("SELECT putanja FROM folderi ORDER BY putanja");
            using var r = k.ExecuteReader();
            var spisak = new List<string>();
            while (r.Read()) spisak.Add(r.GetString(0));
            return spisak;
        }
    }

    public void DodajFolder(string putanja)
    {
        lock (_brava)
        {
            using var k = Komanda("INSERT OR IGNORE INTO folderi(putanja) VALUES($p)", ("$p", putanja));
            k.ExecuteNonQuery();
        }
    }

    /// <summary>The user removed the folder on purpose, so its songs leave the library.</summary>
    public void UkloniFolder(string putanja)
    {
        lock (_brava)
        {
            using var t = _veza.BeginTransaction();
            using (var k = Komanda("DELETE FROM folderi WHERE putanja=$p", ("$p", putanja))) { k.Transaction = t; k.ExecuteNonQuery(); }
            var prefiks = putanja.TrimEnd('\\') + "\\";
            using (var k = Komanda("UPDATE pesme SET nestala=1 WHERE substr(putanja,1,length($p))=$p", ("$p", prefiks))) { k.Transaction = t; k.ExecuteNonQuery(); }
            t.Commit();
        }
    }

    // ---- songs and albums ----

    public (List<Album> Albumi, List<Pesma> Pesme) UcitajSve()
    {
        lock (_brava)
        {
            var albumi = new Dictionary<long, Album>();
            using (var k = Komanda("SELECT id, kljuc, naziv, izvodjac, godina, folder, dodato FROM albumi"))
            using (var r = k.ExecuteReader())
                while (r.Read())
                {
                    var a = new Album
                    {
                        Id = r.GetInt64(0), Kljuc = r.GetString(1), Naziv = r.GetString(2), Izvodjac = r.GetString(3),
                        Godina = r.GetInt32(4), Folder = r.GetString(5), Dodato = Vreme(r.GetString(6)),
                    };
                    albumi[a.Id] = a;
                }

            var pesme = new List<Pesma>();
            using (var k = Komanda("""
                SELECT id, putanja, velicina, izmenjeno, naslov, izvodjac, album_id, godina, zanr, broj, disk, trajanje, omiljeno,
                       bitrate, frekvencija, jacina_db, vrh_db
                FROM pesme WHERE nestala=0 AND iskljucena=0 ORDER BY album_id, disk, broj, putanja
                """))
            using (var r = k.ExecuteReader())
                while (r.Read())
                {
                    long albumId = r.GetInt64(6);
                    if (!albumi.TryGetValue(albumId, out var album)) continue;
                    var p = new Pesma
                    {
                        Id = r.GetInt64(0), Putanja = r.GetString(1), Velicina = r.GetInt64(2), Izmenjeno = r.GetInt64(3),
                        Naslov = r.GetString(4), Izvodjac = r.GetString(5), AlbumId = albumId, Godina = r.GetInt32(7),
                        Zanr = r.GetString(8), Broj = r.GetInt32(9), Disk = r.GetInt32(10), Trajanje = r.GetDouble(11),
                        Omiljeno = r.GetInt32(12) != 0, Bitrate = r.GetInt32(13), Frekvencija = r.GetInt32(14), Album = album,
                        JacinaDb = r.IsDBNull(15) ? null : r.GetDouble(15),
                        VrhDb = r.IsDBNull(16) ? null : r.GetDouble(16),
                    };
                    p.ZaPretragu = Pretraga.TekstZaPretragu(p, album);
                    album.Pesme.Add(p);
                    pesme.Add(p);
                }

            using (var k = Komanda("""
                SELECT p.album_id, MAX(u.vreme) FROM pustanja u JOIN pesme p ON p.id=u.pesma_id GROUP BY p.album_id
                """))
            using (var r = k.ExecuteReader())
                while (r.Read())
                    if (albumi.TryGetValue(r.GetInt64(0), out var a))
                        a.PoslednjePustanje = Vreme(r.GetString(1));

            var poId = pesme.ToDictionary(p => p.Id);
            using (var k = Komanda("SELECT pesma_id, COUNT(*), MAX(vreme) FROM pustanja GROUP BY pesma_id"))
            using (var r = k.ExecuteReader())
                while (r.Read())
                    if (poId.TryGetValue(r.GetInt64(0), out var p))
                    {
                        p.Pustano = r.GetInt32(1);
                        p.PoslednjePustanje = Vreme(r.GetString(2));
                    }

            return (albumi.Values.Where(a => a.Pesme.Count > 0).ToList(), pesme);
        }
    }

    /// <summary>Path, size and date of every known song, including missing ones.</summary>
    public Dictionary<string, (long Velicina, long Izmenjeno, bool Nestala)> PoznatiFajlovi()
    {
        lock (_brava)
        {
            var spisak = new Dictionary<string, (long, long, bool)>(StringComparer.OrdinalIgnoreCase);
            using var k = Komanda("SELECT putanja, velicina, izmenjeno, nestala FROM pesme");
            using var r = k.ExecuteReader();
            while (r.Read()) spisak[r.GetString(0)] = (r.GetInt64(1), r.GetInt64(2), r.GetInt32(3) != 0);
            return spisak;
        }
    }

    /// <summary>Writes a batch of scanned songs in one transaction, creating albums as needed.</summary>
    public void Upisi(IReadOnlyList<SkeniranaPesma> pesme)
    {
        if (pesme.Count == 0) return;
        lock (_brava)
        {
            using var t = _veza.BeginTransaction();
            var sada = Vreme(DateTime.Now);
            foreach (var s in pesme)
            {
                long albumId;
                using (var k = Komanda("""
                    INSERT INTO albumi(kljuc, naziv, izvodjac, godina, folder, dodato) VALUES($k, $n, $i, $g, $f, $d)
                    ON CONFLICT(kljuc) DO UPDATE SET naziv=excluded.naziv, izvodjac=excluded.izvodjac,
                        godina=MAX(godina, excluded.godina), folder=excluded.folder
                    RETURNING id
                    """, ("$k", s.AlbumKljuc), ("$n", s.AlbumNaziv), ("$i", s.AlbumIzvodjac), ("$g", s.Godina),
                    ("$f", s.Folder), ("$d", sada)))
                {
                    k.Transaction = t;
                    albumId = (long)k.ExecuteScalar()!;
                }

                // A file that moved keeps its id, and with it its history and its place in shows.
                long premestena = s.Nova ? NadjiNestalu(t, s) : 0;
                if (premestena != 0)
                {
                    using var k = Komanda("UPDATE pesme SET putanja=$p, nestala=0, album_id=$a, izmenjeno=$m, bitrate=$br, frekvencija=$fr, " +
                                          "jacina_db=COALESCE($j, jacina_db), vrh_db=COALESCE($vr, vrh_db) WHERE id=$id",
                        ("$p", s.Putanja), ("$a", albumId), ("$m", s.Izmenjeno), ("$br", s.Bitrate), ("$fr", s.Frekvencija),
                        ("$j", s.JacinaDb), ("$vr", s.VrhDb), ("$id", premestena));
                    k.Transaction = t;
                    k.ExecuteNonQuery();
                    continue;
                }

                using (var k = Komanda("""
                    INSERT INTO pesme(putanja, velicina, izmenjeno, naslov, izvodjac, album_id, godina, zanr, broj, disk, trajanje, bitrate, frekvencija,
                                       jacina_db, vrh_db)
                    VALUES($p, $v, $m, $n, $i, $a, $g, $z, $b, $d, $t, $br, $fr, $j, $vr)
                    ON CONFLICT(putanja) DO UPDATE SET velicina=excluded.velicina, izmenjeno=excluded.izmenjeno,
                        naslov=excluded.naslov, izvodjac=excluded.izvodjac, album_id=excluded.album_id, godina=excluded.godina,
                        zanr=excluded.zanr, broj=excluded.broj, disk=excluded.disk, trajanje=excluded.trajanje,
                        bitrate=excluded.bitrate, frekvencija=excluded.frekvencija, nestala=0,
                        jacina_db=COALESCE(excluded.jacina_db, jacina_db), vrh_db=COALESCE(excluded.vrh_db, vrh_db)
                    """, ("$p", s.Putanja), ("$v", s.Velicina), ("$m", s.Izmenjeno), ("$n", s.Naslov), ("$i", s.Izvodjac),
                    ("$a", albumId), ("$g", s.Godina), ("$z", s.Zanr), ("$b", s.Broj), ("$d", s.Disk), ("$t", s.Trajanje),
                    ("$br", s.Bitrate), ("$fr", s.Frekvencija), ("$j", s.JacinaDb), ("$vr", s.VrhDb)))
                {
                    k.Transaction = t;
                    k.ExecuteNonQuery();
                }
            }
            t.Commit();
        }
    }

    private long NadjiNestalu(SqliteTransaction t, SkeniranaPesma s)
    {
        using var k = Komanda("SELECT id FROM pesme WHERE nestala=1 AND velicina=$v AND naslov=$n AND izvodjac=$i LIMIT 1",
            ("$v", s.Velicina), ("$n", s.Naslov), ("$i", s.Izvodjac));
        k.Transaction = t;
        return k.ExecuteScalar() is long id ? id : 0;
    }

    /// <summary>Stores what the measurement found for one song; see <see cref="Zvuk.Glasnoca"/>.</summary>
    public void UpisiGlasnocu(long pesmaId, double jacinaDb, double vrhDb)
    {
        lock (_brava)
        {
            using var k = Komanda("UPDATE pesme SET jacina_db=$j, vrh_db=$v WHERE id=$id",
                ("$j", jacinaDb), ("$v", vrhDb), ("$id", pesmaId));
            k.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Songs that still have no loudness — or every song, when the people asked for the whole library to be
    /// measured again. The oldest rows go first, so even a long job covers what was added first.
    /// </summary>
    public List<(long Id, string Putanja)> PesmeZaMerenje(bool sve = false, int najvise = int.MaxValue)
    {
        lock (_brava)
        {
            var upit = sve
                ? "SELECT id, putanja FROM pesme WHERE nestala=0 AND iskljucena=0 ORDER BY id LIMIT $n"
                : "SELECT id, putanja FROM pesme WHERE nestala=0 AND iskljucena=0 AND (jacina_db IS NULL OR vrh_db IS NULL) ORDER BY id LIMIT $n";
            using var k = Komanda(upit, ("$n", najvise));
            using var r = k.ExecuteReader();
            var spisak = new List<(long, string)>();
            while (r.Read()) spisak.Add((r.GetInt64(0), r.GetString(1)));
            return spisak;
        }
    }

    public void OznaciNestale(IEnumerable<string> putanje)
    {
        lock (_brava)
        {
            using var t = _veza.BeginTransaction();
            using var k = Komanda("UPDATE pesme SET nestala=1 WHERE putanja=$p", ("$p", ""));
            k.Transaction = t;
            foreach (var p in putanje)
            {
                k.Parameters["$p"].Value = p;
                k.ExecuteNonQuery();
            }
            t.Commit();
        }
    }

    public void OznaciPrisutne(IEnumerable<string> putanje)
    {
        lock (_brava)
        {
            using var t = _veza.BeginTransaction();
            using var k = Komanda("UPDATE pesme SET nestala=0 WHERE putanja=$p", ("$p", ""));
            k.Transaction = t;
            foreach (var p in putanje)
            {
                k.Parameters["$p"].Value = p;
                k.ExecuteNonQuery();
            }
            t.Commit();
        }
    }

    /// <summary>
    /// Hides an album the user removed. The files stay where they are and the scanner knows them, so they do
    /// not come back on their own.
    /// </summary>
    public void IskljuciAlbum(long albumId)
    {
        lock (_brava)
        {
            using var k = Komanda("UPDATE pesme SET iskljucena=1 WHERE album_id=$a", ("$a", albumId));
            k.ExecuteNonQuery();
        }
    }

    /// <summary>Same as an album, one song at a time.</summary>
    public void IskljuciPesme(IEnumerable<long> pesme)
    {
        lock (_brava)
        {
            using var t = _veza.BeginTransaction();
            using var k = Komanda("UPDATE pesme SET iskljucena=1 WHERE id=$id", ("$id", 0L));
            k.Transaction = t;
            foreach (var id in pesme)
            {
                k.Parameters["$id"].Value = id;
                k.ExecuteNonQuery();
            }
            t.Commit();
        }
    }

    public int BrojIskljucenihPesama()
    {
        lock (_brava)
        {
            using var k = Komanda("SELECT COUNT(*) FROM pesme WHERE iskljucena=1 AND nestala=0");
            return (int)(long)k.ExecuteScalar()!;
        }
    }

    public void VratiIskljucene()
    {
        lock (_brava)
        {
            using var k = Komanda("UPDATE pesme SET iskljucena=0 WHERE iskljucena=1");
            k.ExecuteNonQuery();
        }
    }

    /// <summary>The user added this music on purpose, so a earlier "remove from library" no longer holds.</summary>
    public void UkljuciPutanje(IEnumerable<string> putanje)
    {
        lock (_brava)
        {
            using var t = _veza.BeginTransaction();
            using var k = Komanda("UPDATE pesme SET iskljucena=0 WHERE putanja=$p OR substr(putanja,1,length($f))=$f", ("$p", ""), ("$f", ""));
            k.Transaction = t;
            foreach (var p in putanje)
            {
                k.Parameters["$p"].Value = p;
                k.Parameters["$f"].Value = p.TrimEnd('\\') + "\\";
                k.ExecuteNonQuery();
            }
            t.Commit();
        }
    }

    public void PostaviOmiljeno(long pesmaId, bool omiljeno)
    {
        lock (_brava)
        {
            using var k = Komanda("UPDATE pesme SET omiljeno=$o WHERE id=$id", ("$o", omiljeno ? 1 : 0), ("$id", pesmaId));
            k.ExecuteNonQuery();
        }
    }

    // ---- shows ----

    public List<Emisija> Emisije()
    {
        lock (_brava)
        {
            using var k = Komanda("SELECT id, ime, boja, redosled FROM emisije ORDER BY redosled, id");
            using var r = k.ExecuteReader();
            var spisak = new List<Emisija>();
            while (r.Read())
                spisak.Add(new Emisija { Id = r.GetInt64(0), Ime = r.GetString(1), Boja = r.GetString(2), Redosled = r.GetInt32(3) });
            return spisak;
        }
    }

    public Emisija NovaEmisija(string ime, string boja)
    {
        lock (_brava)
        {
            using var k = Komanda("""
                INSERT INTO emisije(ime, boja, redosled) VALUES($i, $b, (SELECT COALESCE(MAX(redosled),0)+1 FROM emisije))
                RETURNING id, redosled
                """, ("$i", ime), ("$b", boja));
            using var r = k.ExecuteReader();
            r.Read();
            return new Emisija { Id = r.GetInt64(0), Ime = ime, Boja = boja, Redosled = r.GetInt32(1) };
        }
    }

    public void IzmeniEmisiju(Emisija e)
    {
        lock (_brava)
        {
            using var k = Komanda("UPDATE emisije SET ime=$i, boja=$b WHERE id=$id", ("$i", e.Ime), ("$b", e.Boja), ("$id", e.Id));
            k.ExecuteNonQuery();
        }
    }

    /// <summary>The show goes away, but its plays stay in the listening statistics.</summary>
    public void ObrisiEmisiju(long id)
    {
        lock (_brava)
        {
            using var t = _veza.BeginTransaction();
            foreach (var sql in new[]
            {
                "DELETE FROM emisije WHERE id=$id",
                "DELETE FROM emisija_pesme WHERE emisija_id=$id",
                "UPDATE pustanja SET emisija_id=NULL WHERE emisija_id=$id",
            })
            {
                using var k = Komanda(sql, ("$id", id));
                k.Transaction = t;
                k.ExecuteNonQuery();
            }
            t.Commit();
        }
    }

    public List<long> PesmeEmisije(long emisijaId)
    {
        lock (_brava)
        {
            using var k = Komanda("SELECT pesma_id FROM emisija_pesme WHERE emisija_id=$e ORDER BY rb", ("$e", emisijaId));
            using var r = k.ExecuteReader();
            var spisak = new List<long>();
            while (r.Read()) spisak.Add(r.GetInt64(0));
            return spisak;
        }
    }

    public void SacuvajPesmeEmisije(long emisijaId, IReadOnlyList<long> pesme)
    {
        lock (_brava)
        {
            using var t = _veza.BeginTransaction();
            using (var k = Komanda("DELETE FROM emisija_pesme WHERE emisija_id=$e", ("$e", emisijaId)))
            {
                k.Transaction = t;
                k.ExecuteNonQuery();
            }
            using (var k = Komanda("INSERT INTO emisija_pesme(emisija_id, rb, pesma_id) VALUES($e, $r, $p)",
                       ("$e", emisijaId), ("$r", 0), ("$p", 0L)))
            {
                k.Transaction = t;
                for (int i = 0; i < pesme.Count; i++)
                {
                    k.Parameters["$r"].Value = i;
                    k.Parameters["$p"].Value = pesme[i];
                    k.ExecuteNonQuery();
                }
            }
            t.Commit();
        }
    }

    // ---- plays ----

    public void ZabeleziPustanje(long pesmaId, long? emisijaId, DateTime vreme)
    {
        lock (_brava)
        {
            using var k = Komanda("INSERT INTO pustanja(pesma_id, vreme, emisija_id) VALUES($p, $v, $e)",
                ("$p", pesmaId), ("$v", Vreme(vreme)), ("$e", emisijaId));
            k.ExecuteNonQuery();
        }
    }

    public List<Emitovanje> IstorijaEmisije(long emisijaId, int najvise = 500)
    {
        lock (_brava)
        {
            using var k = Komanda("SELECT pesma_id, vreme FROM pustanja WHERE emisija_id=$e ORDER BY vreme DESC LIMIT $n",
                ("$e", emisijaId), ("$n", najvise));
            using var r = k.ExecuteReader();
            var spisak = new List<Emitovanje>();
            while (r.Read()) spisak.Add(new Emitovanje(r.GetInt64(0), Vreme(r.GetString(1))));
            return spisak;
        }
    }

    /// <summary>Last time each song played in this show, since the given moment.</summary>
    public Dictionary<long, DateTime> PoslednjeUEmisiji(long emisijaId, DateTime od)
    {
        lock (_brava)
        {
            using var k = Komanda("""
                SELECT pesma_id, MAX(vreme) FROM pustanja WHERE emisija_id=$e AND vreme>=$od GROUP BY pesma_id
                """, ("$e", emisijaId), ("$od", Vreme(od)));
            using var r = k.ExecuteReader();
            var spisak = new Dictionary<long, DateTime>();
            while (r.Read()) spisak[r.GetInt64(0)] = Vreme(r.GetString(1));
            return spisak;
        }
    }

    public List<(long PesmaId, int Puta)> Najslusanije(DateTime od, int najvise = 100)
    {
        lock (_brava)
        {
            using var k = Komanda("""
                SELECT pesma_id, COUNT(*) c FROM pustanja WHERE vreme>=$od GROUP BY pesma_id ORDER BY c DESC, MAX(vreme) DESC LIMIT $n
                """, ("$od", Vreme(od)), ("$n", najvise));
            using var r = k.ExecuteReader();
            var spisak = new List<(long, int)>();
            while (r.Read()) spisak.Add((r.GetInt64(0), r.GetInt32(1)));
            return spisak;
        }
    }

    public void Dispose() => _veza.Dispose();
}
