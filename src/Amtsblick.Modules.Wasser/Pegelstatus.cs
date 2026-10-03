namespace Amtsblick.Modules.Wasser;

/// <summary>
/// Der dreistellige <c>gesamtcode</c> von eHYD, zerlegt in Lage, Tendenz und Aktualität.
/// Die Einstufung stammt vom Hydrographischen Dienst; Amtsblick übersetzt nur die Ziffern.
/// Jede Ziffer, die nicht dokumentiert ist, wird als "unbekannt" ausgegeben.
/// </summary>
public sealed record Pegelstatus(
    int? Code,
    int? LageZiffer,
    string Lage,
    int? TendenzZiffer,
    string Tendenz,
    int? AktualitaetZiffer,
    string Aktualitaet)
{
    public const string Unbekannt = "unbekannt";

    private static readonly Dictionary<int, string> Lagen = new()
    {
        [1] = "Niederwasser",
        [2] = "Mittelwasser",
        [3] = "erhöhte Wasserführung",
        [4] = "Hochwasser Stufe 1",
        [5] = "Hochwasser Stufe 2",
        [6] = "Hochwasser Stufe 3",
        [9] = "keine Daten",
    };

    private static readonly Dictionary<int, string> Tendenzen = new()
    {
        [0] = "gleich",
        [1] = "steigend",
        [2] = "sinkend",
        [3] = "normal",
    };

    private static readonly Dictionary<int, string> Aktualitaeten = new()
    {
        [0] = "normal",
        [1] = "älter als 24 h",
    };

    /// <summary>Lage 3 bis 6: erhöhte Wasserführung oder Hochwasser.</summary>
    public bool AbErhoehterWasserfuehrung => LageZiffer is >= 3 and <= 6;

    public static Pegelstatus Zerlege(int? code)
    {
        if (code is not (>= 100 and <= 999))
        {
            return new Pegelstatus(code, null, Unbekannt, null, Unbekannt, null, Unbekannt);
        }

        var lage = code.Value / 100;
        var tendenz = code.Value / 10 % 10;
        var aktualitaet = code.Value % 10;
        return new Pegelstatus(
            code,
            lage,
            Lagen.GetValueOrDefault(lage, Unbekannt),
            tendenz,
            Tendenzen.GetValueOrDefault(tendenz, Unbekannt),
            aktualitaet,
            Aktualitaeten.GetValueOrDefault(aktualitaet, Unbekannt));
    }
}
