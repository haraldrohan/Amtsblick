using System.Globalization;
using System.Text.Json;
using Amtsblick.Core.Ort;

namespace Amtsblick.Modules.Wasser.Ehyd;

/// <summary>Pegelmessstelle mit aktuellem Wert. <see cref="Parameter"/> ist "W" (Wasserstand) oder "Q" (Durchfluss).</summary>
public sealed record Messstelle(
    int Hzbnr,
    string Name,
    string Gewaesser,
    string Hydrodienst,
    string? Internet,
    string Parameter,
    double? Wert,
    string Einheit,
    double? WertWCm,
    DateTimeOffset? Zeitpunkt,
    Pegelstatus Status,
    Koordinate? Ort,
    string? Prognose,
    string? Land,
    string? Gkz = null,
    string? Gemeinde = null,
    string? Bundesland = null);

public sealed record PegelSeite(IReadOnlyList<Messstelle> Messstellen, int? Gesamt);

/// <summary>
/// Liest eine GeoJSON-FeatureCollection des Layers <c>i000501:pegel_aktuell</c> (OGC API Features oder WFS).
/// Nicht jede Ausgabe enthält alle Felder: <c>wertw_cm</c>, <c>prognose</c>, <c>land</c>, <c>lat</c> und
/// <c>lon</c> sind optional; fehlen <c>lat</c>/<c>lon</c>, kommt die Position aus der Geometrie.
/// </summary>
public static class PegelParser
{
    public static PegelSeite Lies(JsonElement wurzel)
    {
        var messstellen = new List<Messstelle>();
        foreach (var feature in wurzel.GetProperty("features").EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var e) || Zahl(e, "hzbnr") is not { } hzbnr)
            {
                continue;
            }

            var code = Zahl(e, "gesamtcode");
            messstellen.Add(new Messstelle(
                (int)hzbnr,
                Text(e, "messstelle") ?? "",
                Text(e, "gewaesser") ?? "",
                Text(e, "hydrodienst") ?? "",
                Text(e, "internet"),
                Text(e, "parameter") ?? "",
                Zahl(e, "wert"),
                Text(e, "einheit") ?? "",
                Zahl(e, "wertw_cm"),
                Text(e, "zeitpunkt") is { } z
                    && DateTimeOffset.TryParse(z, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var zeitpunkt)
                    ? zeitpunkt
                    : null,
                Pegelstatus.Zerlege(code is { } c && c == Math.Floor(c) ? (int)c : null),
                Position(feature, e),
                Text(e, "prognose"),
                Text(e, "land")));
        }

        int? gesamt = wurzel.TryGetProperty("numberMatched", out var n) && n.ValueKind == JsonValueKind.Number
            ? n.GetInt32()
            : null;
        return new PegelSeite(messstellen, gesamt);
    }

    private static Koordinate? Position(JsonElement feature, JsonElement eigenschaften)
    {
        if (Zahl(eigenschaften, "lat") is { } lat && Zahl(eigenschaften, "lon") is { } lon)
        {
            return new Koordinate(lat, lon);
        }

        if (feature.TryGetProperty("geometry", out var geometrie) && geometrie.ValueKind == JsonValueKind.Object
            && geometrie.TryGetProperty("coordinates", out var k) && k.ValueKind == JsonValueKind.Array && k.GetArrayLength() >= 2)
        {
            var (x, y) = (k[0].GetDouble(), k[1].GetDouble());

            // GeoJSON schreibt [Länge, Breite]; manche WFS-Ausgaben liefern für EPSG:4326 die umgekehrte
            // Reihenfolge. In Österreich (Breite 46–49, Länge 9–17) ist das eindeutig unterscheidbar.
            return x > 40 && y < 40 ? new Koordinate(x, y) : new Koordinate(y, x);
        }

        return null;
    }

    private static string? Text(JsonElement eigenschaften, string name)
    {
        if (!eigenschaften.TryGetProperty(name, out var wert))
        {
            return null;
        }

        var text = wert.ValueKind switch
        {
            JsonValueKind.String => wert.GetString(),
            JsonValueKind.Number => wert.GetRawText(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    // Zahlen kommen je nach Feld als JSON-Zahl oder als Text, teils mit Dezimalkomma.
    private static double? Zahl(JsonElement eigenschaften, string name)
    {
        if (!eigenschaften.TryGetProperty(name, out var wert))
        {
            return null;
        }

        return wert.ValueKind switch
        {
            JsonValueKind.Number => wert.GetDouble(),
            JsonValueKind.String when double.TryParse(
                wert.GetString()!.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var zahl) => zahl,
            _ => null,
        };
    }
}
