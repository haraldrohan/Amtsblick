using Amtsblick.Core;
using Amtsblick.Core.Http;
using Amtsblick.Modules.Wasser;
using Amtsblick.Modules.Wetter;
using ModelContextProtocol.Protocol;

namespace Amtsblick.Server;

public static class ServerRegistrierung
{
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
                o.ServerInfo = new Implementation { Name = "amtsblick", Title = "Amtsblick", Version = UserAgent.Version };
                o.ServerInstructions =
                    "Amtliche österreichische Daten nach Ort: Wetter (GeoSphere Austria) und Pegel (eHYD). "
                    + "Orte sind Gemeinden; bei mehrdeutigen Namen zuerst ort_finden nutzen. "
                    + "Jede Antwort enthält Quellenvermerke und Hinweise, die an Nutzer weiterzugeben sind. "
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
}
