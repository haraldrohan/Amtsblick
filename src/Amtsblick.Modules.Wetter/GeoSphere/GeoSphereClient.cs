using System.Text.Json;
using Amtsblick.Core.Kontingent;
using Amtsblick.Core.Ort;

namespace Amtsblick.Modules.Wetter.GeoSphere;

/// <summary>HTTP-Zugriff auf die GeoSphere Dataset API. Jede Anfrage läuft durch den <see cref="Anfragetakt"/>.</summary>
public sealed class GeoSphereClient(IHttpClientFactory fabrik, Anfragetakt takt)
{
    /// <summary>Name des HttpClients und zugleich der Quelle im Kontingent-Register.</summary>
    public const string Quelle = "geosphere";

    public const string Basis = "https://dataset.api.hub.geosphere.at/v1/";

    public Anfragetakt Takt => takt;

    public async Task<Metadaten> HoleMetadatenAsync(Ressource ressource, CancellationToken ct)
    {
        using var dokument = await HoleAsync($"{Basis}{ressource.Pfad}/metadata", ct);
        return GeoSphereParser.LiesMetadaten(dokument.RootElement);
    }

    public async Task<Zeitreihe> HoleZeitreiheAsync(Ressource ressource, Koordinate punkt, CancellationToken ct)
    {
        var url = $"{Basis}{ressource.Pfad}?lat_lon={punkt}&parameters={string.Join(',', ressource.Parameter)}&output_format=geojson";
        using var dokument = await HoleAsync(url, ct);
        return GeoSphereParser.LiesZeitreihe(dokument.RootElement);
    }

    private async Task<JsonDocument> HoleAsync(string url, CancellationToken ct)
    {
        await takt.WarteAsync(ct);
        using var http = fabrik.CreateClient(Quelle);
        using var antwort = await http.GetAsync(url, ct);
        antwort.EnsureSuccessStatusCode();
        await using var strom = await antwort.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(strom, cancellationToken: ct);
    }
}
