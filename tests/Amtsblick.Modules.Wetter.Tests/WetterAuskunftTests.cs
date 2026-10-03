using System.Net;
using static Amtsblick.Modules.Wetter.Tests.Wetterumgebung;

namespace Amtsblick.Modules.Wetter.Tests;

public class WetterPrognoseTests
{
    [Fact]
    public async Task Prognose_fuer_Steyr_liefert_Bloecke_Tage_Einheiten_Vermerk_und_Hinweis()
    {
        var umgebung = new Wetterumgebung();

        var teil = await umgebung.Auskunft.PrognoseAsync("Steyr");
        var antwort = Json(teil);

        // 48 Stunden ab 21:00 Ortszeit; Werte aus der Fixture nachgerechnet.
        Assert.Equal(
            "Steyr (Oberösterreich), nächste 48 h: 8,1 bis 22,8 °C, Niederschlag 0,0 mm, stärkste Böe 17 km/h. Prognoselauf 02.10. 14:00.",
            teil.Zusammenfassung);
        Assert.Equal("40201", antwort.GetProperty("ort").GetProperty("gkz").GetString());
        Assert.Equal("2026-10-02T14:00+02:00", antwort.GetProperty("lauf").GetString());
        Assert.Equal("2026-10-02T21:00+02:00", antwort.GetProperty("zeitraum").GetProperty("von").GetString());
        Assert.Equal("2026-10-04T21:00+02:00", antwort.GetProperty("zeitraum").GetProperty("bis").GetString());

        var einheiten = antwort.GetProperty("einheiten");
        Assert.Equal("°C", einheiten.GetProperty("temperatur").GetString());
        Assert.Equal("mm", einheiten.GetProperty("niederschlag").GetString());
        Assert.Equal("km/h", einheiten.GetProperty("boe").GetString());
        Assert.Equal("km/h", einheiten.GetProperty("wind").GetString());
        Assert.Equal("m", einheiten.GetProperty("schneefallgrenze").GetString());
        Assert.Equal("min", einheiten.GetProperty("sonne").GetString());

        // Erster Block ist der Rest des Abends: 21 bis 24 Uhr.
        var bloecke = antwort.GetProperty("bloecke");
        Assert.Equal(9, bloecke.GetArrayLength());
        var abend = bloecke[0];
        Assert.Equal("2026-10-02T21:00+02:00", abend.GetProperty("von").GetString());
        Assert.Equal("2026-10-03T00:00+02:00", abend.GetProperty("bis").GetString());
        Assert.Equal(13.5, abend.GetProperty("temp_min").GetDouble());
        Assert.Equal(15.2, abend.GetProperty("temp_max").GetDouble());
        Assert.Equal(0, abend.GetProperty("niederschlag_mm").GetDouble());
        Assert.Equal(3, abend.GetProperty("boe_max_kmh").GetDouble());
        Assert.Equal(3224, abend.GetProperty("schneefallgrenze_min_m").GetDouble());
        Assert.Equal(3235, abend.GetProperty("schneefallgrenze_max_m").GetDouble());
        Assert.Equal(0, abend.GetProperty("sonne_min").GetDouble());

        var tage = antwort.GetProperty("tage");
        Assert.Equal(3, tage.GetArrayLength());
        Assert.Equal("2026-10-03", tage[1].GetProperty("datum").GetString());
        Assert.Equal(24, tage[1].GetProperty("stunden").GetInt32());
        Assert.Equal(8.1, tage[1].GetProperty("temp_min").GetDouble());
        Assert.Equal(22.8, tage[1].GetProperty("temp_max").GetDouble());

        var quelle = antwort.GetProperty("quellen")[0];
        Assert.Equal("Datenquelle: GeoSphere Austria - https://data.hub.geosphere.at", quelle.GetProperty("vermerk").GetString());
        Assert.Equal("https://data.hub.geosphere.at", quelle.GetProperty("link").GetString());
        Assert.Equal("nwp-v2-1h-1km", quelle.GetProperty("datensatz").GetString());
        Assert.Equal("CC BY 4.0", quelle.GetProperty("lizenz").GetString());
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/deed.de", quelle.GetProperty("lizenz_link").GetString());
        Assert.Equal("https://doi.org/10.60669/rv80-9d61", quelle.GetProperty("doi").GetString());
        Assert.Contains("Einheiten umgerechnet", quelle.GetProperty("bearbeitung").GetString());
        Assert.Equal("Keine amtliche Unwetterwarnung.", Hinweise(antwort)[^1]);
    }

    [Fact]
    public async Task Abgerufen_wird_der_auf_Hundertstelgrad_gerundete_Gemeindemittelpunkt_mit_genau_den_vorgesehenen_Parametern()
    {
        var umgebung = new Wetterumgebung();

        await umgebung.Auskunft.PrognoseAsync("Steyr");

        var anfrage = umgebung.Netz.Anfragen.Single(a => a.Contains(PrognoseDaten));
        Assert.Matches(@"lat_lon=48\.\d{1,2},14\.\d{1,2}&", anfrage);
        Assert.Contains("parameters=2t,tp,rain,sf,snowlmt,10u,10v,10fg,tcc,sund&", anfrage);
        Assert.DoesNotContain("sy", anfrage[anfrage.IndexOf('?')..]);
    }

    [Fact]
    public async Task Stundenzahl_begrenzt_den_Zeitraum()
    {
        var umgebung = new Wetterumgebung();

        var antwort = Json(await umgebung.Auskunft.PrognoseAsync("Steyr", stunden: 6));

        Assert.Equal("2026-10-03T03:00+02:00", antwort.GetProperty("zeitraum").GetProperty("bis").GetString());
        Assert.Equal(2, antwort.GetProperty("bloecke").GetArrayLength());
    }

    [Fact]
    public async Task Zwei_Abfragen_zur_selben_Gemeinde_innerhalb_der_TTL_erzeugen_genau_einen_Upstream_Abruf()
    {
        var umgebung = new Wetterumgebung();

        await umgebung.Auskunft.PrognoseAsync("Steyr");
        umgebung.Zeit.Advance(TimeSpan.FromMinutes(10));
        await umgebung.Auskunft.PrognoseAsync("40201", stunden: 24);

        Assert.Equal(1, umgebung.Netz.Anzahl(PrognoseDaten));
        Assert.Equal(1, umgebung.Netz.Anzahl(PrognoseMetadaten));
        Assert.Equal(2, umgebung.Netz.Anfragen.Count);
    }

    [Fact]
    public async Task Gleichzeitige_Abfragen_teilen_sich_einen_Upstream_Abruf()
    {
        var umgebung = new Wetterumgebung();

        await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => umgebung.Auskunft.PrognoseAsync("Steyr")));

        Assert.Equal(1, umgebung.Netz.Anzahl(PrognoseDaten));
    }

    [Fact]
    public async Task Nach_Ablauf_von_drei_Stunden_wird_neu_abgerufen()
    {
        var umgebung = new Wetterumgebung();

        await umgebung.Auskunft.PrognoseAsync("Steyr");
        umgebung.Zeit.Advance(TimeSpan.FromMinutes(179));
        await umgebung.Auskunft.PrognoseAsync("Steyr");
        Assert.Equal(1, umgebung.Netz.Anzahl(PrognoseDaten));

        umgebung.Zeit.Advance(TimeSpan.FromMinutes(2));
        await umgebung.Auskunft.PrognoseAsync("Steyr");
        Assert.Equal(2, umgebung.Netz.Anzahl(PrognoseDaten));
    }

    [Fact]
    public async Task Ein_neuer_Lauf_laut_Metadaten_ersetzt_den_Cache_vor_Ablauf_der_TTL()
    {
        var umgebung = new Wetterumgebung();
        await umgebung.Auskunft.PrognoseAsync("Steyr");

        // Metadaten werden höchstens alle 15 Minuten geprüft.
        umgebung.PrognoseLauf = "2026-10-02T15:00+00:00";
        umgebung.Zeit.Advance(TimeSpan.FromMinutes(14));
        await umgebung.Auskunft.PrognoseAsync("Steyr");
        Assert.Equal(1, umgebung.Netz.Anzahl(PrognoseMetadaten));
        Assert.Equal(1, umgebung.Netz.Anzahl(PrognoseDaten));

        umgebung.Zeit.Advance(TimeSpan.FromMinutes(2));
        await umgebung.Auskunft.PrognoseAsync("Steyr");
        Assert.Equal(2, umgebung.Netz.Anzahl(PrognoseMetadaten));
        Assert.Equal(2, umgebung.Netz.Anzahl(PrognoseDaten));
    }

    [Fact]
    public async Task Zehn_Restanfragen_im_Header_fuehren_zu_Antworten_nur_aus_dem_Cache()
    {
        var umgebung = new Wetterumgebung { RestStunde = 10 };

        // Der erste Abruf geht noch hinaus; erst seine Antwort meldet das knappe Kontingent.
        var erste = Json(await umgebung.Auskunft.PrognoseAsync("Steyr"));
        var abgesetzt = umgebung.Netz.Anfragen.Count;
        Assert.DoesNotContain(Hinweise(erste), h => h.Contains("Cache"));
        Assert.Equal(10, umgebung.Kontingent.Fuer("geosphere")!.RestStunde);
        Assert.True(umgebung.Dienst.KontingentKnapp);

        // Gleicher Ort: aus dem Cache, mit Hinweis.
        var zweite = await umgebung.Auskunft.PrognoseAsync("Steyr");
        Assert.Contains("8,1 bis 22,8 °C", zweite.Zusammenfassung);
        Assert.Contains(zweite.Hinweise, h => h.Contains("Anfragekontingent") && h.Contains("nur aus dem Cache"));

        // Anderer Ort: nichts im Cache, also keine Daten, aber auch keine Anfrage.
        var andere = await umgebung.Auskunft.PrognoseAsync("St. Pölten");
        Assert.Contains("nicht verfügbar", andere.Zusammenfassung);
        Assert.Contains(andere.Hinweise, h => h.Contains("Anfragekontingent"));
        Assert.Contains(WetterAuskunft.Hinweis, andere.Hinweise);

        // Auch Nowcast und Metadaten bleiben aus.
        await umgebung.Auskunft.NiederschlagJetztAsync("Steyr");
        Assert.Equal(abgesetzt, umgebung.Netz.Anfragen.Count);

        // Nach Ablauf des Stundenfensters wird wieder angefragt.
        umgebung.RestStunde = 239;
        umgebung.Zeit.Advance(TimeSpan.FromMinutes(61));
        var spaeter = await umgebung.Auskunft.PrognoseAsync("St. Pölten");
        Assert.DoesNotContain("nicht verfügbar", spaeter.Zusammenfassung);
        Assert.False(umgebung.Dienst.KontingentKnapp);
    }

    [Fact]
    public async Task Mehrdeutiger_Ort_wird_ohne_Abruf_mit_Kandidaten_beantwortet()
    {
        var umgebung = new Wetterumgebung();

        var antwort = Json(await umgebung.Auskunft.PrognoseAsync("Sankt Johann"));

        Assert.Empty(umgebung.Netz.Anfragen);
        Assert.Contains("mehrdeutig", antwort.GetProperty("zusammenfassung").GetString());
        Assert.Equal(8, antwort.GetProperty("kandidaten").GetArrayLength());
        Assert.Equal("Datenquelle: Statistik Austria — data.statistik.gv.at", antwort.GetProperty("quellen")[0].GetProperty("vermerk").GetString());
        Assert.Contains(WetterAuskunft.Hinweis, Hinweise(antwort));
    }

    [Fact]
    public async Task Tippfehler_im_Ort_wird_als_Interpretation_ausgewiesen()
    {
        var umgebung = new Wetterumgebung();

        var teil = await umgebung.Auskunft.PrognoseAsync("Steir");

        Assert.StartsWith("Steyr (Oberösterreich)", teil.Zusammenfassung);
        Assert.Contains(teil.Hinweise, h => h.Contains("interpretiert"));
    }

    [Fact]
    public async Task Stoerung_ohne_Cache_ergibt_eine_Antwort_mit_Vermerk_und_Hinweis_statt_eines_Fehlers()
    {
        var umgebung = new Wetterumgebung { Status = HttpStatusCode.BadGateway };

        var antwort = Json(await umgebung.Auskunft.PrognoseAsync("Steyr"));

        Assert.Contains("nicht verfügbar", antwort.GetProperty("zusammenfassung").GetString());
        Assert.Equal(WetterAuskunft.Namensnennung, antwort.GetProperty("quellen")[0].GetProperty("vermerk").GetString());
        Assert.Contains(WetterAuskunft.Hinweis, Hinweise(antwort));
    }

    [Fact]
    public async Task Stoerung_mit_abgelaufenem_Cache_liefert_den_letzten_Stand_und_sagt_das()
    {
        var umgebung = new Wetterumgebung();
        await umgebung.Auskunft.PrognoseAsync("Steyr");

        umgebung.Status = HttpStatusCode.ServiceUnavailable;
        umgebung.Zeit.Advance(TimeSpan.FromHours(4));
        var teil = await umgebung.Auskunft.PrognoseAsync("Steyr");

        Assert.StartsWith("Steyr (Oberösterreich)", teil.Zusammenfassung);
        Assert.Contains(teil.Hinweise, h => h.Contains("Aktualisierung war nicht möglich"));
    }
}

public class NiederschlagJetztTests
{
    [Fact]
    public async Task Aktueller_Nowcast_liefert_Viertelstundenschritte()
    {
        var umgebung = new Wetterumgebung();

        var teil = await umgebung.Auskunft.NiederschlagJetztAsync("Steyr");
        var antwort = Json(teil);

        Assert.Equal("Steyr: 14,8 °C; bis 23:30 Uhr kein Niederschlag vorhergesagt.", teil.Zusammenfassung);
        Assert.True(antwort.GetProperty("nowcast_aktuell").GetBoolean());
        Assert.Equal("2026-10-02T20:30+02:00", antwort.GetProperty("nowcast_lauf").GetString());
        Assert.Equal("nowcast-v1-15min-1km", antwort.GetProperty("datensatz").GetString());
        Assert.Equal("15 min", antwort.GetProperty("schrittweite").GetString());
        Assert.Equal("mm", antwort.GetProperty("einheiten").GetProperty("niederschlag").GetString());

        var schritte = antwort.GetProperty("schritte");
        Assert.Equal(10, schritte.GetArrayLength());
        Assert.Equal("2026-10-02T21:15+02:00", schritte[0].GetProperty("bis").GetString());
        Assert.Equal(0, schritte[0].GetProperty("niederschlag_mm").GetDouble());
        Assert.Equal(14.8, schritte[0].GetProperty("temperatur").GetDouble());
        Assert.Equal(5, schritte[0].GetProperty("wind_kmh").GetDouble());
        Assert.Equal(7, schritte[0].GetProperty("boe_kmh").GetDouble());

        Assert.Equal("nowcast-v1-15min-1km", antwort.GetProperty("quellen")[0].GetProperty("datensatz").GetString());
        Assert.Equal("Einheiten umgerechnet (kg/m² in mm, m/s in km/h), Werte gerundet", antwort.GetProperty("quellen")[0].GetProperty("bearbeitung").GetString());
        Assert.DoesNotContain(Hinweise(antwort), h => h.Contains(WetterAuskunft.HinweisNowcastAlt));
        Assert.Contains(WetterAuskunft.Hinweis, Hinweise(antwort));
        Assert.Equal(0, umgebung.Netz.Anzahl(PrognoseDaten));
    }

    [Fact]
    public async Task Veralteter_Lauf_laut_Metadaten_wird_gekennzeichnet_und_durch_die_Prognose_ersetzt()
    {
        // Letzter Nowcast-Lauf 18:30 UTC, jetzt 21:00 UTC: zweieinhalb Stunden alt.
        var umgebung = new Wetterumgebung();
        umgebung.Zeit.SetUtcNow(new DateTimeOffset(2026, 10, 2, 21, 0, 0, TimeSpan.Zero));

        var antwort = Json(await umgebung.Auskunft.NiederschlagJetztAsync("Steyr"));

        Assert.False(antwort.GetProperty("nowcast_aktuell").GetBoolean());
        Assert.Equal("2026-10-02T20:30+02:00", antwort.GetProperty("nowcast_lauf").GetString());
        Assert.Contains(Hinweise(antwort), h => h.StartsWith("Nowcast derzeit nicht aktuell"));
        Assert.Equal("nwp-v2-1h-1km", antwort.GetProperty("datensatz").GetString());
        Assert.Equal("1 h", antwort.GetProperty("schrittweite").GetString());
        Assert.Equal(3, antwort.GetProperty("schritte").GetArrayLength());
        Assert.Equal("2026-10-03T00:00+02:00", antwort.GetProperty("schritte")[0].GetProperty("bis").GetString());
        Assert.Equal("nwp-v2-1h-1km", antwort.GetProperty("quellen")[0].GetProperty("datensatz").GetString());
        Assert.Contains(WetterAuskunft.Hinweis, Hinweise(antwort));

        // Der veraltete Nowcast wird gar nicht erst geholt.
        Assert.Equal(0, umgebung.Netz.Anzahl(NowcastDaten));
        Assert.Equal(1, umgebung.Netz.Anzahl(PrognoseDaten));
    }

    [Fact]
    public async Task Veralteter_Lauf_wird_auch_ohne_Metadaten_an_der_Referenzzeit_der_Daten_erkannt()
    {
        var umgebung = new Wetterumgebung { MetadatenStatus = HttpStatusCode.InternalServerError };
        umgebung.Zeit.SetUtcNow(new DateTimeOffset(2026, 10, 2, 19, 31, 0, TimeSpan.Zero));

        var antwort = Json(await umgebung.Auskunft.NiederschlagJetztAsync("Steyr"));

        Assert.False(antwort.GetProperty("nowcast_aktuell").GetBoolean());
        Assert.Contains(Hinweise(antwort), h => h.StartsWith("Nowcast derzeit nicht aktuell"));
        Assert.Equal("nwp-v2-1h-1km", antwort.GetProperty("datensatz").GetString());
    }

    [Fact]
    public async Task Genau_eine_Stunde_alter_Lauf_gilt_noch_als_aktuell()
    {
        var umgebung = new Wetterumgebung();
        umgebung.Zeit.SetUtcNow(new DateTimeOffset(2026, 10, 2, 19, 30, 0, TimeSpan.Zero));

        var antwort = Json(await umgebung.Auskunft.NiederschlagJetztAsync("Steyr"));

        Assert.True(antwort.GetProperty("nowcast_aktuell").GetBoolean());
    }

    [Fact]
    public async Task Metadaten_werden_hoechstens_alle_15_Minuten_geprueft()
    {
        var umgebung = new Wetterumgebung();

        await umgebung.Auskunft.NiederschlagJetztAsync("Steyr");
        umgebung.Zeit.Advance(TimeSpan.FromMinutes(14));
        await umgebung.Auskunft.NiederschlagJetztAsync("Garsten");
        Assert.Equal(1, umgebung.Netz.Anzahl(NowcastMetadaten));

        umgebung.Zeit.Advance(TimeSpan.FromMinutes(2));
        await umgebung.Auskunft.NiederschlagJetztAsync("Steyr");
        Assert.Equal(2, umgebung.Netz.Anzahl(NowcastMetadaten));
    }

    [Fact]
    public async Task Lage_verbindet_Nowcast_und_Prognose_mit_beiden_Quellen()
    {
        var umgebung = new Wetterumgebung();
        var orte = new Amtsblick.Core.Ort.OrtResolver(Amtsblick.Tests.Testgemeinden.Verzeichnis());

        var teil = await umgebung.Auskunft.LageAsync(orte.Loese("Steyr"));

        Assert.StartsWith("Steyr: 14,8 °C; bis 23:30 Uhr kein Niederschlag vorhergesagt. Nächste 24 h: 8,1 bis 22,8 °C", teil.Zusammenfassung);
        Assert.Equal(new[] { "nowcast-v1-15min-1km", "nwp-v2-1h-1km" }, teil.Quellen.Select(q => q.Datensatz));
        Assert.Single(teil.Hinweise, h => h == WetterAuskunft.Hinweis);
    }
}

public class StoerungTests
{
    [Fact]
    public async Task Nach_einem_Fehlschlag_wird_derselbe_Punkt_eine_Minute_lang_nicht_erneut_angefragt()
    {
        var umgebung = new Wetterumgebung { Status = HttpStatusCode.BadGateway };

        await umgebung.Auskunft.PrognoseAsync("Steyr");
        umgebung.Zeit.Advance(TimeSpan.FromSeconds(59));
        var teil = await umgebung.Auskunft.PrognoseAsync("Steyr");
        Assert.Equal(1, umgebung.Netz.Anzahl(PrognoseDaten));
        Assert.Contains("nicht verfügbar", teil.Zusammenfassung);

        umgebung.Status = HttpStatusCode.OK;
        umgebung.Zeit.Advance(TimeSpan.FromSeconds(2));
        teil = await umgebung.Auskunft.PrognoseAsync("Steyr");
        Assert.Equal(2, umgebung.Netz.Anzahl(PrognoseDaten));
        Assert.StartsWith("Steyr (Oberösterreich)", teil.Zusammenfassung);
    }
}

public class LangsameQuelleTests
{
    [Fact]
    public async Task Langsamer_Nowcast_wird_nach_kurzer_Wartezeit_durch_die_Prognose_ersetzt_und_im_Hintergrund_fertig_geladen()
    {
        var umgebung = new Wetterumgebung { NowcastSperre = new TaskCompletionSource() };

        // Der Aufruf wartet auf den Nowcast, bis die Geduld abgelaufen ist.
        var aufruf = umgebung.Auskunft.NiederschlagJetztAsync("Steyr");
        Assert.False(aufruf.IsCompleted);
        umgebung.Zeit.Advance(WetterDienst.Geduld);
        var ersatz = Json(await aufruf.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.False(ersatz.GetProperty("nowcast_aktuell").GetBoolean());
        Assert.Equal("nwp-v2-1h-1km", ersatz.GetProperty("datensatz").GetString());
        Assert.Contains(Hinweise(ersatz), h => h.StartsWith("Nowcast wird noch geladen"));
        Assert.Contains(WetterAuskunft.Hinweis, Hinweise(ersatz));

        // Der zurückgehaltene Abruf wird fertig und füllt den Cache: kein zweiter Abruf nötig.
        umgebung.NowcastSperre.SetResult();
        var danach = Json(await umgebung.Auskunft.NiederschlagJetztAsync("Steyr"));

        Assert.True(danach.GetProperty("nowcast_aktuell").GetBoolean());
        Assert.Equal("15 min", danach.GetProperty("schrittweite").GetString());
        Assert.Equal(1, umgebung.Netz.Anzahl(NowcastDaten));
    }

    [Fact]
    public async Task Waehrend_ein_langsamer_Abruf_laeuft_loest_eine_zweite_Frage_keinen_weiteren_aus()
    {
        var umgebung = new Wetterumgebung { NowcastSperre = new TaskCompletionSource() };

        var erster = umgebung.Auskunft.NiederschlagJetztAsync("Steyr");
        var zweiter = umgebung.Auskunft.NiederschlagJetztAsync("Steyr");
        umgebung.Zeit.Advance(WetterDienst.Geduld);
        await Task.WhenAll(erster, zweiter).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, umgebung.Netz.Anzahl(NowcastDaten));
        umgebung.NowcastSperre.SetResult();
    }

    [Fact]
    public async Task Langsame_Metadaten_halten_die_Prognose_nicht_auf()
    {
        var umgebung = new Wetterumgebung { MetadatenSperre = new TaskCompletionSource() };

        var aufruf = umgebung.Auskunft.PrognoseAsync("Steyr");
        Assert.False(aufruf.IsCompleted);
        umgebung.Zeit.Advance(WetterDienst.MetadatenGeduld);
        var teil = await aufruf.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.StartsWith("Steyr (Oberösterreich), nächste 48 h: 8,1 bis 22,8 °C", teil.Zusammenfassung);
        Assert.Equal(1, umgebung.Netz.Anzahl(PrognoseDaten));
        umgebung.MetadatenSperre.SetResult();
    }
}

public class WetterQuellenTests
{
    [Fact]
    public async Task Quellen_nennt_je_Datensatz_den_letzten_Abruf()
    {
        var umgebung = new Wetterumgebung();
        Assert.All(umgebung.Auskunft.Quellen(), q => Assert.Null(q.LetzterAbruf));

        await umgebung.Auskunft.PrognoseAsync("Steyr");
        var quellen = umgebung.Auskunft.Quellen().ToDictionary(q => q.Datensatz!);

        Assert.Equal("2026-10-02T21:05+02:00", quellen["nwp-v2-1h-1km"].LetzterAbruf);
        Assert.Null(quellen["nowcast-v1-15min-1km"].LetzterAbruf);
    }
}
