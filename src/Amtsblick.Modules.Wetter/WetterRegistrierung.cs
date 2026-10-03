using Amtsblick.Core.Http;
using Amtsblick.Core.Kontingent;
using Amtsblick.Core.Quellen;
using Amtsblick.Modules.Wetter.GeoSphere;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amtsblick.Modules.Wetter;

public static class WetterRegistrierung
{
    /// <summary>Höchstens so viele Anfragen pro Sekunde an GeoSphere (erlaubt sind 5).</summary>
    private const int AnfragenProSekunde = 4;

    public static IServiceCollection AddWetterModul(this IServiceCollection services)
    {
        // Großzügiges Timeout: Aufrufer warten nur WetterDienst.Geduld, der Abruf darf im Hintergrund fertig werden.
        services.AddAmtsblickHttpClient(GeoSphereClient.Quelle, TimeSpan.FromSeconds(60), wiederholungen: 2);
        services.AddSingleton(sp => new GeoSphereClient(
            sp.GetRequiredService<IHttpClientFactory>(),
            new Anfragetakt(sp.GetRequiredService<TimeProvider>(), AnfragenProSekunde)));
        services.AddSingleton<WetterDienst>(sp => new WetterDienst(
            sp.GetRequiredService<GeoSphereClient>(),
            sp.GetRequiredService<KontingentRegister>(),
            sp.GetRequiredService<GeoSphereClient>().Takt,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetService<ILogger<WetterDienst>>()));
        services.AddSingleton<WetterAuskunft>();
        services.AddSingleton<IQuellenAnbieter>(sp => sp.GetRequiredService<WetterAuskunft>());
        return services;
    }
}
