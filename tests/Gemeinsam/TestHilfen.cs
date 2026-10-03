using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Amtsblick.Core.Ort;

namespace Amtsblick.Tests;

/// <summary>Ersetzt das Netz: zeichnet jede Anfrage auf und antwortet aus einer Funktion.</summary>
public sealed class AufzeichnenderHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> antworte) : HttpMessageHandler
{
    public AufzeichnenderHandler(Func<HttpRequestMessage, HttpResponseMessage> antworte)
        : this(anfrage => Task.FromResult(antworte(anfrage)))
    {
    }

    public ConcurrentQueue<string> Anfragen { get; } = new();

    public ConcurrentQueue<string> UserAgents { get; } = new();

    public int Anzahl(string teil) => Anfragen.Count(a => a.Contains(teil, StringComparison.Ordinal));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Anfragen.Enqueue(request.RequestUri!.ToString());
        UserAgents.Enqueue(request.Headers.TryGetValues("User-Agent", out var werte) ? string.Join(' ', werte) : "");
        return antworte(request);
    }
}

public sealed class FesteFabrik(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

public static class Fixture
{
    public static string Text(string datei) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", datei), Encoding.UTF8);

    public static JsonElement Json(string datei)
    {
        using var dokument = JsonDocument.Parse(Text(datei));
        return dokument.RootElement.Clone();
    }

    public static HttpResponseMessage Antwort(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

public static class Testgemeinden
{
    private static readonly Dictionary<string, string> Bezirke = new()
    {
        ["402"] = "Stadt Steyr",
        ["415"] = "Steyr-Land",
    };

    /// <summary>Steyr, St. Ulrich bei Steyr und Garsten mit den echten Grenzen der Statistik Austria.</summary>
    public static List<Gemeinde> MitGrenzen() =>
        GemeindeImport.LiesGemeinden(Fixture.Json("gemeinden-steyr.geojson"), Bezirke);

    /// <summary>Die drei Gemeinden mit Grenzen und weitere nur mit Namen, für die Namenssuche.</summary>
    public static GemeindeVerzeichnis Verzeichnis() => new(
        [
            .. MitGrenzen(),
            Nur("41624", "Steyregg", "Urfahr-Umgebung"),
            Nur("41502", "Aschach an der Steyr", "Steyr-Land"),
            Nur("40920", "Steinbach an der Steyr", "Kirchdorf"),
            Nur("30201", "St. Pölten", "St. Pölten (Stadt)"),
            Nur("50418", "St. Johann im Pongau", "St. Johann im Pongau"),
            Nur("70416", "St. Johann in Tirol", "Kitzbühel"),
            Nur("70725", "St. Johann im Walde", "Lienz"),
            Nur("40436", "St. Johann am Walde", "Braunau"),
            Nur("41331", "St. Johann am Wimberg", "Rohrbach"),
            Nur("61032", "Sankt Johann im Saggautal", "Leibnitz"),
            Nur("62244", "Sankt Johann in der Haide", "Hartberg-Fürstenfeld"),
            Nur("61633", "Söding-Sankt Johann", "Voitsberg"),
        ],
        new DateOnly(2026, 1, 1));

    private static Gemeinde Nur(string gkz, string name, string bezirk) =>
        new(gkz, name, bezirk, Bundeslaender.AusGkz(gkz), new Koordinate(47.5, 13.5));
}
