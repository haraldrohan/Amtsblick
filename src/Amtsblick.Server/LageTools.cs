using System.ComponentModel;
using Amtsblick.Core;
using Amtsblick.Core.Ort;
using Amtsblick.Core.Quellen;
using Amtsblick.Modules.Wasser;
using Amtsblick.Modules.Wetter;
using ModelContextProtocol.Server;

namespace Amtsblick.Server;

/// <summary>Modulübergreifendes Tool; liegt im Server, weil nur er alle Module kennt.</summary>
[McpServerToolType]
public sealed class LageTools(OrtResolver orte, WetterAuskunft wetter, StatistikAustriaQuelle statistik, IServiceProvider dienste)
{
    private const double PegelRadiusKm = 15;
    private const int MaxPegel = 5;

    [McpServerTool(Name = "lage_am_ort", Title = "Lage am Ort", ReadOnly = true, Destructive = false, OpenWorld = true)]
    [Description("""
        Überblick für eine österreichische Gemeinde in einem Aufruf: aktuelles Wetter und Niederschlag der
        nächsten 3 Stunden (Nowcast), Prognose der nächsten 24 Stunden und – wenn das Modul Wasser aktiv ist –
        die nächstgelegenen Pegel im Umkreis von 15 km. Keine amtliche Warnung.
        Beispielfragen: "Wie ist die Lage in Steyr?" · "Was ist gerade in Schärding los, Wetter und Wasser?" ·
        "Gib mir einen Überblick für Hallein."
        Die Antwort endet mit dem Quellenvermerk des Datengebers samt Lizenz (CC BY 4.0) und Lizenzlink.
        """)]
    public async Task<string> LageAmOrt(
        [Description("Gemeindename, optional \"Name, Bundesland\"; oder fünfstellige GKZ; oder Koordinate \"Breite, Länge\", z. B. \"48.04, 14.42\".")]
        string ort,
        CancellationToken ct = default)
    {
        var pegelAuskunft = dienste.GetService<PegelAuskunft>();
        var aufloesung = orte.Loese(ort);
        if (!aufloesung.Eindeutig)
        {
            string[] hinweise = pegelAuskunft is null
                ? [WetterAuskunft.Hinweis]
                : [WetterAuskunft.Hinweis, PegelAuskunft.Hinweis];
            return Teilantwort.OrtUnklar(aufloesung, statistik.Vermerk, hinweise).AlsJson();
        }

        var wetterTeil = await wetter.LageAsync(aufloesung, ct);
        var pegelTeil = pegelAuskunft is null
            ? null
            : await pegelAuskunft.InDerNaeheAsync(aufloesung, PegelRadiusKm, MaxPegel, ct);
        var gemeinde = aufloesung.Gemeinde!;

        return ToolAntwort.Erzeuge(
            pegelTeil is null ? wetterTeil.Zusammenfassung : $"{wetterTeil.Zusammenfassung} {pegelTeil.Zusammenfassung}",
            new
            {
                Ort = new { gemeinde.Name, gemeinde.Gkz, gemeinde.Bezirk, gemeinde.Bundesland },
                Wetter = wetterTeil.Daten,
                Pegel = pegelTeil?.Daten,
            },
            wetterTeil.Quellen.Concat(pegelTeil?.Quellen ?? []),
            wetterTeil.Hinweise.Concat(pegelTeil?.Hinweise ?? []).Distinct());
    }
}
