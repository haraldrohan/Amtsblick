using Amtsblick.Core.Ort;

namespace Amtsblick.Modules.Wetter.GeoSphere;

/// <summary>Datensatz der GeoSphere Dataset API mit den Parametern, die Amtsblick abruft.</summary>
/// <param name="Doi">DOI, die GeoSphere für den Datensatz vergibt, als URL.</param>
public sealed record Ressource(string Id, string Pfad, IReadOnlyList<string> Parameter, TimeSpan Ttl, string Doi)
{
    /// <summary>Stündliche Prognose, 61 h, neuer Lauf alle 3 h.</summary>
    public static Ressource Prognose { get; } = new(
        "nwp-v2-1h-1km",
        "timeseries/forecast/nwp-v2-1h-1km",
        ["2t", "tp", "rain", "sf", "snowlmt", "10u", "10v", "10fg", "tcc", "sund"],
        TimeSpan.FromHours(3),
        "https://doi.org/10.60669/rv80-9d61");

    /// <summary>Nowcast in 15-Minuten-Schritten, 13 Schritte.</summary>
    public static Ressource Nowcast { get; } = new(
        "nowcast-v1-15min-1km",
        "timeseries/forecast/nowcast-v1-15min-1km",
        ["rr", "pt", "t2m", "ff", "fx"],
        TimeSpan.FromMinutes(15),
        "https://doi.org/10.60669/ahad-4y43");
}

public sealed record Parameterreihe(string Name, string Einheit, IReadOnlyList<double?> Werte);

/// <summary>Punkt-Zeitreihe. Der Wert zum Zeitpunkt T gilt für das Intervall, das bei T endet.</summary>
public sealed record Zeitreihe(
    DateTimeOffset Referenzzeit,
    IReadOnlyList<DateTimeOffset> Zeitpunkte,
    Koordinate Gitterpunkt,
    IReadOnlyDictionary<string, Parameterreihe> Parameter)
{
    public double? Wert(string parameter, int index) =>
        Parameter.TryGetValue(parameter, out var reihe) && index < reihe.Werte.Count ? reihe.Werte[index] : null;
}

public sealed record ParameterInfo(string Name, string Beschreibung, string Einheit);

public sealed record Metadaten(
    string Titel,
    DateTimeOffset LetzterLauf,
    int Vorhersagelaenge,
    string Frequenz,
    IReadOnlyList<ParameterInfo> Parameter);
