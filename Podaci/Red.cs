namespace Bronxamp.Podaci;

public enum Ponavljanje { Ne, Sve, Jedna }

/// <summary>The play queue: what plays now and what follows, with shuffle and repeat. No audio in here.</summary>
public sealed class Red
{
    // Wrapper gives each entry its own identity: the same song can be in a show twice.
    private sealed record Stavka(Pesma Pesma);

    private List<Stavka> _izvorni = [];
    private List<Stavka> _redosled = [];
    private int _pozicija = -1;
    private bool _mesanje;
    private readonly Random _slucajno;

    public Red(int? seme = null) => _slucajno = seme is int s ? new Random(s) : new Random();

    public string Naziv { get; private set; } = "";
    public Emisija? Emisija { get; private set; }
    public bool JeAlbum { get; private set; }
    public Ponavljanje Ponavljanje { get; set; }

    public int Ukupno => _redosled.Count;
    public Pesma? Tekuca => _pozicija >= 0 && _pozicija < _redosled.Count ? _redosled[_pozicija].Pesma : null;

    public bool Mesanje
    {
        get => _mesanje;
        set
        {
            if (_mesanje == value) return;
            _mesanje = value;
            if (_redosled.Count == 0) return;

            var tekuca = _pozicija >= 0 && _pozicija < _redosled.Count ? _redosled[_pozicija] : null;
            if (value)
            {
                Promesaj(tekuca);
            }
            else
            {
                _redosled = [.. _izvorni];
                _pozicija = tekuca == null ? -1 : _redosled.IndexOf(tekuca);
            }
        }
    }

    public void Postavi(IEnumerable<Pesma> pesme, int pocetak, string naziv, Emisija? emisija = null, bool jeAlbum = false)
    {
        _izvorni = pesme.Select(p => new Stavka(p)).ToList();
        _redosled = [.. _izvorni];
        _pozicija = _redosled.Count == 0 ? -1 : Math.Clamp(pocetak, 0, _redosled.Count - 1);
        Naziv = naziv;
        Emisija = emisija;
        JeAlbum = jeAlbum;
        if (_mesanje && _pozicija >= 0) Promesaj(_redosled[_pozicija]);
    }

    /// <summary>Shuffles what has not played yet. Everything else stays where it is.</summary>
    private void Promesaj(Stavka? tekuca)
    {
        var ostale = _izvorni.Where(s => s != tekuca).ToList();
        for (int i = ostale.Count - 1; i > 0; i--)
        {
            int j = _slucajno.Next(i + 1);
            (ostale[i], ostale[j]) = (ostale[j], ostale[i]);
        }
        _redosled = tekuca == null ? ostale : [tekuca, .. ostale];
        _pozicija = tekuca == null ? -1 : 0;
    }

    private int SledecaPozicija()
    {
        if (_redosled.Count == 0) return -1;
        if (Ponavljanje == Ponavljanje.Jedna) return _pozicija;
        if (_pozicija + 1 < _redosled.Count) return _pozicija + 1;
        return Ponavljanje == Ponavljanje.Sve ? 0 : -1;
    }

    /// <summary>What plays after the current song on its own, without the user pressing anything.</summary>
    public Pesma? Sledeca
    {
        get
        {
            int i = SledecaPozicija();
            return i < 0 ? null : _redosled[i].Pesma;
        }
    }

    public bool Napred()
    {
        int i = SledecaPozicija();
        if (i < 0) return false;
        _pozicija = i;
        return true;
    }

    /// <summary>The Next button. Unlike the automatic advance it leaves a repeated song.</summary>
    public bool RucnoNapred()
    {
        if (_redosled.Count == 0) return false;
        if (_pozicija + 1 < _redosled.Count) _pozicija++;
        else if (Ponavljanje != Ponavljanje.Ne) _pozicija = 0;
        else return false;
        return true;
    }

    public bool Nazad()
    {
        if (_redosled.Count == 0) return false;
        if (_pozicija > 0) _pozicija--;
        else if (Ponavljanje == Ponavljanje.Sve) _pozicija = _redosled.Count - 1;
        else return false;
        return true;
    }

    /// <summary>Songs after the current one, in play order.</summary>
    public IEnumerable<Pesma> Preostale => _redosled.Skip(_pozicija + 1).Select(s => s.Pesma);

    /// <summary>Jumps to the n-th song after the current one.</summary>
    public bool SkociNa(int kolikoUnapred)
    {
        int i = _pozicija + 1 + kolikoUnapred;
        if (i < 0 || i >= _redosled.Count) return false;
        _pozicija = i;
        return true;
    }

    public void DodajNaKraj(IEnumerable<Pesma> pesme)
    {
        foreach (var p in pesme)
        {
            var s = new Stavka(p);
            _izvorni.Add(s);
            _redosled.Add(s);
        }
        if (_pozicija < 0 && _redosled.Count > 0) _pozicija = 0;
    }

    public void DodajSledece(IEnumerable<Pesma> pesme)
    {
        int gde = _pozicija + 1;
        foreach (var p in pesme)
        {
            var s = new Stavka(p);
            _redosled.Insert(gde, s);
            int uIzvornom = _pozicija >= 0 ? _izvorni.IndexOf(_redosled[_pozicija]) + (gde - _pozicija) : gde;
            _izvorni.Insert(Math.Min(uIzvornom, _izvorni.Count), s);
            gde++;
        }
        if (_pozicija < 0 && _redosled.Count > 0) _pozicija = 0;
    }

    /// <summary>All songs in play order, including those already played.</summary>
    public IEnumerable<Pesma> SvePesme => _redosled.Select(s => s.Pesma);

    /// <summary>Removes the n-th song after the current one.</summary>
    public void UkloniUnapred(int kolikoUnapred)
    {
        int i = _pozicija + 1 + kolikoUnapred;
        if (kolikoUnapred < 0 || i >= _redosled.Count) return;
        _izvorni.Remove(_redosled[i]);
        _redosled.RemoveAt(i);
    }

    /// <summary>Removes the current song. True when a following song took its place.</summary>
    public bool UkloniTekucu()
    {
        if (_pozicija < 0 || _pozicija >= _redosled.Count) return false;
        _izvorni.Remove(_redosled[_pozicija]);
        _redosled.RemoveAt(_pozicija);
        if (_pozicija < _redosled.Count) return true;
        _pozicija = _redosled.Count - 1;
        return false;
    }

    /// <summary>Drops a song that cannot be played, wherever it is after the current position.</summary>
    public void Izbaci(Pesma pesma)
    {
        for (int i = _redosled.Count - 1; i > _pozicija; i--)
            if (_redosled[i].Pesma == pesma)
            {
                _izvorni.Remove(_redosled[i]);
                _redosled.RemoveAt(i);
            }
    }

    /// <summary>The show's songs were edited while it plays: keep playing, follow the new list.</summary>
    public void OsveziEmisiju(IReadOnlyList<Pesma> pesme)
    {
        var tekuca = Tekuca;
        var mesanje = _mesanje;
        int gde = tekuca == null ? -1 : pesme.ToList().IndexOf(tekuca);
        if (gde < 0) return;
        _mesanje = false;
        Postavi(pesme, gde, Naziv, Emisija, JeAlbum);
        Mesanje = mesanje;
    }

    public void Isprazni()
    {
        _izvorni = [];
        _redosled = [];
        _pozicija = -1;
        Naziv = "";
        Emisija = null;
        JeAlbum = false;
    }
}

public static class KrajEmisije
{
    /// <summary>
    /// Seconds until the last song ends. Each crossfade overlaps two songs, so it takes that much off the
    /// total. The engine shortens a crossfade to half of the shorter song; this has to match it.
    /// </summary>
    public static double Preostalo(double preostaloTekuce, double trajanjeTekuce, IEnumerable<double> sledece, double prelaz)
    {
        double ukupno = Math.Max(0, preostaloTekuce);
        double prethodna = trajanjeTekuce;
        foreach (var trajanje in sledece)
        {
            ukupno += trajanje - Math.Min(prelaz, Math.Min(prethodna, trajanje) / 2);
            prethodna = trajanje;
        }
        return ukupno;
    }
}
