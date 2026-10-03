using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amtsblick.Core;
using Amtsblick.Core.Ort;
using Amtsblick.Core.Quellen;
using Amtsblick.Modules.Wasser.Ehyd;
using Amtsblick.Tests;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Amtsblick.Modules.Wasser.Tests;

/// <summary>
/// Wasser-Modul mit den gespeicherten eHYD-Antworten statt Netz. Die Uhr steht auf dem Zeitpunkt,
/// zu dem die Fixture geholt wurde (2. Oktober 2026, 19:07 UTC = 21:07 Ortszeit).
/// </summary>
public sealed class Wasserumgebung
{
    public const string Collections = "/collections?f=json";
    public const string Items = "/collections/i000501:pegel_aktuell/items?";
    public const string Wfs = "/i000501/wfs?";
    public const int SteyrOrtskai = 205922;
    public const int Jaegerberg = 205757;

    public Wasserumgebung(string kontakt = "betrieb@example.org", Func<bool>? istAktiv = null)
    {
        Netz = new AufzeichnenderHandler(AntworteAsync);
        var verzeichnis = new GemeindeVerzeichnis(Testgemeinden.MitGrenzen(), new DateOnly(2026, 1, 1));
        Dienst = new PegelDienst(
            new EhydClient(new FesteFabrik(Netz)),
            verzeichnis,
            Options.Create(new AmtsblickOptionen { Kontakt = kontakt }),
            Zeit,
            istAktiv);
        Auskunft = new PegelAuskunft(Dienst, new OrtResolver(verzeichnis), new StatistikAustriaQuelle(verzeichnis));
    }

    public FakeTimeProvider Zeit { get; } = new(new DateTimeOffset(2026, 10, 2, 19, 7, 0, TimeSpan.Zero));
    public AufzeichnenderHandler Netz { get; }
    public PegelDienst Dienst { get; }
    public PegelAuskunft Auskunft { get; }

    public HttpStatusCode OgcStatus { get; set; } = HttpStatusCode.OK;
    public HttpStatusCode WfsStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>Hält die Antwort auf den Bestandsabruf zurück, bis die Aufgabe abgeschlossen wird.</summary>
    public TaskCompletionSource? Sperre { get; set; }

    /// <summary>gesamtcode je HZB-Nummer, der in der Fixture ersetzt wird.</summary>
    public Dictionary<int, int> Codes { get; } = [];

    public static JsonElement Json(Teilantwort antwort)
    {
        using var dokument = JsonDocument.Parse(antwort.AlsJson());
        return dokument.RootElement.Clone();
    }

    public static List<string> Hinweise(JsonElement antwort) =>
        antwort.GetProperty("hinweise").EnumerateArray().Select(h => h.GetString()!).ToList();

    public string Bestand()
    {
        var json = Fixture.Text("pegel_aktuell.items.json");
        foreach (var (hzbnr, code) in Codes)
        {
            json = Regex.Replace(json, $"(\"hzbnr\":{hzbnr},[^}}]*?\"gesamtcode\":)\\d+", "${1}" + code);
        }

        return json;
    }

    private async Task<HttpResponseMessage> AntworteAsync(HttpRequestMessage anfrage)
    {
        var url = anfrage.RequestUri!.ToString();
        if (url.Contains(Wfs))
        {
            return WfsStatus == HttpStatusCode.OK ? Fixture.Antwort(Bestand()) : new HttpResponseMessage(WfsStatus);
        }

        if (OgcStatus != HttpStatusCode.OK)
        {
            return new HttpResponseMessage(OgcStatus);
        }

        if (url.EndsWith(Collections, StringComparison.Ordinal))
        {
            return Fixture.Antwort(Fixture.Text("collections.json"));
        }

        if (url.Contains(Items))
        {
            if (Sperre is { } sperre)
            {
                await sperre.Task;
            }

            return Fixture.Antwort(Bestand());
        }

        throw new InvalidOperationException($"Unerwartete Anfrage: {url}");
    }
}
