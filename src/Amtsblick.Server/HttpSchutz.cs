using System.Diagnostics;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Amtsblick.Server;

/// <summary>Konfigurationsabschnitt "Amtsblick:Http" für den Betrieb über Streamable HTTP.</summary>
public sealed class HttpOptionen
{
    public const string Abschnitt = "Amtsblick:Http";

    /// <summary>Höchstzahl der MCP-Anfragen je Client-IP und Minute; darüber antwortet der Server mit 429.</summary>
    public int AnfragenProMinute { get; set; } = 60;

    /// <summary>
    /// Ursprünge (Schema und Host, z. B. "https://claude.ai"), deren Browser-Anfragen angenommen werden.
    /// Anfragen ohne Origin-Header (Server zu Server, Kommandozeile) und von localhost sind immer erlaubt.
    /// </summary>
    public string[] ErlaubteUrspruenge { get; set; } = ["https://claude.ai", "https://claude.com"];

    /// <summary>
    /// An: der Server steht hinter genau einem Reverse Proxy und nimmt die Client-IP für das Rate-Limit
    /// aus X-Forwarded-For. Aus (Standard): es gilt die Adresse der Verbindung.
    /// </summary>
    public bool HinterProxy { get; set; }

    /// <summary>
    /// Name, Anschrift und Kontakt des Betreibers dieser Instanz für die Offenlegung auf der Startseite.
    /// Leer: die Startseite nennt keinen Betreiber.
    /// </summary>
    public string Betreiber { get; set; } = "";
}

/// <summary>
/// Schutz des HTTP-Transports nach den Sicherheitsvorgaben der MCP-Spezifikation: Origin-Prüfung gegen
/// DNS-Rebinding, Rate-Limit je Client und Logs ohne personenbezogene Angaben. Der Server ist ohne
/// Anmeldung nutzbar, weil er nur öffentliche Daten liest.
/// </summary>
public static class HttpSchutz
{
    public const string Richtlinie = "mcp";

    public static IServiceCollection AddAmtsblickHttpSchutz(this IServiceCollection services, IConfiguration konfiguration)
    {
        services.Configure<HttpOptionen>(konfiguration.GetSection(HttpOptionen.Abschnitt));
        var optionen = konfiguration.GetSection(HttpOptionen.Abschnitt).Get<HttpOptionen>() ?? new HttpOptionen();

        if (optionen.HinterProxy)
        {
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.ForwardLimit = 1;

                // Die Adresse des Proxys ist beim Hosting-Anbieter nicht fest; vertraut wird dem letzten Hop.
                o.KnownIPNetworks.Clear();
                o.KnownProxies.Clear();
            });
        }

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(Richtlinie, kontext => RateLimitPartition.GetFixedWindowLimiter(
                kontext.Connection.RemoteIpAddress?.ToString() ?? "unbekannt",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, optionen.AnfragenProMinute),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
        return services;
    }

    public static WebApplication UseAmtsblickHttpSchutz(this WebApplication app)
    {
        var optionen = app.Services.GetRequiredService<IOptions<HttpOptionen>>().Value;
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Amtsblick.Http");
        if (optionen.HinterProxy)
        {
            app.UseForwardedHeaders();
        }

        // Technisches Log: Methode, Pfad ohne Query, Status, Dauer. Keine IP, kein Inhalt, kein Ort.
        app.Use(async (kontext, weiter) =>
        {
            var start = Stopwatch.GetTimestamp();
            await weiter(kontext);
            log.LogInformation("{Methode} {Pfad} {Status} {Dauer} ms",
                kontext.Request.Method, kontext.Request.Path.Value, kontext.Response.StatusCode,
                (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        });

        app.Use(async (kontext, weiter) =>
        {
            if (kontext.Request.Headers.Origin.FirstOrDefault() is { Length: > 0 } ursprung
                && !UrsprungErlaubt(ursprung, optionen.ErlaubteUrspruenge))
            {
                kontext.Response.StatusCode = StatusCodes.Status403Forbidden;
                await kontext.Response.WriteAsync("Origin nicht erlaubt.");
                return;
            }

            await weiter(kontext);
        });

        app.UseRateLimiter();
        return app;
    }

    public static bool UrsprungErlaubt(string ursprung, IEnumerable<string> erlaubt)
    {
        if (!Uri.TryCreate(ursprung, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.IsLoopback || (IPAddress.TryParse(uri.Host, out var adresse) && IPAddress.IsLoopback(adresse)))
        {
            return true;
        }

        var normiert = uri.GetLeftPart(UriPartial.Authority);
        return erlaubt.Any(e => string.Equals(e.TrimEnd('/'), normiert, StringComparison.OrdinalIgnoreCase));
    }
}
