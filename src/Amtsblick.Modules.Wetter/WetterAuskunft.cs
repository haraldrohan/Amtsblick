using Amtsblick.Core;
using Amtsblick.Core.Ort;
using Amtsblick.Core.Quellen;
using Amtsblick.Modules.Wetter.GeoSphere;

namespace Amtsblick.Modules.Wetter;

/// <summary>Baut die Antworten der Wetter-Tools aus den Zeitreihen des <see cref="WetterDienst"/>.</summary>
public sealed class WetterAuskunft(OrtResolver orte, WetterDienst dienst, StatistikAustriaQuelle statistik, TimeProvider zeit)
    : IQuellenAnbieter
{
    public const string Hinweis = "Keine amtliche Unwetterwarnung.";
    public const string HinweisNowcastAlt = "Nowcast derzeit nicht aktuell";

    private const int SchritteIn3Stunden = 12;

    private static readonly TimeSpan NowcastHoechstalter = TimeSpan.FromHours(1);

    private static readonly object EinheitenPrognose = new
    {
        Temperatur = "°C",
        Niederschlag = "mm",
        Schnee = "mm (Wasseräquivalent)",
        Wind = "km/h",
        Boe = "km/h",
        Schneefallgrenze = "m",
        Bewoelkung = "%",
        Sonne = "min",
    };

    private static readonly object EinheitenKurzfrist = new
    {
        Temperatur = "°C",
        Niederschlag = "mm",
        Wind = "km/h",
        Boe = "km/h",
        NiederschlagsartCode = "Rohwert der GeoSphere; keine amtliche Code-Tabelle veröffentlicht",
    };

    public IEnumerable<Quellenvermerk> Quellen() =>
        new[] { Ressource.Prognose, Ressource.Nowcast }
            .Select(r => Vermerk(r, null) with { LetzterAbruf = Zeit.Iso(dienst.LetzterAbruf(r)) });

    /// <summary>Wortlaut laut Nutzungsbedingungen des GeoSphere Austria Data Hub.</summary>
    public const string Namensnennung = "Datenquelle: GeoSphere Austria - https://data.hub.geosphere.at";

    public static Quellenvermerk Vermerk(Ressource ressource, DateTimeOffset? lauf) => new(
        Quelle: "GeoSphere Austria",
        Lizenz: Quellenvermerk.CcBy40,
        LizenzLink: Quellenvermerk.CcBy40Link,
        Vermerk: Namensnennung,
        Link: "https://data.hub.geosphere.at",
        Datensatz: ressource.Id,
        Doi: ressource.Doi,
        Stand: Zeit.Iso(lauf),
        Bearbeitung: ressource.Bearbeitung);

    /// <summary>Tool <c>wetter_prognose</c>.</summary>
    public async Task<Teilantwort> PrognoseAsync(string ort, int stunden = 48, CancellationToken ct = default)
    {
        var aufloesung = orte.Loese(ort);
        if (!aufloesung.Eindeutig)
        {
            return Teilantwort.OrtUnklar(aufloesung, statistik.Vermerk, Hinweis);
        }

        var gemeinde = aufloesung.Gemeinde!;
        stunden = Math.Clamp(stunden, 1, 61);
        var abruf = await dienst.HoleAsync(Ressource.Prognose, aufloesung.Punkt, ct);
        var werte = abruf.Reihe is null ? [] : Stundenwerte(abruf.Reihe, stunden);
        if (abruf.Reihe is null || werte.Count == 0)
        {
            return OhneDaten(gemeinde, "Prognose", Ressource.Prognose, abruf, aufloesung);
        }

        var bloecke = werte
            .GroupBy(w => (Datum: DateOnly.FromDateTime(w.Von.DateTime), Block: w.Von.Hour / 6))
            .Select(g => Block(g.ToList()))
            .ToList();
        var tage = werte
            .GroupBy(w => DateOnly.FromDateTime(w.Von.DateTime))
            .Select(g => new
            {
                Datum = g.Key.ToString("yyyy-MM-dd"),
                Stunden = g.Count(),
                TempMin = Min(g, w => w.Temperatur),
                TempMax = Max(g, w => w.Temperatur),
                NiederschlagMm = Summe(g, w => w.Niederschlag),
                SchneeMm = Summe(g, w => w.Schnee),
                BoeMaxKmh = Max(g, w => w.Boe, 0),
                SonneMin = Summe(g, w => w.Sonne, 0),
            })
            .ToList();

        var zusammenfassung =
            $"{gemeinde.Name} ({gemeinde.Bundesland}), nächste {werte.Count} h: "
            + $"{Text(Min(werte, w => w.Temperatur))} bis {Text(Max(werte, w => w.Temperatur))} °C, "
            + $"Niederschlag {Text(Summe(werte, w => w.Niederschlag))} mm, "
            + $"stärkste Böe {Text(Max(werte, w => w.Boe, 0), 0)} km/h. Prognoselauf {Zeit.Kurz(abruf.Reihe.Referenzzeit)}.";

        return new Teilantwort(
            zusammenfassung,
            new
            {
                Ort = OrtAngabe(gemeinde, aufloesung.Punkt, abruf.Reihe),
                Lauf = Zeit.Iso(abruf.Reihe.Referenzzeit),
                Abgerufen = Zeit.Iso(abruf.Abgerufen),
                Zeitraum = new { Von = Zeit.Iso(werte[0].Von), Bis = Zeit.Iso(werte[^1].Bis) },
                Einheiten = EinheitenPrognose,
                Bloecke = bloecke,
                Tage = tage,
            },
            [Vermerk(Ressource.Prognose, abruf.Reihe.Referenzzeit)],
            Hinweise(aufloesung, abruf));
    }

    /// <summary>Tool <c>niederschlag_jetzt</c>.</summary>
    public async Task<Teilantwort> NiederschlagJetztAsync(string ort, CancellationToken ct = default)
    {
        var aufloesung = orte.Loese(ort);
        return aufloesung.Eindeutig
            ? await KurzfristAsync(aufloesung, ct)
            : Teilantwort.OrtUnklar(aufloesung, statistik.Vermerk, Hinweis);
    }

    /// <summary>Wetterteil von <c>lage_am_ort</c>: aktuelle Werte, nächste 3 h und nächste 24 h.</summary>
    public async Task<Teilantwort> LageAsync(OrtAufloesung aufloesung, CancellationToken ct = default)
    {
        var gemeinde = aufloesung.Gemeinde!;
        var kurzfrist = await KurzfristAsync(aufloesung, ct);
        var abruf = await dienst.HoleAsync(Ressource.Prognose, aufloesung.Punkt, ct);
        var werte = abruf.Reihe is null ? [] : Stundenwerte(abruf.Reihe, 24);

        object? tag = null;
        var text = "";
        var quellen = kurzfrist.Quellen.ToList();
        if (werte.Count > 0)
        {
            tag = new
            {
                Zeitraum = new { Von = Zeit.Iso(werte[0].Von), Bis = Zeit.Iso(werte[^1].Bis) },
                TempMin = Min(werte, w => w.Temperatur),
                TempMax = Max(werte, w => w.Temperatur),
                NiederschlagMm = Summe(werte, w => w.Niederschlag),
                BoeMaxKmh = Max(werte, w => w.Boe, 0),
                Einheiten = EinheitenPrognose,
            };
            text = $" Nächste {werte.Count} h: {Text(Min(werte, w => w.Temperatur))} bis {Text(Max(werte, w => w.Temperatur))} °C, "
                 + $"{Text(Summe(werte, w => w.Niederschlag))} mm Niederschlag, Böen bis {Text(Max(werte, w => w.Boe, 0), 0)} km/h.";
            if (!quellen.Any(q => q.Datensatz == Ressource.Prognose.Id))
            {
                quellen.Add(Vermerk(Ressource.Prognose, abruf.Reihe!.Referenzzeit));
            }
        }

        return new Teilantwort(
            kurzfrist.Zusammenfassung + text,
            new { Kurzfrist = kurzfrist.Daten, Naechste24h = tag },
            quellen,
            kurzfrist.Hinweise.Union(Hinweise(aufloesung, abruf)).ToList());
    }

    // Nächste 3 h aus dem Nowcast; ist dessen Lauf älter als 1 h oder fehlt er, aus der Stundenprognose.
    private async Task<Teilantwort> KurzfristAsync(OrtAufloesung aufloesung, CancellationToken ct)
    {
        var gemeinde = aufloesung.Gemeinde!;
        var jetzt = zeit.GetUtcNow();

        // Ist der Lauf laut Metadaten zu alt, wird der Nowcast gar nicht erst abgerufen.
        var metadaten = await dienst.MetadatenAsync(Ressource.Nowcast, ct);
        var lauf = metadaten?.LetzterLauf;
        var veraltet = lauf is { } l && jetzt - l > NowcastHoechstalter;
        Abruf? nowcast = null;
        if (!veraltet)
        {
            nowcast = await dienst.HoleAsync(Ressource.Nowcast, aufloesung.Punkt, ct);
            if (nowcast.Reihe is { } reihe)
            {
                lauf = reihe.Referenzzeit;
                veraltet = jetzt - reihe.Referenzzeit > NowcastHoechstalter;
            }
        }

        if (!veraltet && nowcast?.Reihe is { } aktuell)
        {
            var schritte = Enumerable.Range(0, aktuell.Zeitpunkte.Count)
                .Where(i => aktuell.Zeitpunkte[i] > jetzt)
                .Take(SchritteIn3Stunden)
                .Select(i => new Schritt(
                    aktuell.Zeitpunkte[i],
                    aktuell.Wert("rr", i) is { } rr ? Umrechnung.MmAusKgProM2(rr) : null,
                    aktuell.Wert("t2m", i),
                    aktuell.Wert("ff", i) is { } ff ? Umrechnung.KmhAusMs(ff) : null,
                    aktuell.Wert("fx", i) is { } fx ? Umrechnung.KmhAusMs(fx) : null,
                    aktuell.Wert("pt", i)))
                .ToList();
            if (schritte.Count > 0)
            {
                return Kurzfrist(gemeinde, aufloesung, nowcast, schritte, Ressource.Nowcast, "15 min", lauf, nowcastAktuell: true, []);
            }
        }

        var hinweise = new List<string>();
        if (veraltet)
        {
            hinweise.Add($"{HinweisNowcastAlt} (letzter Lauf {Zeit.Kurz(lauf!.Value)}); Werte aus der stündlichen Prognose.");
        }
        else
        {
            hinweise.Add(nowcast?.WirdGeladen == true
                ? "Nowcast wird noch geladen, weil GeoSphere langsam antwortet; vorerst Werte aus der stündlichen Prognose. Dieselbe Frage liefert in Kürze die 15-Minuten-Werte."
                : "Nowcast derzeit nicht verfügbar; Werte aus der stündlichen Prognose.");
        }

        var prognose = await dienst.HoleAsync(Ressource.Prognose, aufloesung.Punkt, ct);
        var stunden = prognose.Reihe is null ? [] : Stundenwerte(prognose.Reihe, 3);
        if (prognose.Reihe is null || stunden.Count == 0)
        {
            var leer = OhneDaten(gemeinde, "Niederschlagsvorhersage", Ressource.Prognose, prognose, aufloesung);
            return leer with { Hinweise = hinweise.Concat(leer.Hinweise).ToList() };
        }

        var ersatz = stunden
            .Select(w => new Schritt(w.Bis, w.Niederschlag, w.Temperatur, w.Wind?.Kmh, w.Boe, null))
            .ToList();
        return Kurzfrist(gemeinde, aufloesung, prognose, ersatz, Ressource.Prognose, "1 h", lauf, nowcastAktuell: false, hinweise);
    }

    private Teilantwort Kurzfrist(
        Gemeinde gemeinde, OrtAufloesung aufloesung, Abruf abruf, List<Schritt> schritte, Ressource ressource,
        string aufloesungZeit, DateTimeOffset? nowcastLauf, bool nowcastAktuell, List<string> hinweise)
    {
        var summe = Math.Round(schritte.Sum(s => s.Niederschlag ?? 0), 1);
        var erster = schritte.FirstOrDefault(s => s.Niederschlag >= 0.05);
        var bis = Zeit.Lokal(schritte[^1].Bis).ToString("HH:mm");
        var temperatur = schritte[0].Temperatur is { } t ? $" {Text(Math.Round(t, 1))} °C;" : "";
        var zusammenfassung = summe > 0 && erster is not null
            ? $"{gemeinde.Name}:{temperatur} bis {bis} Uhr {Text(summe)} mm Niederschlag, erstmals im Intervall bis {Zeit.Lokal(erster.Bis):HH:mm} Uhr."
            : $"{gemeinde.Name}:{temperatur} bis {bis} Uhr kein Niederschlag vorhergesagt.";

        return new Teilantwort(
            zusammenfassung,
            new
            {
                Ort = OrtAngabe(gemeinde, aufloesung.Punkt, abruf.Reihe!),
                NowcastAktuell = nowcastAktuell,
                NowcastLauf = Zeit.Iso(nowcastLauf),
                Datensatz = ressource.Id,
                Lauf = Zeit.Iso(abruf.Reihe!.Referenzzeit),
                Schrittweite = aufloesungZeit,
                Einheiten = EinheitenKurzfrist,
                NiederschlagSummeMm = summe,
                Schritte = schritte.Select(s => new
                {
                    Bis = Zeit.Iso(s.Bis),
                    NiederschlagMm = Runde(s.Niederschlag, 2),
                    Temperatur = Runde(s.Temperatur, 1),
                    WindKmh = Runde(s.Wind, 0),
                    BoeKmh = Runde(s.Boe, 0),
                    NiederschlagsartCode = s.Niederschlag > 0 ? (int?)s.Art : null,
                }).ToList(),
            },
            [Vermerk(ressource, abruf.Reihe.Referenzzeit)],
            hinweise.Concat(Hinweise(aufloesung, abruf)).ToList());
    }

    private Teilantwort OhneDaten(Gemeinde gemeinde, string was, Ressource ressource, Abruf abruf, OrtAufloesung aufloesung) => new(
        $"{gemeinde.Name}: {was} derzeit nicht verfügbar. {abruf.Fehler}".TrimEnd(),
        new { Ort = new { gemeinde.Name, gemeinde.Gkz, gemeinde.Bundesland } },
        [Vermerk(ressource, null)],
        Hinweise(aufloesung, abruf));

    private static List<string> Hinweise(OrtAufloesung aufloesung, Abruf abruf)
    {
        var hinweise = new List<string>();
        if (aufloesung.Hinweis is not null)
        {
            hinweise.Add(aufloesung.Hinweis);
        }

        if (abruf.NurCache)
        {
            hinweise.Add(abruf.Abgerufen is { } stand
                ? $"Anfragekontingent bei GeoSphere fast ausgeschöpft: Antwort stammt nur aus dem Cache (abgerufen {Zeit.Kurz(stand)})."
                : "Anfragekontingent bei GeoSphere fast ausgeschöpft: es wird nur aus dem Cache geantwortet, für diesen Ort liegt nichts vor.");
        }

        if (abruf.Veraltet && abruf.Abgerufen is { } alt)
        {
            hinweise.Add(abruf.WirdGeladen
                ? $"Stand aus dem Cache vom {Zeit.Kurz(alt)}; neuere Daten werden gerade geladen."
                : $"Stand aus dem Cache vom {Zeit.Kurz(alt)}; eine Aktualisierung war nicht möglich.");
        }

        hinweise.Add(Hinweis);
        return hinweise;
    }

    private static object OrtAngabe(Gemeinde gemeinde, Koordinate punkt, Zeitreihe reihe) => new
    {
        gemeinde.Name,
        gemeinde.Gkz,
        gemeinde.Bundesland,
        Punkt = punkt.Gerundet(2).ToString(),
        Gitterpunkt = reihe.Gitterpunkt.Gerundet(4).ToString(),
    };

    // Die nächsten Stundenintervalle, die noch nicht vorbei sind, in Ortszeit.
    private List<Stundenwert> Stundenwerte(Zeitreihe reihe, int anzahl)
    {
        var jetzt = zeit.GetUtcNow();
        var stunde = TimeSpan.FromHours(1);
        var werte = new List<Stundenwert>();
        for (var i = 0; i < reihe.Zeitpunkte.Count && werte.Count < anzahl; i++)
        {
            var bis = reihe.Zeitpunkte[i];
            if (bis <= jetzt)
            {
                continue;
            }

            var u = reihe.Wert("10u", i);
            var v = reihe.Wert("10v", i);
            werte.Add(new Stundenwert(
                Zeit.Lokal(bis - stunde),
                Zeit.Lokal(bis),
                reihe.Wert("2t", i),
                reihe.Wert("tp", i) is { } tp ? Umrechnung.MmAusKgProM2(tp) : null,
                reihe.Wert("rain", i) is { } regen ? Umrechnung.MmAusKgProM2(regen) : null,
                reihe.Wert("sf", i) is { } schnee ? Umrechnung.MmAusKgProM2(schnee) : null,
                reihe.Wert("snowlmt", i),
                u is not null && v is not null ? (u.Value, v.Value, Umrechnung.Wind(u.Value, v.Value).Kmh) : null,
                reihe.Wert("10fg", i) is { } boe ? Umrechnung.KmhAusMs(boe) : null,
                reihe.Wert("tcc", i),
                reihe.Wert("sund", i) is { } sonne ? Umrechnung.MinutenAusSekunden(sonne) : null));
        }

        return werte;
    }

    private static object Block(List<Stundenwert> stunden)
    {
        // Mittlere Richtung aus dem Vektormittel, mittlere Geschwindigkeit aus den Beträgen.
        var wind = stunden.Where(s => s.Wind is not null).Select(s => s.Wind!.Value).ToList();
        var richtung = wind.Count > 0
            ? Umrechnung.Wind(wind.Average(w => w.U), wind.Average(w => w.V)).RichtungGrad
            : null;
        return new
        {
            Von = Zeit.Iso(stunden[0].Von),
            Bis = Zeit.Iso(stunden[^1].Bis),
            TempMin = Min(stunden, s => s.Temperatur),
            TempMax = Max(stunden, s => s.Temperatur),
            NiederschlagMm = Summe(stunden, s => s.Niederschlag),
            RegenMm = Summe(stunden, s => s.Regen),
            SchneeMm = Summe(stunden, s => s.Schnee),
            BoeMaxKmh = Max(stunden, s => s.Boe, 0),
            WindKmh = wind.Count > 0 ? Math.Round(wind.Average(w => w.Kmh), 0) : (double?)null,
            Windrichtung = richtung is { } grad ? Umrechnung.Himmelsrichtung(grad) : null,
            SchneefallgrenzeMinM = Min(stunden, s => s.Schneefallgrenze, 0),
            SchneefallgrenzeMaxM = Max(stunden, s => s.Schneefallgrenze, 0),
            BewoelkungProzent = Mittel(stunden, s => s.Bewoelkung),
            SonneMin = Summe(stunden, s => s.Sonne, 0),
        };
    }

    private static double? Min<T>(IEnumerable<T> werte, Func<T, double?> wahl, int stellen = 1) =>
        Runde(werte.Select(wahl).Where(w => w is not null).Min(), stellen);

    private static double? Max<T>(IEnumerable<T> werte, Func<T, double?> wahl, int stellen = 1) =>
        Runde(werte.Select(wahl).Where(w => w is not null).Max(), stellen);

    private static double? Summe<T>(IEnumerable<T> werte, Func<T, double?> wahl, int stellen = 1)
    {
        var vorhanden = werte.Select(wahl).Where(w => w is not null).ToList();
        return vorhanden.Count == 0 ? null : Runde(vorhanden.Sum(), stellen);
    }

    private static double? Mittel<T>(IEnumerable<T> werte, Func<T, double?> wahl)
    {
        var vorhanden = werte.Select(wahl).Where(w => w is not null).ToList();
        return vorhanden.Count == 0 ? null : Runde(vorhanden.Average(), 0);
    }

    private static double? Runde(double? wert, int stellen) =>
        wert is { } w ? Math.Round(w, stellen, MidpointRounding.AwayFromZero) : null;

    private static string Text(double? wert, int stellen = 1) => wert is { } w ? Zeit.Zahl(w, stellen) : "–";

    private sealed record Stundenwert(
        DateTimeOffset Von,
        DateTimeOffset Bis,
        double? Temperatur,
        double? Niederschlag,
        double? Regen,
        double? Schnee,
        double? Schneefallgrenze,
        (double U, double V, double Kmh)? Wind,
        double? Boe,
        double? Bewoelkung,
        double? Sonne);

    private sealed record Schritt(
        DateTimeOffset Bis, double? Niederschlag, double? Temperatur, double? Wind, double? Boe, double? Art);
}
