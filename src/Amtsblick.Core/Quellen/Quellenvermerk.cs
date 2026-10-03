using Amtsblick.Core.Ort;

namespace Amtsblick.Core.Quellen;

/// <summary>
/// Herkunftsangabe, die jede Tool-Antwort begleitet. <see cref="Vermerk"/> ist der vom Datengeber
/// verlangte Wortlaut und darf nicht umformuliert werden.
/// </summary>
/// <param name="Stand">Zeitpunkt oder Datum, auf das sich die Daten beziehen (ISO 8601).</param>
public sealed record Quellenvermerk(
    string Quelle,
    string Lizenz,
    string Vermerk,
    string Link,
    string? Datensatz = null,
    string? Stand = null);

/// <summary>Jedes aktive Modul meldet hierüber seine Quellen für das Tool <c>quellen</c>.</summary>
public interface IQuellenAnbieter
{
    IEnumerable<Quellenvermerk> Quellen();
}

public sealed class StatistikAustriaQuelle(GemeindeVerzeichnis verzeichnis) : IQuellenAnbieter
{
    public const string Lizenz = "CC BY 4.0";

    public Quellenvermerk Vermerk => new(
        Quelle: "Statistik Austria",
        Lizenz: Lizenz,
        Vermerk: "Datenquelle: Statistik Austria — data.statistik.gv.at",
        Link: "https://data.statistik.gv.at",
        Datensatz: "OGDEXT_GEM_1, OGDEXT_POLBEZ_1",
        Stand: verzeichnis.Gebietsstand?.ToString("yyyy-MM-dd"));

    public IEnumerable<Quellenvermerk> Quellen() => [Vermerk];
}
