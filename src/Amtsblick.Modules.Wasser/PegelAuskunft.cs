using Amtsblick.Core;
using Amtsblick.Core.Ort;
using Amtsblick.Core.Quellen;
using Amtsblick.Modules.Wasser.Ehyd;

namespace Amtsblick.Modules.Wasser;

/// <summary>Baut die Antworten der Pegel-Tools aus dem Bestand des <see cref="PegelDienst"/>.</summary>
public sealed class PegelAuskunft(PegelDienst dienst, OrtResolver orte, StatistikAustriaQuelle statistik) : IQuellenAnbieter
{
    public const string Hinweis = "Keine amtliche Warnung. Maßgeblich sind die Warndienste des Landes.";

    private const int MaxInDerNaehe = 20;
    private const int MaxJeStufe = 50;

    public IEnumerable<Quellenvermerk> Quellen() => [Vermerk(null)];

    /// <summary>Wortlaut laut Metadaten des Dienstes; "ehyd.gv.at" ist als Link auf https://ehyd.gv.at zu setzen.</summary>
    public const string Namensnennung = "Datenquelle: ehyd.gv.at";

    public static Quellenvermerk Vermerk(DateTimeOffset? abgerufen) => new(
        Quelle: "ehyd.gv.at – Hydrographischer Dienst Österreich (BMLUK und Bundesländer)",
        Lizenz: Quellenvermerk.CcBy40,
        LizenzLink: Quellenvermerk.CcBy40Link,
        Vermerk: Namensnennung,
        Link: "https://ehyd.gv.at",
        Datensatz: EhydClient.Layer,
        Stand: Zeit.Iso(abgerufen),
        Bearbeitung: "Statuscode in Lage, Tendenz und Aktualität übersetzt, Gemeinde und Entfernung ergänzt, Auswahl nach Ort, Gewässer oder Stufe");

    /// <summary>Tool <c>pegel_in_der_naehe</c>.</summary>
    public async Task<Teilantwort> InDerNaeheAsync(string ort, double radiusKm = 15, CancellationToken ct = default)
    {
        var aufloesung = orte.Loese(ort);
        return aufloesung.Eindeutig
            ? await InDerNaeheAsync(aufloesung, radiusKm, MaxInDerNaehe, ct)
            : Teilantwort.OrtUnklar(aufloesung, statistik.Vermerk, Hinweis);
    }

    /// <summary>Messstellen um einen bereits aufgelösten Ort, nach Entfernung sortiert.</summary>
    public async Task<Teilantwort> InDerNaeheAsync(OrtAufloesung aufloesung, double radiusKm, int max, CancellationToken ct = default)
    {
        var stand = await dienst.StandAsync(ct);
        if (stand.Abgerufen is null)
        {
            return OhneDaten(stand);
        }

        var gemeinde = aufloesung.Gemeinde!;
        radiusKm = Math.Clamp(radiusKm, 1, 100);
        var nahe = stand.Messstellen
            .Where(m => m.Ort is not null)
            .Select(m => (Messstelle: m, Km: m.Ort!.Value.EntfernungKm(aufloesung.Punkt)))
            .Where(m => m.Km <= radiusKm)
            .OrderBy(m => m.Km)
            .Take(max)
            .ToList();

        var zusammenfassung = nahe.Count == 0
            ? $"Keine Pegelmessstelle im Umkreis von {radiusKm:0} km um {gemeinde.Name}."
            : $"{nahe.Count} Pegelmessstelle{(nahe.Count == 1 ? "" : "n")} im Umkreis von {radiusKm:0} km um {gemeinde.Name}; "
              + $"nächste: {Kurz(nahe[0].Messstelle)}.";

        return new Teilantwort(
            zusammenfassung + StandText(stand),
            new
            {
                Ort = new { gemeinde.Name, gemeinde.Gkz, gemeinde.Bundesland },
                RadiusKm = radiusKm,
                StandAbruf = Zeit.Iso(stand.Abgerufen),
                stand.Veraltet,
                Messstellen = nahe.Select(m => Zeile(m.Messstelle, m.Km)).ToList(),
            },
            [Vermerk(stand.Abgerufen)],
            Hinweise(stand, aufloesung.Hinweis));
    }

    /// <summary>Tool <c>pegel_an_gewaesser</c>.</summary>
    public async Task<Teilantwort> AnGewaesserAsync(string gewaesser, CancellationToken ct = default)
    {
        var stand = await dienst.StandAsync(ct);
        if (stand.Abgerufen is null)
        {
            return OhneDaten(stand);
        }

        var gesucht = Namensnormalisierung.Normalisiere(gewaesser);
        var namen = stand.Messstellen
            .Select(m => m.Gewaesser)
            .Where(n => n.Length > 0)
            .Distinct()
            .Select(n => (Name: n, Norm: Namensnormalisierung.Normalisiere(n)))
            .ToList();

        string? hinweis = null;
        var treffer = namen.Where(n => n.Norm == gesucht).Select(n => n.Name).ToList();
        if (treffer.Count == 0 && gesucht.Length > 0)
        {
            // Kein Gewässer dieses Namens: Namensteile und Tippfehler als Vorschläge.
            var aehnlich = namen
                .Where(n => n.Norm.Contains(gesucht, StringComparison.Ordinal)
                         || Namensnormalisierung.Abstand(n.Norm, gesucht) <= (gesucht.Length <= 4 ? 1 : 2))
                .Select(n => n.Name)
                .Order()
                .ToList();
            if (aehnlich.Count != 1)
            {
                return new Teilantwort(
                    aehnlich.Count == 0
                        ? $"Kein Gewässer \"{gewaesser}\" im Pegelbestand gefunden."
                        : $"Kein Gewässer \"{gewaesser}\" gefunden; ähnlich: {string.Join(", ", aehnlich.Take(15))}.",
                    new { StandAbruf = Zeit.Iso(stand.Abgerufen), stand.Veraltet, Vorschlaege = aehnlich.Take(30).ToList() },
                    [Vermerk(stand.Abgerufen)],
                    Hinweise(stand, null));
            }

            treffer = aehnlich;
            hinweis = $"\"{gewaesser}\" wurde als {aehnlich[0]} interpretiert.";
        }

        var messstellen = stand.Messstellen
            .Where(m => treffer.Contains(m.Gewaesser))
            .OrderBy(m => m.Hzbnr)
            .ToList();
        var lagen = messstellen
            .GroupBy(m => m.Status.Lage)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count()}× {g.Key}");

        return new Teilantwort(
            $"{treffer[0]}: {messstellen.Count} Messstelle{(messstellen.Count == 1 ? "" : "n")} ({string.Join(", ", lagen)})." + StandText(stand),
            new
            {
                Gewaesser = treffer[0],
                StandAbruf = Zeit.Iso(stand.Abgerufen),
                stand.Veraltet,
                Messstellen = messstellen.Select(m => Zeile(m, null)).ToList(),
            },
            [Vermerk(stand.Abgerufen)],
            Hinweise(stand, hinweis));
    }

    /// <summary>Tool <c>hochwasserlage</c>.</summary>
    public async Task<Teilantwort> HochwasserlageAsync(string? bundesland = null, CancellationToken ct = default)
    {
        string? land = null;
        if (!string.IsNullOrWhiteSpace(bundesland))
        {
            land = Bundeslaender.Finde(bundesland);
            if (land is null)
            {
                return new Teilantwort(
                    $"Unbekanntes Bundesland \"{bundesland}\". Möglich: {string.Join(", ", Bundeslaender.Alle)}.",
                    null,
                    [Vermerk(null)],
                    [Hinweis]);
            }
        }

        var stand = await dienst.StandAsync(ct);
        if (stand.Abgerufen is null)
        {
            return OhneDaten(stand);
        }

        var messstellen = stand.Messstellen.Where(m => land is null || m.Bundesland == land).ToList();
        var stufen = messstellen
            .Where(m => m.Status.AbErhoehterWasserfuehrung)
            .GroupBy(m => m.Status.LageZiffer!.Value)
            .OrderByDescending(g => g.Key)
            .Select(g => new
            {
                Lage = g.First().Status.Lage,
                Anzahl = g.Count(),
                Messstellen = g.OrderBy(m => m.Gewaesser).ThenBy(m => m.Name).Take(MaxJeStufe).Select(m => Zeile(m, null)).ToList(),
            })
            .ToList();

        var gebiet = land ?? "Österreich";
        var ohneDaten = messstellen.Count(m => m.Status.LageZiffer is 9 or null);
        var zusammenfassung = stufen.Count == 0
            ? $"{gebiet}: keine der {messstellen.Count} Messstellen meldet erhöhte Wasserführung oder Hochwasser."
            : $"{gebiet}: {string.Join(", ", stufen.Select(s => $"{s.Anzahl}× {s.Lage}"))} (von {messstellen.Count} Messstellen).";

        var hinweise = Hinweise(stand, null);
        if (stufen.Any(s => s.Anzahl > MaxJeStufe))
        {
            hinweise.Insert(0, $"Je Stufe werden höchstens {MaxJeStufe} Messstellen aufgelistet; die Anzahl ist vollständig.");
        }

        if (land is not null && stand.Messstellen.All(m => m.Bundesland is null))
        {
            hinweise.Insert(0, "Den Messstellen ist kein Bundesland zugeordnet, weil die Gemeindedaten nicht importiert sind.");
        }

        return new Teilantwort(
            zusammenfassung + StandText(stand),
            new
            {
                Gebiet = gebiet,
                StandAbruf = Zeit.Iso(stand.Abgerufen),
                stand.Veraltet,
                MessstellenGesamt = messstellen.Count,
                MessstellenOhneDaten = ohneDaten,
                Stufen = stufen,
            },
            [Vermerk(stand.Abgerufen)],
            hinweise);
    }

    private static Teilantwort OhneDaten(Pegelstand stand) => new(
        $"Pegeldaten derzeit nicht verfügbar. {stand.Fehler}".TrimEnd(),
        null,
        [Vermerk(null)],
        stand.Fehler?.StartsWith("Abruf", StringComparison.Ordinal) == true
            ? ["Der nächste Abrufversuch erfolgt frühestens 60 Minuten nach dem letzten.", Hinweis]
            : [Hinweis]);

    private static string StandText(Pegelstand stand) =>
        $" Stand des Abrufs: {Zeit.Kurz(stand.Abgerufen!.Value)}{(stand.Veraltet ? " (veraltet)" : "")}.";

    private static List<string> Hinweise(Pegelstand stand, string? ortshinweis)
    {
        var hinweise = new List<string>();
        if (ortshinweis is not null)
        {
            hinweise.Add(ortshinweis);
        }

        if (stand.Veraltet)
        {
            hinweise.Add($"Veraltet: {stand.Fehler} Es gilt der Stand vom {Zeit.Kurz(stand.Abgerufen!.Value)}.");
        }

        hinweise.Add("Der Bestand wird höchstens einmal pro Stunde abgerufen; die Werte können bis zu einer Stunde älter sein als beim Hydrographischen Dienst.");
        hinweise.Add(Hinweis);
        return hinweise;
    }

    private static string Kurz(Messstelle m)
    {
        var wert = m.Wert is { } w ? $"{Zeit.Zahl(w, w >= 100 ? 0 : 2)} {m.Einheit}".Trim() : "kein Wert";
        return $"{m.Name}, Gewässer {m.Gewaesser}: {(m.Parameter == "W" ? "Wasserstand" : "Durchfluss")} {wert}, {m.Status.Lage}, Tendenz {m.Status.Tendenz}";
    }

    private static object Zeile(Messstelle m, double? km) => new
    {
        m.Hzbnr,
        Messstelle = m.Name,
        m.Gewaesser,
        m.Gemeinde,
        m.Gkz,
        EntfernungKm = km is { } k ? Math.Round(k, 1) : (double?)null,
        Parameter = m.Parameter switch { "W" => "W (Wasserstand)", "Q" => "Q (Durchfluss)", _ => m.Parameter },
        m.Wert,
        Einheit = m.Einheit.Length > 0 ? m.Einheit : null,
        WertwCm = m.WertWCm,
        Zeitpunkt = Zeit.Iso(m.Zeitpunkt),
        m.Status.Lage,
        m.Status.Tendenz,
        m.Status.Aktualitaet,
        Gesamtcode = m.Status.Code,
        m.Prognose,
        m.Hydrodienst,
        m.Internet,
    };
}
