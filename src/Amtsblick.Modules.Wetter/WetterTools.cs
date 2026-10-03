using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Amtsblick.Modules.Wetter;

[McpServerToolType]
public sealed class WetterTools(WetterAuskunft auskunft)
{
    [McpServerTool(Name = "wetter_prognose", Title = "Wetterprognose", ReadOnly = true, OpenWorld = true)]
    [Description("""
        Wetterprognose der GeoSphere Austria für eine österreichische Gemeinde: 6-Stunden-Blöcke mit
        Temperatur (min/max), Niederschlag, Schneeanteil, stärkster Böe, Wind und Schneefallgrenze, dazu
        Tageswerte. Stündliches Modell mit 1 km Auflösung, bis 61 Stunden voraus. Keine amtliche Unwetterwarnung.
        Beispielfragen: "Regnet es heute Abend in Steyr?" · "Wie warm wird es morgen in St. Pölten?" ·
        "Wie stark wird der Wind am Wochenende in Mariazell?"
        """)]
    public async Task<string> WetterPrognose(
        [Description("Gemeindename, optional \"Name, Bundesland\"; oder fünfstellige GKZ; oder Koordinate \"Breite, Länge\", z. B. \"48.04, 14.42\". Bei einem Namen gilt die Prognose für den Gemeindemittelpunkt.")]
        string ort,
        [Description("Vorhersagezeitraum in Stunden ab jetzt, 1 bis 61. Standard 48.")]
        int stunden = 48,
        CancellationToken ct = default) =>
        (await auskunft.PrognoseAsync(ort, stunden, ct)).AlsJson();

    [McpServerTool(Name = "niederschlag_jetzt", Title = "Niederschlag in den nächsten Stunden", ReadOnly = true, OpenWorld = true)]
    [Description("""
        Niederschlag der nächsten 3 Stunden in 15-Minuten-Schritten (Nowcast der GeoSphere Austria) für eine
        österreichische Gemeinde, dazu Temperatur, Wind und Böen. Ist der Nowcast nicht aktuell, kommen
        Stundenwerte aus der Prognose und die Antwort sagt das. Keine amtliche Unwetterwarnung.
        Beispielfragen: "Regnet es gleich in Linz?" · "Wann hört der Regen in Graz auf?" ·
        "Bleibt es in der nächsten Stunde in Steyr trocken?"
        """)]
    public async Task<string> NiederschlagJetzt(
        [Description("Gemeindename, optional \"Name, Bundesland\"; oder fünfstellige GKZ; oder Koordinate \"Breite, Länge\", z. B. \"48.04, 14.42\".")]
        string ort,
        CancellationToken ct = default) =>
        (await auskunft.NiederschlagJetztAsync(ort, ct)).AlsJson();
}
