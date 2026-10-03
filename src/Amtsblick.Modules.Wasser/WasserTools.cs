using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Amtsblick.Modules.Wasser;

[McpServerToolType]
public sealed class WasserTools(PegelAuskunft auskunft)
{
    [McpServerTool(Name = "pegel_in_der_naehe", Title = "Pegel in der Nähe", ReadOnly = true, OpenWorld = true)]
    [Description("""
        Aktuelle Pegelmessstellen (Wasserstand oder Durchfluss) des Hydrographischen Dienstes im Umkreis einer
        österreichischen Gemeinde, nach Entfernung sortiert, höchstens 20. Je Messstelle: Gewässer, Wert mit
        Einheit, Messzeitpunkt, Lage (Nieder-/Mittelwasser, erhöhte Wasserführung, Hochwasserstufe) und Tendenz.
        Der Bestand wird höchstens stündlich abgerufen. Keine amtliche Warnung.
        Beispielfragen: "Wie hoch ist die Enns in Steyr?" · "Welche Pegel gibt es rund um Schärding?" ·
        "Steigt das Wasser bei Hallein?"
        """)]
    public async Task<string> PegelInDerNaehe(
        [Description("Gemeindename, optional \"Name, Bundesland\"; oder fünfstellige GKZ; oder Koordinate \"Breite, Länge\", z. B. \"48.04, 14.42\".")]
        string ort,
        [Description("Suchradius in Kilometern um den Gemeindemittelpunkt bzw. die Koordinate, 1 bis 100. Standard 15.")]
        double radius_km = 15,
        CancellationToken ct = default) =>
        (await auskunft.InDerNaeheAsync(ort, radius_km, ct)).AlsJson();

    [McpServerTool(Name = "pegel_an_gewaesser", Title = "Pegel an einem Gewässer", ReadOnly = true, OpenWorld = true)]
    [Description("""
        Alle aktuellen Pegelmessstellen an einem österreichischen Gewässer mit Wert, Einheit, Messzeitpunkt,
        Lage und Tendenz. Der Gewässername muss dem Namen im Pegelbestand entsprechen; bei Abweichung kommen
        Vorschläge zurück. Der Bestand wird höchstens stündlich abgerufen. Keine amtliche Warnung.
        Beispielfragen: "Wie ist die Lage an der Donau?" · "Welche Messstellen gibt es an der Mur?" ·
        "Wie viel Wasser führt die Salzach?"
        """)]
    public async Task<string> PegelAnGewaesser(
        [Description("Name des Gewässers ohne Artikel, z. B. \"Enns\", \"Donau\", \"Große Mühl\".")]
        string gewaesser,
        CancellationToken ct = default) =>
        (await auskunft.AnGewaesserAsync(gewaesser, ct)).AlsJson();

    [McpServerTool(Name = "hochwasserlage", Title = "Hochwasserlage", ReadOnly = true, OpenWorld = true)]
    [Description("""
        Übersicht der Pegelmessstellen, die laut Hydrographischem Dienst mindestens erhöhte Wasserführung
        melden, gruppiert nach Stufe (erhöhte Wasserführung, Hochwasser Stufe 1 bis 3), jeweils mit Tendenz.
        Die Einstufung stammt aus den Quelldaten; es werden keine eigenen Warnstufen berechnet.
        Keine amtliche Warnung – maßgeblich sind die Warndienste des Landes.
        Beispielfragen: "Gibt es gerade Hochwasser in Österreich?" · "Wie ist die Hochwasserlage in
        Niederösterreich?" · "Wo steigen die Pegel in Tirol?"
        """)]
    public async Task<string> Hochwasserlage(
        [Description("Optional: Bundesland zum Einschränken, z. B. \"Oberösterreich\". Ohne Angabe ganz Österreich.")]
        string? bundesland = null,
        CancellationToken ct = default) =>
        (await auskunft.HochwasserlageAsync(bundesland, ct)).AlsJson();
}
