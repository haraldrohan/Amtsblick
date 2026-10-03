using Amtsblick.Core;
using Amtsblick.Core.Http;
using Amtsblick.Core.Ort;
using Amtsblick.Core.Quellen;
using Amtsblick.Modules.Wasser.Ehyd;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amtsblick.Modules.Wasser;

/// <summary>Konfigurationsabschnitt "Amtsblick:Module:Wasser".</summary>
public sealed class WasserOptionen
{
    public const string Abschnitt = "Amtsblick:Module:Wasser";

    /// <summary>Aus: das Modul ruft eHYD nicht mehr ab; wirkt bei Änderung der appsettings.json sofort.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>
    /// Nur für den gehosteten Betrieb: ein Hintergrund-Timer holt den Bestand stündlich, damit er beim
    /// ersten Tool-Aufruf schon vorliegt. Standard ist aus; dann ruft die Instanz ohne Tool-Aufruf nichts ab.
    /// </summary>
    public bool Vorladen { get; set; }
}

public static class WasserRegistrierung
{
    /// <summary>Letzter Pegelstand im Datenverzeichnis; überdauert Neustarts.</summary>
    public const string Speicherdatei = "pegel_aktuell.json";

    public static IServiceCollection AddWasserModul(this IServiceCollection services, IConfiguration konfiguration)
    {
        var abschnitt = konfiguration.GetSection(WasserOptionen.Abschnitt);
        services.Configure<WasserOptionen>(abschnitt);

        // Keine Wiederholungen: auch ein gescheiterter Versuch verbraucht den einen Abruf der Stunde.
        services.AddAmtsblickHttpClient(EhydClient.Quelle, TimeSpan.FromSeconds(30), wiederholungen: 0);
        services.AddSingleton<EhydClient>();
        services.AddSingleton(sp =>
        {
            var wasser = sp.GetRequiredService<IOptionsMonitor<WasserOptionen>>();
            return new PegelDienst(
                sp.GetRequiredService<EhydClient>(),
                sp.GetRequiredService<GemeindeVerzeichnis>(),
                sp.GetRequiredService<IOptions<AmtsblickOptionen>>(),
                sp.GetRequiredService<TimeProvider>(),
                () => wasser.CurrentValue.Aktiv,
                sp.GetService<ILogger<PegelDienst>>(),
                Path.Combine(sp.GetRequiredService<IOptions<AmtsblickOptionen>>().Value.ErmittleDatenVerzeichnis(), Speicherdatei));
        });
        services.AddSingleton<PegelAuskunft>();
        services.AddSingleton<IQuellenAnbieter>(sp => sp.GetRequiredService<PegelAuskunft>());
        if (abschnitt.GetValue(nameof(WasserOptionen.Vorladen), false))
        {
            services.AddHostedService<PegelVorlader>();
        }

        return services;
    }
}

/// <summary>Optionaler Hintergrund-Timer; der <see cref="PegelDienst"/> begrenzt weiterhin auf einen Abruf pro Stunde.</summary>
public sealed class PegelVorlader(PegelDienst dienst, TimeProvider zeit) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppToken)
    {
        using var timer = new PeriodicTimer(PegelDienst.Abstand + TimeSpan.FromSeconds(5), zeit);
        do
        {
            await dienst.StandAsync(stoppToken);
        }
        while (await timer.WaitForNextTickAsync(stoppToken));
    }
}
