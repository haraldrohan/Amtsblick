using Amtsblick.Core;
using Amtsblick.Core.Http;
using Amtsblick.Modules.Wasser;
using Amtsblick.Modules.Wetter;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

namespace Amtsblick.Server;

public static class ServerRegistrierung
{
    public const string Kurzbeschreibung =
        "Amtliche österreichische Daten nach Ort: Wetter (GeoSphere Austria) und Pegel (eHYD). " + ToolAntwort.Pflichthinweis;

    /// <summary>
    /// Kern und alle Module, die in der Konfiguration nicht abgeschaltet sind
    /// (<c>Amtsblick:Module:&lt;Name&gt;:Aktiv</c>, Standard an). Ein abgeschaltetes Modul wird nicht
    /// registriert: seine Tools erscheinen nicht und es ruft nichts ab.
    /// </summary>
    public static IMcpServerBuilder AddAmtsblick(this IServiceCollection services, IConfiguration konfiguration)
    {
        services.AddAmtsblickKern(konfiguration);
        var mcp = services
            .AddMcpServer(o =>
            {
                o.ServerInfo = new Implementation
                {
                    Name = "amtsblick",
                    Title = "Amtsblick",
                    Version = UserAgent.Version,
                    Description = Kurzbeschreibung,
                    WebsiteUrl = "https://github.com/haraldrohan/Amtsblick",
                };
                o.ServerInstructions =
                    ToolAntwort.Pflichthinweis
                    + " Amtliche österreichische Daten nach Ort: Wetter (GeoSphere Austria) und Pegel (eHYD). "
                    + "Orte sind Gemeinden; bei mehrdeutigen Namen zuerst ort_finden nutzen. "
                    + "Jede Antwort endet in der Zusammenfassung mit dem Quellenvermerk der Datengeber. Gib ihn am Ende "
                    + "deiner Antwort wörtlich wieder, mit Lizenz und Lizenzlink, und formuliere ihn nicht um; die Lizenz "
                    + "CC BY 4.0 verlangt das. Gib auch die Hinweise vollständig weiter, etwa den Verweis auf die "
                    + "Warndienste des Landes. "
                    + "Die Daten ersetzen keine amtlichen Warnungen.";
            })
            .WithTools<KernTools>();

        if (konfiguration.GetValue("Amtsblick:Module:Wetter:Aktiv", true))
        {
            services.AddWetterModul();
            mcp.WithTools<WetterTools>().WithTools<LageTools>();
        }

        if (konfiguration.GetValue(WasserOptionen.Abschnitt + ":Aktiv", true))
        {
            services.AddWasserModul(konfiguration);
            mcp.WithTools<WasserTools>();
        }

        return mcp;
    }

    /// <summary>Der Server für Streamable HTTP mit Schutzschicht, MCP-Endpunkt /mcp und /health.</summary>
    public static WebApplication ErzeugeHttpApp(string[] args, Action<WebApplicationBuilder>? anpassen = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        anpassen?.Invoke(builder);
        builder.Services.AddAmtsblick(builder.Configuration)
            .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless);
        builder.Services.AddAmtsblickHttpSchutz(builder.Configuration);

        var app = builder.Build();
        app.UseAmtsblickHttpSchutz();
        app.MapMcp("/mcp").RequireRateLimiting(HttpSchutz.Richtlinie);
        app.MapGet("/health", Gesundheit.Antwort);
        app.MapGet("/", Seiten.Start);
        app.MapGet("/datenschutz", Seiten.Datenschutz);
        return app;
    }
}
