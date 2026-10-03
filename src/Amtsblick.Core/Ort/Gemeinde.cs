namespace Amtsblick.Core.Ort;

/// <summary>Gemeinde laut Statistik Austria. <see cref="Gkz"/> ist die fünfstellige Gemeindekennziffer.</summary>
public sealed record Gemeinde(
    string Gkz,
    string Name,
    string Bezirk,
    string Bundesland,
    Koordinate Mittelpunkt,
    Flaeche? Flaeche = null);

public static class Bundeslaender
{
    private static readonly string[] Namen =
    [
        "Burgenland", "Kärnten", "Niederösterreich", "Oberösterreich", "Salzburg",
        "Steiermark", "Tirol", "Vorarlberg", "Wien",
    ];

    public static IReadOnlyList<string> Alle => Namen;

    /// <summary>Die erste Ziffer der GKZ bezeichnet das Bundesland (1 = Burgenland … 9 = Wien).</summary>
    public static string AusGkz(string gkz) =>
        gkz.Length > 0 && gkz[0] is >= '1' and <= '9' ? Namen[gkz[0] - '1'] : "unbekannt";

    /// <summary>Findet ein Bundesland unabhängig von Schreibweise und Umlauten ("oberoesterreich", "OÖ" nicht).</summary>
    public static string? Finde(string? text)
    {
        var gesucht = Namensnormalisierung.Normalisiere(text);
        return gesucht.Length == 0
            ? null
            : Namen.FirstOrDefault(n => Namensnormalisierung.Normalisiere(n) == gesucht);
    }
}
