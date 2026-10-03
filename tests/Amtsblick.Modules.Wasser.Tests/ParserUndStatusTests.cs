using System.Text.Json;
using Amtsblick.Modules.Wasser.Ehyd;
using Amtsblick.Tests;

namespace Amtsblick.Modules.Wasser.Tests;

public class PegelParserTests
{
    [Fact]
    public void Bestand_wird_aus_der_gespeicherten_Antwort_gelesen()
    {
        var seite = PegelParser.Lies(Fixture.Json("pegel_aktuell.items.json"));

        Assert.Equal(300, seite.Messstellen.Count);
        Assert.Equal(300, seite.Gesamt);
        Assert.Equal(300, seite.Messstellen.Select(m => m.Hzbnr).Distinct().Count());
        Assert.All(seite.Messstellen, m => Assert.NotNull(m.Ort));
        Assert.All(seite.Messstellen, m => Assert.Contains(m.Parameter, new[] { "W", "Q" }));
        Assert.All(seite.Messstellen, m => Assert.InRange(m.Ort!.Value.Lat, 46.3, 49.1));
        Assert.All(seite.Messstellen, m => Assert.InRange(m.Ort!.Value.Lon, 9.5, 17.2));
    }

    [Fact]
    public void Messstelle_Steyr_Ortskai_hat_alle_Felder()
    {
        var steyr = PegelParser.Lies(Fixture.Json("pegel_aktuell.items.json"))
            .Messstellen.Single(m => m.Hzbnr == Wasserumgebung.SteyrOrtskai);

        Assert.Equal("Steyr (Ortskai)", steyr.Name);
        Assert.Equal("Enns", steyr.Gewaesser);
        Assert.Equal("Oberösterreich", steyr.Hydrodienst);
        Assert.Equal("http://hydro.ooe.gv.at/#8910", steyr.Internet);
        Assert.Equal("Q", steyr.Parameter);
        Assert.Equal(134, steyr.Wert);
        Assert.Equal("m³/s", steyr.Einheit);
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 19, 30, 0, TimeSpan.FromHours(2)), steyr.Zeitpunkt);
        Assert.Equal(230, steyr.Status.Code);
        Assert.Equal("Mittelwasser", steyr.Status.Lage);
        Assert.Equal("normal", steyr.Status.Tendenz);
        Assert.Equal("normal", steyr.Status.Aktualitaet);
        Assert.Equal(48.04331855, steyr.Ort!.Value.Lat, 8);
        Assert.Equal(14.42493069, steyr.Ort!.Value.Lon, 8);

        // Diese Felder liefert die OGC-API-Ausgabe nicht.
        Assert.Null(steyr.WertWCm);
        Assert.Null(steyr.Prognose);
        Assert.Null(steyr.Land);
    }

    [Fact]
    public void Messstelle_ohne_Daten_hat_weder_Wert_noch_Zeitpunkt()
    {
        var schladming = PegelParser.Lies(Fixture.Json("pegel_aktuell.items.json"))
            .Messstellen.Single(m => m.Hzbnr == 210641);

        Assert.Null(schladming.Wert);
        Assert.Null(schladming.Zeitpunkt);
        Assert.Equal("", schladming.Einheit);
        Assert.Equal("keine Daten", schladming.Status.Lage);
        Assert.False(schladming.Status.AbErhoehterWasserfuehrung);
    }

    [Fact]
    public void Optionale_Felder_und_Zahlen_als_Text_werden_gelesen()
    {
        using var dokument = JsonDocument.Parse("""
            {"type":"FeatureCollection","numberMatched":2,"features":[
              {"type":"Feature","geometry":null,"properties":{
                "hzbnr":"207035","messstelle":"Korneuburg","gewaesser":"Donau","hydrodienst":"viaDonau",
                "internet":"https://example.org/p","parameter":"W","wert":"312,5","einheit":"cm","wertw_cm":"312.5",
                "zeitpunkt":"2026-10-02T19:30:00+02:00","gesamtcode":"411","lat":"48.3421","lon":"16,3329",
                "prognose":"https://example.org/prognose","land":"Niederösterreich"}},
              {"type":"Feature","geometry":{"type":"Point","coordinates":[48.2,16.4]},"properties":{
                "hzbnr":207241,"messstelle":"Vertauscht","gewaesser":"Donau","parameter":"W","gesamtcode":null}},
              {"type":"Feature","geometry":null,"properties":{"messstelle":"ohne Nummer"}}]}
            """);

        var seite = PegelParser.Lies(dokument.RootElement);

        Assert.Equal(2, seite.Messstellen.Count);
        var korneuburg = seite.Messstellen[0];
        Assert.Equal(207035, korneuburg.Hzbnr);
        Assert.Equal(312.5, korneuburg.Wert);
        Assert.Equal(312.5, korneuburg.WertWCm);
        Assert.Equal(48.3421, korneuburg.Ort!.Value.Lat);
        Assert.Equal(16.3329, korneuburg.Ort!.Value.Lon);
        Assert.Equal("https://example.org/prognose", korneuburg.Prognose);
        Assert.Equal("Niederösterreich", korneuburg.Land);
        Assert.Equal("Hochwasser Stufe 1", korneuburg.Status.Lage);
        Assert.Equal("steigend", korneuburg.Status.Tendenz);
        Assert.Equal("älter als 24 h", korneuburg.Status.Aktualitaet);

        // Achsen in der Reihenfolge Breite, Länge werden an den Wertebereichen erkannt.
        var vertauscht = seite.Messstellen[1];
        Assert.Equal(48.2, vertauscht.Ort!.Value.Lat);
        Assert.Equal(16.4, vertauscht.Ort!.Value.Lon);
        Assert.Equal(Pegelstatus.Unbekannt, vertauscht.Status.Lage);
    }
}

public class PegelstatusTests
{
    [Theory]
    [InlineData(1, "Niederwasser")]
    [InlineData(2, "Mittelwasser")]
    [InlineData(3, "erhöhte Wasserführung")]
    [InlineData(4, "Hochwasser Stufe 1")]
    [InlineData(5, "Hochwasser Stufe 2")]
    [InlineData(6, "Hochwasser Stufe 3")]
    [InlineData(9, "keine Daten")]
    public void Lage_fuer_alle_dokumentierten_Ziffern(int ziffer, string erwartet)
    {
        var status = Pegelstatus.Zerlege(ziffer * 100 + 30);

        Assert.Equal(ziffer, status.LageZiffer);
        Assert.Equal(erwartet, status.Lage);
        Assert.Equal(ziffer is >= 3 and <= 6, status.AbErhoehterWasserfuehrung);
    }

    [Theory]
    [InlineData(0, "gleich")]
    [InlineData(1, "steigend")]
    [InlineData(2, "sinkend")]
    [InlineData(3, "normal")]
    public void Tendenz_fuer_alle_dokumentierten_Ziffern(int ziffer, string erwartet)
    {
        var status = Pegelstatus.Zerlege(200 + ziffer * 10);

        Assert.Equal(ziffer, status.TendenzZiffer);
        Assert.Equal(erwartet, status.Tendenz);
    }

    [Theory]
    [InlineData(0, "normal")]
    [InlineData(1, "älter als 24 h")]
    public void Aktualitaet_fuer_alle_dokumentierten_Ziffern(int ziffer, string erwartet)
    {
        var status = Pegelstatus.Zerlege(230 + ziffer);

        Assert.Equal(ziffer, status.AktualitaetZiffer);
        Assert.Equal(erwartet, status.Aktualitaet);
    }

    [Fact]
    public void Alle_Kombinationen_dokumentierter_Ziffern_werden_vollstaendig_uebersetzt()
    {
        foreach (var lage in new[] { 1, 2, 3, 4, 5, 6, 9 })
        {
            for (var tendenz = 0; tendenz <= 3; tendenz++)
            {
                for (var aktualitaet = 0; aktualitaet <= 1; aktualitaet++)
                {
                    var status = Pegelstatus.Zerlege(lage * 100 + tendenz * 10 + aktualitaet);

                    Assert.NotEqual(Pegelstatus.Unbekannt, status.Lage);
                    Assert.NotEqual(Pegelstatus.Unbekannt, status.Tendenz);
                    Assert.NotEqual(Pegelstatus.Unbekannt, status.Aktualitaet);
                }
            }
        }
    }

    [Theory]
    [InlineData(730, "unbekannt", "normal", "normal")]
    [InlineData(830, "unbekannt", "normal", "normal")]
    [InlineData(250, "Mittelwasser", "unbekannt", "normal")]
    [InlineData(235, "Mittelwasser", "normal", "unbekannt")]
    [InlineData(789, "unbekannt", "unbekannt", "unbekannt")]
    public void Unbekannte_Ziffern_werden_einzeln_als_unbekannt_ausgegeben(int code, string lage, string tendenz, string aktualitaet)
    {
        var status = Pegelstatus.Zerlege(code);

        Assert.Equal(code, status.Code);
        Assert.Equal(lage, status.Lage);
        Assert.Equal(tendenz, status.Tendenz);
        Assert.Equal(aktualitaet, status.Aktualitaet);
        Assert.False(status.AbErhoehterWasserfuehrung);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(23)]
    [InlineData(1000)]
    [InlineData(-230)]
    public void Codes_die_nicht_dreistellig_sind_bleiben_ganz_unbekannt(int? code)
    {
        var status = Pegelstatus.Zerlege(code);

        Assert.Null(status.LageZiffer);
        Assert.Equal(Pegelstatus.Unbekannt, status.Lage);
        Assert.Equal(Pegelstatus.Unbekannt, status.Tendenz);
        Assert.Equal(Pegelstatus.Unbekannt, status.Aktualitaet);
    }
}
