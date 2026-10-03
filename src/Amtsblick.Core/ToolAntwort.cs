using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Amtsblick.Core.Quellen;

namespace Amtsblick.Core;

/// <summary>
/// Einheitliche Form aller Tool-Antworten: ein kompaktes JSON-Objekt, das mit einer Zeile
/// <c>zusammenfassung</c> beginnt und mit <c>quellen</c> und <c>hinweise</c> endet.
/// </summary>
public static class ToolAntwort
{
    public static JsonSerializerOptions Json { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <param name="daten">Objekt, dessen Eigenschaften in die Antwort übernommen werden; null für keine.</param>
    public static string Erzeuge(
        string zusammenfassung, object? daten, IEnumerable<Quellenvermerk> quellen, IEnumerable<string?> hinweise)
    {
        var antwort = new JsonObject { ["zusammenfassung"] = zusammenfassung };
        if (daten is not null && JsonSerializer.SerializeToNode(daten, Json) is JsonObject objekt)
        {
            foreach (var (name, wert) in objekt.ToList())
            {
                objekt.Remove(name);
                antwort[name] = wert;
            }
        }

        antwort["quellen"] = JsonSerializer.SerializeToNode(quellen, Json);
        antwort["hinweise"] = JsonSerializer.SerializeToNode(hinweise.Where(h => !string.IsNullOrWhiteSpace(h)), Json);
        return antwort.ToJsonString(Json);
    }
}

/// <summary>Zeitangaben für Antworten in österreichischer Ortszeit.</summary>
public static class Zeit
{
    public static TimeZoneInfo Wien { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    public static DateTimeOffset Lokal(DateTimeOffset zeitpunkt) => TimeZoneInfo.ConvertTime(zeitpunkt, Wien);

    /// <summary>ISO 8601 auf die Minute mit Zeitzonenversatz, z. B. 2026-10-02T21:00+02:00.</summary>
    public static string Iso(DateTimeOffset zeitpunkt) =>
        Lokal(zeitpunkt).ToString("yyyy-MM-dd'T'HH:mmzzz", CultureInfo.InvariantCulture);

    public static string? Iso(DateTimeOffset? zeitpunkt) => zeitpunkt is { } z ? Iso(z) : null;

    /// <summary>Für Fließtext, z. B. "02.10. 21:00".</summary>
    public static string Kurz(DateTimeOffset zeitpunkt) =>
        Lokal(zeitpunkt).ToString("dd.MM. HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Zahl mit Dezimalkomma für Fließtext.</summary>
    public static string Zahl(double wert, int stellen = 1) =>
        Math.Round(wert, stellen).ToString("F" + stellen, CultureInfo.GetCultureInfo("de-AT"));
}
