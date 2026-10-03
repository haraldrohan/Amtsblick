using Amtsblick.Core.Ort;
using Amtsblick.Core.Quellen;

namespace Amtsblick.Core;

/// <summary>
/// Inhalt einer Tool-Antwort vor der Serialisierung. Module liefern Teilantworten, damit
/// zusammengesetzte Tools (z. B. <c>lage_am_ort</c>) mehrere davon verbinden können.
/// </summary>
public sealed record Teilantwort(
    string Zusammenfassung,
    object? Daten,
    IReadOnlyList<Quellenvermerk> Quellen,
    IReadOnlyList<string> Hinweise)
{
    public string AlsJson() => ToolAntwort.Erzeuge(Zusammenfassung, Daten, Quellen, Hinweise);

    /// <summary>Antwort, wenn die Ortsangabe nicht auf genau eine Gemeinde führt.</summary>
    public static Teilantwort OrtUnklar(OrtAufloesung ort, Quellenvermerk statistik, params string[] hinweise) => new(
        ort.Hinweis ?? "Ort nicht gefunden.",
        new
        {
            Kandidaten = ort.Treffer.Select(t => new
            {
                t.Gemeinde.Gkz,
                t.Gemeinde.Name,
                t.Gemeinde.Bezirk,
                t.Gemeinde.Bundesland,
            }).ToList(),
        },
        [statistik],
        hinweise);
}
