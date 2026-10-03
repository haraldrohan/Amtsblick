using Amtsblick.Core;
using Amtsblick.Core.Http;
using Amtsblick.Core.Ort;
using Amtsblick.Modules.Wasser;
using Amtsblick.Modules.Wetter;
using Amtsblick.Server;
using ModelContextProtocol.AspNetCore;

// Aufruf:
//   Amtsblick.Server            MCP über Streamable HTTP (Endpunkt /mcp)
//   Amtsblick.Server --stdio    MCP über stdio, z. B. für Claude Desktop
//   Amtsblick.Server import     Gemeinden der Statistik Austria nach data/gemeinden.sqlite laden

if (args is ["import", ..])
{
    return await ImportiereAsync();
}

if (args.Contains("--stdio"))
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = [.. args.Where(a => a != "--stdio")],
        ContentRootPath = AppContext.BaseDirectory,
    });

    // stdout gehört dem Protokoll; alle Logs gehen nach stderr.
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Services.AddAmtsblick(builder.Configuration).WithStdioServerTransport();
    await builder.Build().RunAsync();
}
else
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });
    builder.Services.AddAmtsblick(builder.Configuration)
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless);

    var app = builder.Build();
    app.MapMcp("/mcp");
    app.MapGet("/", () => $"Amtsblick {UserAgent.Version} – MCP-Endpunkt (Streamable HTTP): /mcp");
    await app.RunAsync();
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
        Console.Error.WriteLine("Datenquelle: Statistik Austria — data.statistik.gv.at (CC BY 4.0)");
        return 0;
    }
    catch (Exception fehler) when (fehler is HttpRequestException or TaskCanceledException or FormatException or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"Import fehlgeschlagen: {fehler.Message}");
        return 1;
    }
}
