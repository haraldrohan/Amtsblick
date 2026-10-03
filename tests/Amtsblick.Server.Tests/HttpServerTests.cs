using System.Net;
using System.Text;
using System.Text.Json;
using Amtsblick.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace Amtsblick.Server.Tests;

/// <summary>Der gehostete Server über Streamable HTTP, im Speicher gestartet und ohne Netz.</summary>
public sealed class HttpServerTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _app = ServerRegistrierung.ErzeugeHttpApp([], builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Amtsblick:AutoImport"] = "false",
                ["Amtsblick:DatenVerzeichnis"] = Path.Combine(Path.GetTempPath(), $"amtsblick-test-{Guid.NewGuid():N}"),
                ["Amtsblick:Http:AnfragenProMinute"] = "5",
                ["AllowedHosts"] = "*",
            });
        });
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Alle_acht_Tools_haben_Titel_und_die_verlangten_Annotations()
    {
        var tools = (await Rpc("tools/list")).GetProperty("result").GetProperty("tools").EnumerateArray().ToList();

        Assert.Equal(
            new[]
            {
                "hochwasserlage", "lage_am_ort", "niederschlag_jetzt", "ort_finden",
                "pegel_an_gewaesser", "pegel_in_der_naehe", "quellen", "wetter_prognose",
            },
            tools.Select(t => t.GetProperty("name").GetString()).Order());
        Assert.All(tools, tool =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.GetProperty("title").GetString()));
            var annotations = tool.GetProperty("annotations");
            Assert.True(annotations.GetProperty("readOnlyHint").GetBoolean());
            Assert.False(annotations.GetProperty("destructiveHint").GetBoolean());
            Assert.True(annotations.GetProperty("openWorldHint").GetBoolean());
            Assert.Contains("Beispielfragen", tool.GetProperty("description").GetString());
        });
        Assert.All(
            tools.Where(t => t.GetProperty("name").GetString() != "quellen"),
            tool => Assert.Contains("Nenne in deiner Antwort die Datenquelle", tool.GetProperty("description").GetString()));
    }

    [Fact]
    public async Task Server_Info_nennt_Name_Version_Beschreibung_Repo_und_den_Pflichthinweis()
    {
        var antwort = await Rpc("initialize", """{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}""");

        var info = antwort.GetProperty("result").GetProperty("serverInfo");
        Assert.Equal("amtsblick", info.GetProperty("name").GetString());
        Assert.Equal("Amtsblick", info.GetProperty("title").GetString());
        Assert.Matches(@"^\d+\.\d+\.\d+", info.GetProperty("version").GetString());
        Assert.Contains(ToolAntwort.Pflichthinweis, info.GetProperty("description").GetString());
        Assert.Equal("https://github.com/haraldrohan/Amtsblick", info.GetProperty("websiteUrl").GetString());
        Assert.Contains(ToolAntwort.Pflichthinweis, antwort.GetProperty("result").GetProperty("instructions").GetString());
    }

    [Fact]
    public async Task Tool_Antwort_traegt_den_Quellenvermerk_in_der_Zusammenfassung()
    {
        var antwort = await Rpc("tools/call", """{"name":"ort_finden","arguments":{"text":"Steyr"}}""");

        var text = antwort.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;
        using var dokument = JsonDocument.Parse(text);
        Assert.EndsWith(
            "Datenquelle: Statistik Austria — data.statistik.gv.at (CC BY 4.0, https://creativecommons.org/licenses/by/4.0/deed.de; Daten aufbereitet)",
            dokument.RootElement.GetProperty("zusammenfassung").GetString());
    }

    [Fact]
    public async Task Health_nennt_Stand_je_Quelle_und_loest_keinen_Abruf_aus()
    {
        var antwort = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        using var dokument = JsonDocument.Parse(await antwort.Content.ReadAsStringAsync());
        var wurzel = dokument.RootElement;
        Assert.Equal("eingeschränkt", wurzel.GetProperty("status").GetString());
        Assert.Equal(0, wurzel.GetProperty("gemeinden").GetInt32());
        Assert.Equal(
            new[] { "OGDEXT_GEM_1, OGDEXT_POLBEZ_1", "nwp-v2-1h-1km", "nowcast-v1-15min-1km", "i000501:pegel_aktuell" },
            wurzel.GetProperty("quellen").EnumerateArray().Select(q => q.GetProperty("datensatz").GetString()));
        Assert.False(wurzel.TryGetProperty("geosphere_kontingent", out _));
    }

    [Theory]
    [InlineData("https://boese.example", HttpStatusCode.Forbidden)]
    [InlineData("https://claude.ai.boese.example", HttpStatusCode.Forbidden)]
    [InlineData("https://claude.ai", HttpStatusCode.OK)]
    [InlineData("http://localhost:6274", HttpStatusCode.OK)]
    [InlineData(null, HttpStatusCode.OK)]
    public async Task Anfragen_von_nicht_erlaubten_Urspruengen_werden_abgelehnt(string? origin, HttpStatusCode erwartet)
    {
        using var anfrage = new HttpRequestMessage(HttpMethod.Get, "/health");
        if (origin is not null)
        {
            anfrage.Headers.Add("Origin", origin);
        }

        Assert.Equal(erwartet, (await _client.SendAsync(anfrage)).StatusCode);
    }

    [Fact]
    public async Task Rate_Limit_begrenzt_MCP_Anfragen_je_Client_und_laesst_Health_frei()
    {
        var status = new List<HttpStatusCode>();
        for (var i = 0; i < 7; i++)
        {
            status.Add((await Sende("tools/list")).StatusCode);
        }

        Assert.Equal(5, status.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(2, status.Count(s => s == HttpStatusCode.TooManyRequests));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);
    }

    [Theory]
    [InlineData("https://claude.ai", true)]
    [InlineData("https://CLAUDE.ai/", true)]
    [InlineData("http://claude.ai", false)]
    [InlineData("http://127.0.0.1:3000", true)]
    [InlineData("http://[::1]:3000", true)]
    [InlineData("null", false)]
    [InlineData("kein-ursprung", false)]
    public void Ursprung_wird_nach_Schema_und_Host_geprueft(string ursprung, bool erlaubt)
    {
        Assert.Equal(erlaubt, HttpSchutz.UrsprungErlaubt(ursprung, ["https://claude.ai"]));
    }

    private Task<HttpResponseMessage> Sende(string methode, string parameter = "{}")
    {
        var anfrage = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                $$"""{"jsonrpc":"2.0","id":1,"method":"{{methode}}","params":{{parameter}}}""", Encoding.UTF8, "application/json"),
        };
        anfrage.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
        return _client.SendAsync(anfrage);
    }

    // Die Antwort kommt als JSON oder als Server-Sent Event mit einer data-Zeile.
    private async Task<JsonElement> Rpc(string methode, string parameter = "{}")
    {
        var antwort = await Sende(methode, parameter);
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        var text = await antwort.Content.ReadAsStringAsync();
        var json = text.Split('\n').FirstOrDefault(z => z.StartsWith("data: ", StringComparison.Ordinal))?[6..] ?? text;
        using var dokument = JsonDocument.Parse(json);
        return dokument.RootElement.Clone();
    }
}

public sealed class SeitenTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _app = ServerRegistrierung.ErzeugeHttpApp([], builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Amtsblick:AutoImport"] = "false",
                ["Amtsblick:DatenVerzeichnis"] = Path.Combine(Path.GetTempPath(), $"amtsblick-test-{Guid.NewGuid():N}"),
                ["Amtsblick:Http:Betreiber"] = "Erika Muster, Beispielgasse 1, 4400 Steyr <test@example.org>",
                ["AllowedHosts"] = "*",
            });
        });
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Startseite_nennt_Pflichthinweis_Repo_Einbindung_und_Datenschutz()
    {
        var antwort = await _client.GetAsync("/");
        var html = await antwort.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal("text/html", antwort.Content.Headers.ContentType?.MediaType);
        Assert.Contains(ToolAntwort.Pflichthinweis, WebUtility.HtmlDecode(html));
        Assert.Contains("href=\"https://github.com/haraldrohan/Amtsblick\"", html);
        Assert.Contains("href=\"https://github.com/haraldrohan/Amtsblick#einbinden\"", html);
        Assert.Contains("href=\"/datenschutz\"", html);
        Assert.Contains("<code>/mcp</code>", html);
        Assert.Contains("Betreiber und Medieninhaber: Erika Muster, Beispielgasse 1, 4400 Steyr &lt;test@example.org&gt;", html);
        Assert.DoesNotContain("<script", html);
        Assert.False(antwort.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Datenschutz_liefert_die_Erklaerung_aus_dem_Repository_deutsch_und_englisch()
    {
        var antwort = await _client.GetAsync("/datenschutz");
        var html = WebUtility.HtmlDecode(await antwort.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal("text/html", antwort.Content.Headers.ContentType?.MediaType);
        Assert.Contains(">Datenschutzerklärung</h1>", html);
        Assert.Contains("<h1 id=\"privacy-policy\">Privacy policy</h1>", html);
        Assert.Contains(ToolAntwort.Pflichthinweis, html);
        Assert.Contains("<table>", html);
        Assert.Contains("Austria East", html);
        Assert.False(antwort.Headers.Contains("Set-Cookie"));

        // Dieselbe Fassung wie die Datei im Repository.
        var datei = File.ReadAllText(Path.Combine(Wurzel(), "DATENSCHUTZ.md"));
        Assert.Contains("höchstens einmal pro Stunde der Abruf des gesamten Pegelbestands", datei);
        Assert.Contains("höchstens einmal pro Stunde der Abruf des gesamten Pegelbestands", html);
    }

    private static string Wurzel()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !File.Exists(Path.Combine(ordner.FullName, "Amtsblick.sln")))
        {
            ordner = ordner.Parent;
        }

        return ordner?.FullName ?? throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
    }
}
