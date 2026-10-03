using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Amtsblick.Core.Ort;

/// <summary>
/// Lädt Gemeindegrenzen und Bezirksnamen der Statistik Austria (Open Data, CC BY 4.0) und legt sie in
/// SQLite ab. Die jeweils neueste Ausgabe wird über die OGD-Metadaten von data.statistik.gv.at ermittelt.
/// </summary>
public sealed partial class GemeindeImport(HttpClient http)
{
    public const string MetadatenGemeinden = "https://data.statistik.gv.at/ogd/json?dataset=OGDEXT_GEM_1";
    public const string MetadatenBezirke = "https://data.statistik.gv.at/ogd/json?dataset=OGDEXT_POLBEZ_1";

    /// <summary>GKZ, unter der Wien als Ganzes geführt wird; die Geodaten kennen nur die 23 Bezirke.</summary>
    public const string GkzWien = "90001";

    public async Task<(int Anzahl, DateOnly Gebietsstand)> ImportiereAsync(
        string datenbankPfad, Action<string>? melde = null, CancellationToken ct = default)
    {
        var (gemeindenUrl, gebietsstand) = await NeuesteAusgabeAsync(MetadatenGemeinden, ct);
        var (bezirkeUrl, _) = await NeuesteAusgabeAsync(MetadatenBezirke, ct);

        melde?.Invoke($"Lade Bezirke: {bezirkeUrl}");
        Dictionary<string, string> bezirke;
        using (var dokument = await LadeJsonAsync(bezirkeUrl, ct))
        {
            bezirke = LiesBezirke(dokument.RootElement);
        }

        melde?.Invoke($"Lade Gemeinden (Gebietsstand {gebietsstand:yyyy-MM-dd}): {gemeindenUrl}");
        List<Gemeinde> gemeinden;
        using (var dokument = await LadeJsonAsync(gemeindenUrl, ct))
        {
            gemeinden = LiesGemeinden(dokument.RootElement, bezirke);
        }

        gemeinden = MitWienGesamt(gemeinden);
        GemeindeDatenbank.Speichere(datenbankPfad, gemeinden, gebietsstand, DateTimeOffset.UtcNow);
        melde?.Invoke($"{gemeinden.Count} Gemeinden gespeichert in {datenbankPfad}");
        return (gemeinden.Count, gebietsstand);
    }

    /// <summary>GeoJSON-FeatureCollection mit den Attributen g_id (GKZ) und g_name → Gemeinden.</summary>
    public static List<Gemeinde> LiesGemeinden(JsonElement featureCollection, IReadOnlyDictionary<string, string> bezirke)
    {
        var gemeinden = new List<Gemeinde>();
        foreach (var feature in featureCollection.GetProperty("features").EnumerateArray())
        {
            var eigenschaften = feature.GetProperty("properties");
            var gkz = eigenschaften.GetProperty("g_id").GetString()!;
            var name = eigenschaften.GetProperty("g_name").GetString()!;
            var flaeche = Flaeche.AusGeoJson(feature.GetProperty("geometry"));
            var bezirk = gkz.Length >= 3 && bezirke.TryGetValue(gkz[..3], out var b) ? b : "";
            gemeinden.Add(new Gemeinde(gkz, name, bezirk, Bundeslaender.AusGkz(gkz), flaeche.Mittelpunkt(), flaeche));
        }

        return gemeinden;
    }

    /// <summary>Bezirkskennziffer (dreistellig) → Name, mit geglätteter Schreibweise ("Eisenstadt (Stadt)").</summary>
    public static Dictionary<string, string> LiesBezirke(JsonElement featureCollection)
    {
        var bezirke = new Dictionary<string, string>();
        foreach (var feature in featureCollection.GetProperty("features").EnumerateArray())
        {
            var eigenschaften = feature.GetProperty("properties");
            var name = eigenschaften.GetProperty("g_name").GetString()!;
            name = KlammerOhneAbstand().Replace(name, " (");
            name = KommaOhneAbstand().Replace(name, ", ");
            bezirke[eigenschaften.GetProperty("g_id").GetString()!] = name;
        }

        return bezirke;
    }

    /// <summary>
    /// Ergänzt "Wien" (GKZ 90001) als Zusammenfassung der 23 Bezirke, damit die Stadt als Ganzes
    /// auffindbar ist. Mittelpunkt ist jener der Inneren Stadt.
    /// </summary>
    public static List<Gemeinde> MitWienGesamt(List<Gemeinde> gemeinden)
    {
        var bezirke = gemeinden.Where(g => g.Gkz.StartsWith('9') && g.Flaeche is not null).ToList();
        if (bezirke.Count == 0 || gemeinden.Any(g => g.Gkz == GkzWien))
        {
            return gemeinden;
        }

        var flaeche = new Flaeche(bezirke.SelectMany(g => g.Flaeche!.Polygone).ToList());
        var mitte = bezirke.MinBy(g => g.Gkz)!.Mittelpunkt;
        return [.. gemeinden, new Gemeinde(GkzWien, "Wien", "Wien (Stadt)", "Wien", mitte, flaeche)];
    }

    // Die erste Ressource der OGD-Metadaten ist die jüngste Ausgabe. Aus ihrer WFS-Adresse wird der
    // Layername übernommen und derselbe Layer als GeoJSON in WGS84 angefordert.
    private async Task<(string Url, DateOnly Gebietsstand)> NeuesteAusgabeAsync(string metadatenUrl, CancellationToken ct)
    {
        using var dokument = await LadeJsonAsync(metadatenUrl, ct);
        var ressource = dokument.RootElement.GetProperty("resources")[0].GetProperty("url").GetString()!;
        var layer = Layername().Match(ressource);
        if (!layer.Success)
        {
            throw new FormatException($"Kein Layername in der Ressource gefunden: {ressource}");
        }

        var basis = ressource[..ressource.IndexOf('?')];
        var url = $"{basis}?service=WFS&version=1.0.0&request=GetFeature&typeName={layer.Groups[1].Value}"
                + "&outputFormat=application/json&srsName=EPSG:4326";
        var stand = DateOnly.ParseExact(layer.Groups[2].Value, "yyyyMMdd", CultureInfo.InvariantCulture);
        return (url, stand);
    }

    private async Task<JsonDocument> LadeJsonAsync(string url, CancellationToken ct)
    {
        using var antwort = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        antwort.EnsureSuccessStatusCode();
        await using var strom = await antwort.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(strom, cancellationToken: ct);
    }

    [GeneratedRegex(@"typeName=([^&]*_(\d{8}))(&|$)")]
    private static partial Regex Layername();

    [GeneratedRegex(@"(?<=\S)\(")]
    private static partial Regex KlammerOhneAbstand();

    [GeneratedRegex(@",(?=\S)")]
    private static partial Regex KommaOhneAbstand();
}
