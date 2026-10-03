using static Amtsblick.Modules.Wasser.Tests.Wasserumgebung;

namespace Amtsblick.Modules.Wasser.Tests;

public class PegelAuskunftTests
{
    [Fact]
    public async Task Beim_Import_bekommt_jede_Messstelle_die_GKZ_ihrer_Gemeinde()
    {
        var umgebung = new Wasserumgebung();

        var stand = await umgebung.Dienst.StandAsync();

        var steyr = stand.Messstellen.Single(m => m.Hzbnr == SteyrOrtskai);
        Assert.Equal("40201", steyr.Gkz);
        Assert.Equal("Steyr", steyr.Gemeinde);
        Assert.Equal("Oberösterreich", steyr.Bundesland);

        // Die Testdaten kennen nur drei Gemeinden; Messstellen anderswo bleiben ohne GKZ.
        var bangs = stand.Messstellen.Single(m => m.Hzbnr == 200014);
        Assert.Null(bangs.Gkz);
        Assert.Equal("OGC API Features", stand.Weg);
    }

    [Fact]
    public async Task Enns_in_Steyr_liefert_Wert_Lage_Tendenz_Stand_Vermerk_und_Hinweis()
    {
        var umgebung = new Wasserumgebung();

        var teil = await umgebung.Auskunft.InDerNaeheAsync("Steyr");
        var antwort = Json(teil);

        Assert.Contains("im Umkreis von 15 km um Steyr", teil.Zusammenfassung);
        Assert.Contains("nächste: Steyr (Ortskai), Gewässer Enns: Durchfluss 134 m³/s, Mittelwasser, Tendenz normal.", teil.Zusammenfassung);
        Assert.EndsWith("Stand des Abrufs: 02.10. 21:07.", teil.Zusammenfassung);
        Assert.Equal("2026-10-02T21:07+02:00", antwort.GetProperty("stand_abruf").GetString());
        Assert.False(antwort.GetProperty("veraltet").GetBoolean());

        var messstellen = antwort.GetProperty("messstellen").EnumerateArray().ToList();
        var ortskai = messstellen.Single(m => m.GetProperty("hzbnr").GetInt32() == SteyrOrtskai);
        Assert.Equal("Steyr (Ortskai)", ortskai.GetProperty("messstelle").GetString());
        Assert.Equal("Enns", ortskai.GetProperty("gewaesser").GetString());
        Assert.Equal("Steyr", ortskai.GetProperty("gemeinde").GetString());
        Assert.Equal("40201", ortskai.GetProperty("gkz").GetString());
        Assert.Equal("Q (Durchfluss)", ortskai.GetProperty("parameter").GetString());
        Assert.Equal(134, ortskai.GetProperty("wert").GetDouble());
        Assert.Equal("m³/s", ortskai.GetProperty("einheit").GetString());
        Assert.Equal("2026-10-02T19:30+02:00", ortskai.GetProperty("zeitpunkt").GetString());
        Assert.Equal("Mittelwasser", ortskai.GetProperty("lage").GetString());
        Assert.Equal("normal", ortskai.GetProperty("tendenz").GetString());
        Assert.Equal("normal", ortskai.GetProperty("aktualitaet").GetString());
        Assert.Equal(230, ortskai.GetProperty("gesamtcode").GetInt32());
        Assert.InRange(ortskai.GetProperty("entfernung_km").GetDouble(), 0, 5);

        var entfernungen = messstellen.Select(m => m.GetProperty("entfernung_km").GetDouble()).ToList();
        Assert.Equal(entfernungen.Order(), entfernungen);
        Assert.All(entfernungen, km => Assert.InRange(km, 0, 15));

        var quelle = Assert.Single(antwort.GetProperty("quellen").EnumerateArray());
        Assert.Equal("Datenquelle: ehyd.gv.at", quelle.GetProperty("vermerk").GetString());
        Assert.Equal("https://ehyd.gv.at", quelle.GetProperty("link").GetString());
        Assert.Equal("CC BY 4.0", quelle.GetProperty("lizenz").GetString());
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/deed.de", quelle.GetProperty("lizenz_link").GetString());
        Assert.Contains("Statuscode", quelle.GetProperty("bearbeitung").GetString());
        Assert.Equal("2026-10-02T21:07+02:00", quelle.GetProperty("stand").GetString());
        Assert.Equal("Keine amtliche Warnung. Maßgeblich sind die Warndienste des Landes.", Hinweise(antwort)[^1]);
        Assert.Contains(Hinweise(antwort), h => h.Contains("bis zu einer Stunde"));
    }

    [Fact]
    public async Task In_der_Naehe_liefert_hoechstens_20_Messstellen_und_begrenzt_den_Radius()
    {
        var umgebung = new Wasserumgebung();

        var weit = Json(await umgebung.Auskunft.InDerNaeheAsync("Steyr", radiusKm: 5000));
        var eng = Json(await umgebung.Auskunft.InDerNaeheAsync("48.5, 9.0", radiusKm: 1));

        Assert.Equal(20, weit.GetProperty("messstellen").GetArrayLength());
        Assert.Equal(100, weit.GetProperty("radius_km").GetDouble());
        Assert.Contains("liegt in keiner", eng.GetProperty("zusammenfassung").GetString());
    }

    [Fact]
    public async Task Koordinate_als_Ort_misst_die_Entfernung_von_der_Koordinate()
    {
        var umgebung = new Wasserumgebung();

        var antwort = Json(await umgebung.Auskunft.InDerNaeheAsync("48.04331855, 14.42493069", radiusKm: 2));

        var naechste = antwort.GetProperty("messstellen")[0];
        Assert.Equal(SteyrOrtskai, naechste.GetProperty("hzbnr").GetInt32());
        Assert.Equal(0, naechste.GetProperty("entfernung_km").GetDouble());
    }

    [Theory]
    [InlineData("Enns", 7)]
    [InlineData("enns", 7)]
    [InlineData("Steyr", 3)]
    [InlineData("Ramingbach", 1)]
    public async Task An_Gewaesser_liefert_alle_Messstellen_genau_dieses_Gewaessers(string gewaesser, int anzahl)
    {
        var umgebung = new Wasserumgebung();

        var antwort = Json(await umgebung.Auskunft.AnGewaesserAsync(gewaesser));

        var messstellen = antwort.GetProperty("messstellen").EnumerateArray().ToList();
        Assert.Equal(anzahl, messstellen.Count);
        Assert.Single(messstellen.Select(m => m.GetProperty("gewaesser").GetString()).Distinct());
        Assert.StartsWith($"{antwort.GetProperty("gewaesser").GetString()}: {anzahl} Messstelle", antwort.GetProperty("zusammenfassung").GetString());
        Assert.Equal("Datenquelle: ehyd.gv.at", antwort.GetProperty("quellen")[0].GetProperty("vermerk").GetString());
        Assert.Contains(PegelAuskunft.Hinweis, Hinweise(antwort));
    }

    [Fact]
    public async Task Unbekanntes_Gewaesser_liefert_Vorschlaege_statt_zu_raten()
    {
        var umgebung = new Wasserumgebung();

        var teilname = Json(await umgebung.Auskunft.AnGewaesserAsync("Steyrl"));
        var fremd = Json(await umgebung.Auskunft.AnGewaesserAsync("Amazonas"));

        var vorschlaege = teilname.GetProperty("vorschlaege").EnumerateArray().Select(v => v.GetString()).ToList();
        Assert.Contains("Steyrling", vorschlaege);
        Assert.Contains("Krumme Steyrling", vorschlaege);
        Assert.False(teilname.TryGetProperty("messstellen", out _));
        Assert.Contains("Kein Gewässer \"Amazonas\"", fremd.GetProperty("zusammenfassung").GetString());
        Assert.Contains(PegelAuskunft.Hinweis, Hinweise(fremd));
    }

    [Fact]
    public async Task Hochwasserlage_ohne_erhoehte_Pegel_sagt_das_und_zaehlt_Messstellen_ohne_Daten()
    {
        var umgebung = new Wasserumgebung();

        var antwort = Json(await umgebung.Auskunft.HochwasserlageAsync());

        Assert.StartsWith(
            "Österreich: keine der 300 Messstellen meldet erhöhte Wasserführung oder Hochwasser.",
            antwort.GetProperty("zusammenfassung").GetString());
        Assert.Equal(0, antwort.GetProperty("stufen").GetArrayLength());
        Assert.Equal(300, antwort.GetProperty("messstellen_gesamt").GetInt32());
        Assert.Equal(5, antwort.GetProperty("messstellen_ohne_daten").GetInt32());
        var ohneDaten = antwort.GetProperty("ohne_daten").EnumerateArray().ToList();
        Assert.Equal(5, ohneDaten.Count);
        var schladming = ohneDaten.Single(m => m.GetProperty("hzbnr").GetInt32() == 210641);
        Assert.Equal("Schladming", schladming.GetProperty("messstelle").GetString());
        Assert.Equal("Enns", schladming.GetProperty("gewaesser").GetString());
        Assert.Equal("keine Daten", schladming.GetProperty("lage").GetString());
        Assert.Contains("5 Messstellen ohne Daten:", antwort.GetProperty("zusammenfassung").GetString());
        Assert.Contains("Schladming (Enns)", antwort.GetProperty("zusammenfassung").GetString());
        Assert.Equal("2026-10-02T21:07+02:00", antwort.GetProperty("stand_abruf").GetString());
        Assert.Contains(PegelAuskunft.Hinweis, Hinweise(antwort));
    }

    [Fact]
    public async Task Hochwasserlage_gruppiert_ab_Lage_3_nach_Stufe_mit_Tendenz()
    {
        var umgebung = new Wasserumgebung();
        umgebung.Codes[SteyrOrtskai] = 510;
        umgebung.Codes[Jaegerberg] = 320;
        umgebung.Codes[200014] = 400;
        umgebung.Codes[200048] = 700;

        var teil = await umgebung.Auskunft.HochwasserlageAsync();
        var stufen = Json(teil).GetProperty("stufen").EnumerateArray().ToList();

        Assert.StartsWith(
            "Österreich: 1× Hochwasser Stufe 2, 1× Hochwasser Stufe 1, 1× erhöhte Wasserführung (von 300 Messstellen).",
            teil.Zusammenfassung);
        Assert.Equal(
            new[] { "Hochwasser Stufe 2", "Hochwasser Stufe 1", "erhöhte Wasserführung" },
            stufen.Select(s => s.GetProperty("lage").GetString()));
        var stufe2 = stufen[0].GetProperty("messstellen")[0];
        Assert.Equal(SteyrOrtskai, stufe2.GetProperty("hzbnr").GetInt32());
        Assert.Equal("steigend", stufe2.GetProperty("tendenz").GetString());
        Assert.Equal("gleich", stufen[1].GetProperty("messstellen")[0].GetProperty("tendenz").GetString());
        Assert.Equal("sinkend", stufen[2].GetProperty("messstellen")[0].GetProperty("tendenz").GetString());

        // Der undokumentierte Code 700 wird nicht als Hochwasser gedeutet, sondern als unbekannt ausgewiesen.
        var unbekannt = Json(teil).GetProperty("ohne_daten").EnumerateArray().Single(m => m.GetProperty("hzbnr").GetInt32() == 200048);
        Assert.Equal("unbekannt", unbekannt.GetProperty("lage").GetString());
        Assert.Equal(700, unbekannt.GetProperty("gesamtcode").GetInt32());
        Assert.DoesNotContain(stufen, s => s.GetProperty("messstellen").EnumerateArray().Any(m => m.GetProperty("hzbnr").GetInt32() == 200048));
    }

    [Theory]
    [InlineData("Oberösterreich")]
    [InlineData("oberoesterreich")]
    public async Task Hochwasserlage_laesst_sich_auf_ein_Bundesland_einschraenken(string bundesland)
    {
        var umgebung = new Wasserumgebung();
        umgebung.Codes[SteyrOrtskai] = 410;
        umgebung.Codes[200014] = 410;

        var antwort = Json(await umgebung.Auskunft.HochwasserlageAsync(bundesland));

        Assert.Equal("Oberösterreich", antwort.GetProperty("gebiet").GetString());
        var stufe = Assert.Single(antwort.GetProperty("stufen").EnumerateArray());
        Assert.Equal(1, stufe.GetProperty("anzahl").GetInt32());
        Assert.Equal(SteyrOrtskai, stufe.GetProperty("messstellen")[0].GetProperty("hzbnr").GetInt32());
    }

    [Fact]
    public async Task Unbekanntes_Bundesland_wird_ohne_Abruf_abgelehnt()
    {
        var umgebung = new Wasserumgebung();

        var teil = await umgebung.Auskunft.HochwasserlageAsync("Bayern");

        Assert.Empty(umgebung.Netz.Anfragen);
        Assert.Contains("Unbekanntes Bundesland", teil.Zusammenfassung);
        Assert.Contains("Niederösterreich", teil.Zusammenfassung);
        Assert.Contains(PegelAuskunft.Hinweis, teil.Hinweise);
    }

    [Fact]
    public async Task Unbekannter_Ort_wird_ohne_Abruf_mit_Hinweis_beantwortet()
    {
        var umgebung = new Wasserumgebung();

        var teil = await umgebung.Auskunft.InDerNaeheAsync("Xyzzyhausen");

        Assert.Empty(umgebung.Netz.Anfragen);
        Assert.Contains("Kein Ort", teil.Zusammenfassung);
        Assert.Contains(PegelAuskunft.Hinweis, teil.Hinweise);
    }
}

public class ZahlenformatTests
{
    [Theory]
    [InlineData("Garsten", 30)]
    public async Task Zusammenfassung_schreibt_Werte_ohne_angehaengte_Nullen(string ort, double radius)
    {
        var umgebung = new Wasserumgebung();

        var teil = await umgebung.Auskunft.InDerNaeheAsync(ort, radius);

        Assert.DoesNotMatch(@",\d0 (m³/s|cm)", teil.Zusammenfassung);
        Assert.DoesNotMatch(@",00 ", teil.Zusammenfassung);
    }
}
