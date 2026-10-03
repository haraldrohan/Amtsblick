using Amtsblick.Core.Kontingent;
using Amtsblick.Core.Ort;
using Amtsblick.Core.Quellen;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Amtsblick.Core;

public static class KernRegistrierung
{
    /// <summary>Ortsverzeichnis, Kontingent und Quellenregister; Voraussetzung für alle Module.</summary>
    public static IServiceCollection AddAmtsblickKern(this IServiceCollection services, IConfiguration konfiguration)
    {
        services.Configure<AmtsblickOptionen>(konfiguration.GetSection(AmtsblickOptionen.Abschnitt));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<KontingentRegister>();
        services.AddSingleton(sp =>
        {
            var optionen = sp.GetRequiredService<IOptions<AmtsblickOptionen>>().Value;
            return GemeindeDatenbank.Lade(Path.Combine(optionen.ErmittleDatenVerzeichnis(), GemeindeDatenbank.Dateiname));
        });
        services.AddSingleton<OrtResolver>();
        services.AddSingleton<StatistikAustriaQuelle>();
        services.AddSingleton<IQuellenAnbieter>(sp => sp.GetRequiredService<StatistikAustriaQuelle>());
        return services;
    }
}
