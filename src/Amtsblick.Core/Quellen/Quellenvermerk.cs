using Amtsblick.Core.Ort;

namespace Amtsblick.Core.Quellen;

/// <summary>
/// Herkunftsangabe, die jede Tool-Antwort begleitet und die Pflichten aus CC BY 4.0 erfüllt:
/// Namensnennung im vom Datengeber verlangten Wortlaut (<see cref="Vermerk"/>, nicht umformulieren),
/// Name der Lizenz samt Link auf ihren Text, Link zur Quelle und Hinweis auf Bearbeitungen.
/// </summary>
/// <param name="Doi">Dauerhafte Kennung des Datensatzes als URL, sofern der Datengeber eine vergibt.</param>
/// <param name="Stand">Zeitpunkt oder Datum, auf das sich die Daten beziehen (ISO 8601).</param>
/// <param name="Bearbeitung">Was Amtsblick an den Daten verändert hat (CC BY 4.0, Abschnitt 3(a)(1)(B)).</param>
/// <param name="LetzterAbruf">Wann Amtsblick diese Quelle zuletzt erfolgreich abgerufen hat; im Tool <c>quellen</c>.</param>
public sealed record Quellenvermerk(
    string Quelle,
    string Lizenz,
    string LizenzLink,
    string Vermerk,
    string Link,
    string? Datensatz = null,
    string? Doi = null,
    string? Stand = null,
    string? Bearbeitung = null,
    string? LetzterAbruf = null)
{
    public const string CcBy40 = "CC BY 4.0";
    public const string CcBy40Link = "https://creativecommons.org/licenses/by/4.0/deed.de";
}

/// <summary>Jedes aktive Modul meldet hierüber seine Quellen für das Tool <c>quellen</c>.</summary>
public interface IQuellenAnbieter
{
    IEnumerable<Quellenvermerk> Quellen();
}

public sealed class StatistikAustriaQuelle(GemeindeVerzeichnis verzeichnis) : IQuellenAnbieter
{
    /// <summary>Wortlaut laut Nutzungsbedingungen von STATISTIK AUSTRIA open.data.</summary>
    public const string Namensnennung = "Datenquelle: Statistik Austria — data.statistik.gv.at";

    public Quellenvermerk Vermerk => new(
        Quelle: "Statistik Austria",
        Lizenz: Quellenvermerk.CcBy40,
        LizenzLink: Quellenvermerk.CcBy40Link,
        Vermerk: Namensnennung,
        Link: "https://data.statistik.gv.at",
        Datensatz: "OGDEXT_GEM_1, OGDEXT_POLBEZ_1",
        Stand: verzeichnis.Gebietsstand?.ToString("yyyy-MM-dd"),
        Bearbeitung: "Gemeindemittelpunkte aus den Grenzen berechnet, Schreibweise der Bezirksnamen vereinheitlicht, Wien als Ganzes ergänzt");

    public IEnumerable<Quellenvermerk> Quellen() => [Vermerk with { LetzterAbruf = Zeit.Iso(verzeichnis.Importiert) }];
}
