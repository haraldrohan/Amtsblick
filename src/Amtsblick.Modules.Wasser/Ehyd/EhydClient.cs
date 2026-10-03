using System.Text.Json;

namespace Amtsblick.Modules.Wasser.Ehyd;

/// <summary>
/// Holt den Gesamtbestand der aktuellen Pegel. Angesprochen werden ausschließlich die hier
/// festgelegten Adressen; Links aus den Antworten werden nicht verfolgt, geblättert wird über
/// <c>startIndex</c>. Wann abgerufen werden darf, entscheidet allein der <see cref="PegelDienst"/>.
/// </summary>
public sealed class EhydClient(IHttpClientFactory fabrik)
{
    /// <summary>Name des HttpClients und der Quelle im Kontingent-Register.</summary>
    public const string Quelle = "ehyd";

    public const string Layer = "i000501:pegel_aktuell";
    public const string OgcBasis = "https://gis.lfrz.gv.at/api/geodata/i000501/ogc/features/v1";
    public const string WfsUrl =
        "https://gis.lfrz.gv.at/api/geodata/i000501/wfs?service=WFS&version=1.0.0&request=GetFeature"
        + "&typeName=" + Layer + "&outputFormat=application/json&srsName=EPSG:4326";

    private const int Seitengroesse = 1000;
    private const int MaxSeiten = 20;

    private string? _collectionId;

    /// <summary>Gesamtbestand über die OGC API Features; schlägt das fehl, einmal über WFS.</summary>
    public async Task<(IReadOnlyList<Messstelle> Messstellen, string Weg)> HoleGesamtbestandAsync(CancellationToken ct)
    {
        try
        {
            return (await HoleUeberOgcApiAsync(ct), "OGC API Features");
        }
        catch (Exception fehler) when (IstAbruffehler(fehler) && !IstAbweisung(fehler))
        {
            using var dokument = await HoleAsync(WfsUrl, ct);
            return (PegelParser.Lies(dokument.RootElement).Messstellen, "WFS");
        }
    }

    public static bool IstAbruffehler(Exception fehler) =>
        fehler is HttpRequestException or TaskCanceledException or JsonException
            or KeyNotFoundException or FormatException or InvalidOperationException;

    // Weist der Host die Anfrage ab (403, 429), wird nicht auf einem zweiten Weg nachgefasst.
    private static bool IstAbweisung(Exception fehler) =>
        fehler is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.TooManyRequests };

    private async Task<IReadOnlyList<Messstelle>> HoleUeberOgcApiAsync(CancellationToken ct)
    {
        _collectionId ??= await ErmittleCollectionIdAsync(ct);
        var alle = new List<Messstelle>();
        for (var seite = 0; seite < MaxSeiten; seite++)
        {
            var url = $"{OgcBasis}/collections/{_collectionId}/items?f=json&limit={Seitengroesse}&startIndex={alle.Count}";
            using var dokument = await HoleAsync(url, ct);
            var ergebnis = PegelParser.Lies(dokument.RootElement);
            alle.AddRange(ergebnis.Messstellen);
            if (ergebnis.Messstellen.Count == 0 || ergebnis.Gesamt is null || alle.Count >= ergebnis.Gesamt)
            {
                break;
            }
        }

        return alle;
    }

    // Die Collection-ID wird je Instanz einmal über /collections bestimmt.
    private async Task<string> ErmittleCollectionIdAsync(CancellationToken ct)
    {
        using var dokument = await HoleAsync($"{OgcBasis}/collections?f=json", ct);
        var ids = dokument.RootElement.GetProperty("collections").EnumerateArray()
            .Select(c => c.GetProperty("id").GetString())
            .ToList();
        return ids.FirstOrDefault(id => id == Layer)
            ?? ids.FirstOrDefault(id => id is not null && id.EndsWith("pegel_aktuell", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Collection pegel_aktuell nicht in /collections gefunden.");
    }

    private async Task<JsonDocument> HoleAsync(string url, CancellationToken ct)
    {
        using var http = fabrik.CreateClient(Quelle);
        using var antwort = await http.GetAsync(url, ct);
        antwort.EnsureSuccessStatusCode();
        await using var strom = await antwort.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(strom, cancellationToken: ct);
    }
}
