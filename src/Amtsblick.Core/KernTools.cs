using System.ComponentModel;
using Amtsblick.Core.Kontingent;
using Amtsblick.Core.Ort;
using Amtsblick.Core.Quellen;
using ModelContextProtocol.Server;

namespace Amtsblick.Core;

[McpServerToolType]
public sealed class KernTools(
    OrtResolver orte,
    StatistikAustriaQuelle statistik,
    IEnumerable<IQuellenAnbieter> quellenAnbieter,
    KontingentRegister kontingent)
{
    [McpServerTool(Name = "ort_finden", Title = "Ort finden", ReadOnly = true, Destructive = false, OpenWorld = true, Idempotent = true)]
    [Description("""
        Sucht österreichische Gemeinden nach Name, Gemeindekennziffer (GKZ) oder Koordinate und liefert
        GKZ, Bezirk, Bundesland und Mittelpunkt. Erst exakte, dann unscharfe Suche; Umlaute und
        "St."/"Sankt" werden gleich behandelt. Mehrdeutige Namen kommen als Liste zurück.
        Beispielfragen: "In welchem Bezirk liegt Steyr?" · "Welche Gemeinden heißen Sankt Johann?" ·
        "Zu welcher Gemeinde gehört 48.04, 14.42?"
        Nenne in deiner Antwort die Datenquelle mit Lizenz; beides steht am Ende der Zusammenfassung.
        """)]
    public string OrtFinden(
        [Description("Gemeindename (auch mit Tippfehler), optional mit Zusatz \"Name, Bundesland\"; oder fünfstellige GKZ; oder Koordinate \"Breite, Länge\" in Dezimalgrad, z. B. \"48.04, 14.42\".")]
        string text)
    {
        var treffer = orte.Finde(text, max: 20);
        var zeilen = treffer.Select(t => new
        {
            t.Gemeinde.Gkz,
            t.Gemeinde.Name,
            t.Gemeinde.Bezirk,
            t.Gemeinde.Bundesland,
            Lat = Math.Round(t.Gemeinde.Mittelpunkt.Lat, 4),
            Lon = Math.Round(t.Gemeinde.Mittelpunkt.Lon, 4),
            Treffer = t.Art,
        }).ToList();

        var zusammenfassung = treffer.Count switch
        {
            0 => orte.Loese(text).Hinweis ?? $"Kein Ort zu \"{text}\" gefunden.",
            1 => $"{treffer[0].Gemeinde.Name}, Bezirk {treffer[0].Gemeinde.Bezirk}, {treffer[0].Gemeinde.Bundesland} (GKZ {treffer[0].Gemeinde.Gkz}).",
            _ => $"{treffer.Count} Treffer zu \"{text}\": {string.Join(", ", treffer.Take(5).Select(t => t.Gemeinde.Name))}{(treffer.Count > 5 ? " …" : "")}",
        };

        return ToolAntwort.Erzeuge(zusammenfassung, new { Treffer = zeilen }, [statistik.Vermerk], []);
    }

    /// <summary>Weitergabepflichten aus CC BY 4.0 und Abgrenzung gegenüber den Datengebern.</summary>
    public const string Lizenzhinweis =
        "Die Daten bleiben unter der Lizenz der jeweiligen Quelle. Bei Weitergabe sind Vermerk (wörtlich), "
        + "Lizenz mit Link und die Angabe zur Bearbeitung zu übernehmen. Die Datengeber sind an Amtsblick nicht "
        + "beteiligt und billigen weder das Projekt noch die Aufbereitung.";

    [McpServerTool(Name = "quellen", Title = "Quellen und Lizenzen", ReadOnly = true, Destructive = false, OpenWorld = true, Idempotent = true)]
    [Description("""
        Listet alle Datenquellen dieses Servers mit Lizenz und Link zum Lizenztext, vorgeschriebenem
        Quellenvermerk, Link, Datensatz, DOI, Stand und der Angabe, wie Amtsblick die Daten aufbereitet,
        sowie das verbleibende Anfragekontingent je Quelle.
        Beispielfragen: "Woher stammen die Daten?" · "Unter welcher Lizenz stehen die Pegeldaten?" ·
        "Wie viele Wetterabfragen sind in dieser Stunde noch möglich?"
        Gib Vermerk und Lizenz je Quelle so wieder, wie sie in der Liste stehen.
        """)]
    public string Quellen()
    {
        var quellen = quellenAnbieter.SelectMany(a => a.Quellen()).ToList();
        var kontingente = kontingent.Alle.Select(k => new
        {
            k.Quelle,
            k.RestStunde,
            k.LimitStunde,
            k.RestSekunde,
            k.LimitSekunde,
            Stand = Zeit.Iso(k.Stand),
        }).ToList();

        var namen = quellen.Select(q => q.Quelle).Distinct().ToList();
        return ToolAntwort.Erzeuge(
            $"{quellen.Count} Datensätze von {namen.Count} Quellen: {string.Join(", ", namen)}.",
            new { Kontingent = kontingente },
            quellen,
            [ToolAntwort.Pflichthinweis,
             Lizenzhinweis,
             "Kontingent wird erst nach der ersten Anfrage an eine Quelle bekannt."],
            quellenInZusammenfassung: false);
    }
}
