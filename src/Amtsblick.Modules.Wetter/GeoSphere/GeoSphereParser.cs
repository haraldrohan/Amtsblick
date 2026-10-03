using System.Globalization;
using System.Text.Json;
using Amtsblick.Core.Ort;

namespace Amtsblick.Modules.Wetter.GeoSphere;

/// <summary>Liest die GeoJSON-Antworten und die Metadaten der GeoSphere Dataset API.</summary>
public static class GeoSphereParser
{
    /// <summary>Punkt-Zeitreihe; bei mehreren Punkten in der Antwort wird der erste gelesen.</summary>
    public static Zeitreihe LiesZeitreihe(JsonElement wurzel)
    {
        var zeitpunkte = wurzel.GetProperty("timestamps").EnumerateArray().Select(LiesZeit).ToList();
        var feature = wurzel.GetProperty("features")[0];
        var koordinaten = feature.GetProperty("geometry").GetProperty("coordinates");
        var parameter = new Dictionary<string, Parameterreihe>();
        foreach (var eintrag in feature.GetProperty("properties").GetProperty("parameters").EnumerateObject())
        {
            var werte = eintrag.Value.GetProperty("data").EnumerateArray()
                .Select(w => w.ValueKind == JsonValueKind.Number ? w.GetDouble() : (double?)null)
                .ToList();
            parameter[eintrag.Name] = new Parameterreihe(
                eintrag.Value.GetProperty("name").GetString() ?? eintrag.Name,
                eintrag.Value.GetProperty("unit").GetString() ?? "",
                werte);
        }

        return new Zeitreihe(
            LiesZeit(wurzel.GetProperty("reference_time")),
            zeitpunkte,
            new Koordinate(koordinaten[1].GetDouble(), koordinaten[0].GetDouble()),
            parameter);
    }

    public static Metadaten LiesMetadaten(JsonElement wurzel) => new(
        wurzel.GetProperty("title").GetString() ?? "",
        LiesZeit(wurzel.GetProperty("last_forecast_reftime")),
        wurzel.GetProperty("forecast_length").GetInt32(),
        wurzel.GetProperty("frequency").GetString() ?? "",
        wurzel.GetProperty("parameters").EnumerateArray()
            .Select(p => new ParameterInfo(
                p.GetProperty("name").GetString() ?? "",
                p.GetProperty("long_name").GetString() ?? "",
                p.GetProperty("unit").GetString() ?? ""))
            .ToList());

    // Die API schreibt Zeiten ohne Sekunden: 2026-10-02T19:00+00:00.
    private static DateTimeOffset LiesZeit(JsonElement element) =>
        DateTimeOffset.Parse(element.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
}
