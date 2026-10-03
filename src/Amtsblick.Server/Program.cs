using Amtsblick.Core;
using Amtsblick.Core.Http;
using Amtsblick.Core.Ort;
using Amtsblick.Core.Quellen;
using Amtsblick.Server;

// Aufruf:
//   amtsblick              MCP über stdio (Standard; so starten Clients den Server)
//   amtsblick --http       MCP über Streamable HTTP (Endpunkt /mcp), für den gehosteten Betrieb
//   amtsblick import       Gemeinden der Statistik Austria laden und in SQLite ablegen
// "--stdio" wird weiterhin angenommen, ist aber nicht mehr nötig.

if (args is ["import", ..])
{
    return await ImportiereAsync();
}

var http = args.Contains("--http");
string[] rest = [.. args.Where(a => a is not ("--http" or "--stdio"))];

if (!http)
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = rest,
        ContentRootPath = AppContext.BaseDirectory,
    });

    // stdout gehört dem Protokoll; alle Logs gehen nach stderr.
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Services.AddAmtsblick(builder.Configuration).WithStdioServerTransport();
    await builder.Build().RunAsync();
}
else
{
    await ServerRegistrierung.ErzeugeHttpApp(rest).RunAsync();
}

return 0;

static async Task<int> ImportiereAsync()
{
    var konfiguration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .Build();
    var optionen = konfiguration.GetSection(AmtsblickOptionen.Abschnitt).Get<AmtsblickOptionen>() ?? new AmtsblickOptionen();

    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent.Erzeuge(optionen));
    var ziel = Path.Combine(optionen.ErmittleDatenVerzeichnis(), GemeindeDatenbank.Dateiname);
    try
    {
        await new GemeindeImport(http).ImportiereAsync(ziel, Console.Error.WriteLine);
        Console.Error.WriteLine($"{StatistikAustriaQuelle.Namensnennung} (Lizenz: {Quellenvermerk.CcBy40}, {Quellenvermerk.CcBy40Link})");
        return 0;
    }
    catch (Exception fehler) when (fehler is HttpRequestException or TaskCanceledException or FormatException or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"Import fehlgeschlagen: {fehler.Message}");
        return 1;
    }
}
