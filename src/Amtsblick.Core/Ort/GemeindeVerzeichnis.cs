namespace Amtsblick.Core.Ort;

/// <summary>Alle Gemeinden im Speicher; wird beim Start einmal aus SQLite geladen.</summary>
public sealed class GemeindeVerzeichnis
{
    private readonly Dictionary<string, Gemeinde> _nachGkz;

    public GemeindeVerzeichnis(IEnumerable<Gemeinde> gemeinden, DateOnly? gebietsstand = null, DateTimeOffset? importiert = null)
    {
        Alle = gemeinden.OrderBy(g => g.Gkz, StringComparer.Ordinal).ToList();
        _nachGkz = Alle.ToDictionary(g => g.Gkz);
        Gebietsstand = gebietsstand;
        Importiert = importiert;
    }

    public static GemeindeVerzeichnis Leer { get; } = new([]);

    public IReadOnlyList<Gemeinde> Alle { get; }

    /// <summary>Gebietsstand der importierten Daten (z. B. 2026-01-01).</summary>
    public DateOnly? Gebietsstand { get; }

    /// <summary>Zeitpunkt des Imports, also des letzten Abrufs bei der Statistik Austria.</summary>
    public DateTimeOffset? Importiert { get; }

    public bool IstLeer => Alle.Count == 0;

    public Gemeinde? NachGkz(string gkz) => _nachGkz.GetValueOrDefault(gkz);

    /// <summary>
    /// Punkt-in-Polygon. Überlappen sich Flächen (Wien gesamt und Wiener Bezirk), gewinnt die kleinere.
    /// </summary>
    public Gemeinde? Finde(Koordinate punkt) =>
        Alle.Where(g => g.Flaeche is not null && g.Flaeche.Enthaelt(punkt))
            .MinBy(g => (g.Flaeche!.MaxLat - g.Flaeche.MinLat) * (g.Flaeche.MaxLon - g.Flaeche.MinLon));
}
