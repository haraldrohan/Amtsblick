namespace Amtsblick.Core.Ort;

/// <summary>
/// Alle Gemeinden im Speicher. Der Inhalt wird beim Start aus SQLite geladen und kann zur Laufzeit
/// als Ganzes ersetzt werden, wenn der erste Import erst nach dem Start fertig wird.
/// </summary>
public sealed class GemeindeVerzeichnis
{
    private volatile Zustand _zustand;

    public GemeindeVerzeichnis(IEnumerable<Gemeinde> gemeinden, DateOnly? gebietsstand = null, DateTimeOffset? importiert = null)
    {
        _zustand = new Zustand(gemeinden, gebietsstand, importiert, 0);
    }

    public static GemeindeVerzeichnis Leer => new([]);

    public IReadOnlyList<Gemeinde> Alle => _zustand.Alle;

    /// <summary>Gebietsstand der importierten Daten (z. B. 2026-01-01).</summary>
    public DateOnly? Gebietsstand => _zustand.Gebietsstand;

    /// <summary>Zeitpunkt des Imports, also des letzten Abrufs bei der Statistik Austria.</summary>
    public DateTimeOffset? Importiert => _zustand.Importiert;

    /// <summary>Erhöht sich bei jedem Ersetzen; abgeleitete Daten erkennen daran, dass sie veraltet sind.</summary>
    public int Version => _zustand.Version;

    public bool IstLeer => _zustand.Alle.Count == 0;

    /// <summary>
    /// Erklärung für Nutzer, warum das Verzeichnis leer ist (Import läuft oder ist gescheitert).
    /// Null, solange nichts Besonderes vorliegt.
    /// </summary>
    public string? Ladehinweis { get; set; }

    public Gemeinde? NachGkz(string gkz) => _zustand.NachGkz.GetValueOrDefault(gkz);

    /// <summary>
    /// Punkt-in-Polygon. Überlappen sich Flächen (Wien gesamt und Wiener Bezirk), gewinnt die kleinere.
    /// </summary>
    public Gemeinde? Finde(Koordinate punkt) =>
        _zustand.Alle.Where(g => g.Flaeche is not null && g.Flaeche.Enthaelt(punkt))
            .MinBy(g => (g.Flaeche!.MaxLat - g.Flaeche.MinLat) * (g.Flaeche.MaxLon - g.Flaeche.MinLon));

    /// <summary>Übernimmt den Inhalt eines frisch geladenen Verzeichnisses.</summary>
    public void Ersetze(GemeindeVerzeichnis neu)
    {
        _zustand = new Zustand(neu.Alle, neu.Gebietsstand, neu.Importiert, _zustand.Version + 1);
        Ladehinweis = null;
    }

    private sealed class Zustand
    {
        public Zustand(IEnumerable<Gemeinde> gemeinden, DateOnly? gebietsstand, DateTimeOffset? importiert, int version)
        {
            Alle = gemeinden.OrderBy(g => g.Gkz, StringComparer.Ordinal).ToList();
            NachGkz = Alle.ToDictionary(g => g.Gkz);
            Gebietsstand = gebietsstand;
            Importiert = importiert;
            Version = version;
        }

        public IReadOnlyList<Gemeinde> Alle { get; }
        public Dictionary<string, Gemeinde> NachGkz { get; }
        public DateOnly? Gebietsstand { get; }
        public DateTimeOffset? Importiert { get; }
        public int Version { get; }
    }
}
