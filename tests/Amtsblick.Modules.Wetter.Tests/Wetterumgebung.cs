using System.Net;
using System.Text.Json;
using Amtsblick.Core;
using Amtsblick.Core.Http;
using Amtsblick.Core.Kontingent;
using Amtsblick.Core.Ort;
using Amtsblick.Core.Quellen;
using Amtsblick.Modules.Wetter.GeoSphere;
using Amtsblick.Tests;
using Microsoft.Extensions.Time.Testing;

namespace Amtsblick.Modules.Wetter.Tests;

/// <summary>
/// Wetter-Modul mit gespeicherten GeoSphere-Antworten statt Netz. Die Uhr steht auf dem Zeitpunkt,
/// zu dem die Fixtures geholt wurden (2. Oktober 2026, 19:05 UTC = 21:05 Ortszeit).
/// </summary>
public sealed class Wetterumgebung
{
    public const string PrognoseDaten = "nwp-v2-1h-1km?";
    public const string PrognoseMetadaten = "nwp-v2-1h-1km/metadata";
    public const string NowcastDaten = "nowcast-v1-15min-1km?";
    public const string NowcastMetadaten = "nowcast-v1-15min-1km/metadata";

    public Wetterumgebung()
    {
        Netz = new AufzeichnenderHandler(async anfrage =>
        {
            var url = anfrage.RequestUri!.ToString();
            if (NowcastSperre is { } sperre && url.Contains(NowcastDaten))
            {
                await sperre.Task;
            }

            if (MetadatenSperre is { } metadatenSperre && url.EndsWith("/metadata", StringComparison.Ordinal))
            {
                await metadatenSperre.Task;
            }

            return Antworte(anfrage);
        });
        Kontingent = new KontingentRegister(Zeit);
        var takt = new Anfragetakt(Zeit, proSekunde: 100);
        var client = new GeoSphereClient(
            new FesteFabrik(new KontingentHandler(Kontingent, GeoSphereClient.Quelle) { InnerHandler = Netz }), takt);
        var verzeichnis = Testgemeinden.Verzeichnis();
        Dienst = new WetterDienst(client, Kontingent, takt, Zeit);
        Auskunft = new WetterAuskunft(new OrtResolver(verzeichnis), Dienst, new StatistikAustriaQuelle(verzeichnis), Zeit);
    }

    public FakeTimeProvider Zeit { get; } = new(new DateTimeOffset(2026, 10, 2, 19, 5, 0, TimeSpan.Zero));
    public AufzeichnenderHandler Netz { get; }
    public KontingentRegister Kontingent { get; }
    public WetterDienst Dienst { get; }
    public WetterAuskunft Auskunft { get; }

    /// <summary>Wert des Headers x-ratelimit-remaining-hour in jeder Antwort; null lässt ihn weg.</summary>
    public int? RestStunde { get; set; }

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public HttpStatusCode MetadatenStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>Hält die Antwort auf den Nowcast-Abruf zurück, bis die Aufgabe abgeschlossen wird.</summary>
    public TaskCompletionSource? NowcastSperre { get; set; }

    /// <summary>Hält die Antwort auf Metadaten-Abrufe zurück, bis die Aufgabe abgeschlossen wird.</summary>
    public TaskCompletionSource? MetadatenSperre { get; set; }

    /// <summary>Ersetzt last_forecast_reftime in den Prognose-Metadaten.</summary>
    public string? PrognoseLauf { get; set; }

    public static JsonElement Json(Teilantwort antwort)
    {
        using var dokument = JsonDocument.Parse(antwort.AlsJson());
        return dokument.RootElement.Clone();
    }

    public static List<string> Hinweise(JsonElement antwort) =>
        antwort.GetProperty("hinweise").EnumerateArray().Select(h => h.GetString()!).ToList();

    private HttpResponseMessage Antworte(HttpRequestMessage anfrage)
    {
        var url = anfrage.RequestUri!.ToString();
        var metadaten = url.EndsWith("/metadata", StringComparison.Ordinal);
        var status = metadaten ? MetadatenStatus : Status;
        if (status != HttpStatusCode.OK)
        {
            return new HttpResponseMessage(status);
        }

        var json = url switch
        {
            _ when url.Contains(PrognoseMetadaten) => PrognoseLauf is null
                ? Fixture.Text("nwp-v2-1h-1km.metadata.json")
                : Fixture.Text("nwp-v2-1h-1km.metadata.json")
                    .Replace("\"last_forecast_reftime\":\"2026-10-02T12:00+00:00\"", $"\"last_forecast_reftime\":\"{PrognoseLauf}\""),
            _ when url.Contains(NowcastMetadaten) => Fixture.Text("nowcast-v1-15min-1km.metadata.json"),
            _ when url.Contains(PrognoseDaten) => Fixture.Text("nwp-v2-1h-1km.steyr.json"),
            _ when url.Contains(NowcastDaten) => Fixture.Text("nowcast-v1-15min-1km.steyr.json"),
            _ => throw new InvalidOperationException($"Unerwartete Anfrage: {url}"),
        };

        var antwort = Fixture.Antwort(json);
        if (RestStunde is { } rest)
        {
            antwort.Headers.Add("x-ratelimit-remaining-hour", rest.ToString());
            antwort.Headers.Add("x-ratelimit-limit-hour", "240");
            antwort.Headers.Add("x-ratelimit-remaining-second", "4");
        }

        return antwort;
    }
}
