using System.Net;
using System.Reflection;
using Amtsblick.Core.Kontingent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Amtsblick.Core.Http;

public static class UserAgent
{
    /// <summary>Paketversion samt Vorabkennung, z. B. 0.1.0-beta.</summary>
    public static string Version { get; } =
        typeof(UserAgent).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(UserAgent).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    /// <summary>"Amtsblick/&lt;version&gt; (+&lt;Repo-URL&gt;; &lt;Kontaktadresse&gt;)"; ohne Kontakt entfällt der zweite Teil.</summary>
    public static string Erzeuge(AmtsblickOptionen optionen) =>
        string.IsNullOrWhiteSpace(optionen.Kontakt)
            ? $"Amtsblick/{Version} (+{optionen.RepoUrl})"
            : $"Amtsblick/{Version} (+{optionen.RepoUrl}; {optionen.Kontakt.Trim()})";
}

/// <summary>
/// Wiederholt eine Anfrage ausschließlich bei 5xx und 429, mit exponentiell wachsender Pause.
/// Nennt der Server <c>Retry-After</c>, gilt dieser Wert (höchstens 30 Sekunden).
/// </summary>
public sealed class RetryHandler(int wiederholungen, TimeSpan basis, TimeProvider zeit) : DelegatingHandler
{
    private static readonly TimeSpan MaxPause = TimeSpan.FromSeconds(30);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        for (var versuch = 0; ; versuch++)
        {
            var antwort = await base.SendAsync(request, ct);
            var wiederholbar = (int)antwort.StatusCode >= 500 || antwort.StatusCode == HttpStatusCode.TooManyRequests;
            if (!wiederholbar || versuch >= wiederholungen)
            {
                return antwort;
            }

            var pause = antwort.Headers.RetryAfter?.Delta ?? basis * Math.Pow(2, versuch);
            antwort.Dispose();
            await Task.Delay(pause > MaxPause ? MaxPause : pause, zeit, ct);
        }
    }
}

/// <summary>Reicht die Kontingent-Header jeder Antwort an das <see cref="KontingentRegister"/> weiter.</summary>
public sealed class KontingentHandler(KontingentRegister register, string quelle) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var antwort = await base.SendAsync(request, ct);
        register.Melde(quelle, antwort);
        return antwort;
    }
}

public static class HttpRegistrierung
{
    /// <summary>
    /// Benannter HttpClient für eine Quelle: User-Agent, Timeout, Kontingent-Erfassung und Retry.
    /// </summary>
    public static IHttpClientBuilder AddAmtsblickHttpClient(
        this IServiceCollection services, string name, TimeSpan timeout, int wiederholungen)
    {
        return services
            .AddHttpClient(name, (sp, client) =>
            {
                client.Timeout = timeout;
                var optionen = sp.GetRequiredService<IOptions<AmtsblickOptionen>>().Value;
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent.Erzeuge(optionen));
            })
            .AddHttpMessageHandler(sp => new RetryHandler(wiederholungen, TimeSpan.FromSeconds(1), sp.GetRequiredService<TimeProvider>()))
            .AddHttpMessageHandler(sp => new KontingentHandler(sp.GetRequiredService<KontingentRegister>(), name));
    }
}
