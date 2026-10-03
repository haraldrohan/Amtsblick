using Amtsblick.Modules.Wetter.GeoSphere;
using Amtsblick.Tests;

namespace Amtsblick.Modules.Wetter.Tests;

public class GeoSphereParserTests
{
    [Fact]
    public void Prognose_wird_aus_der_gespeicherten_Antwort_gelesen()
    {
        var reihe = GeoSphereParser.LiesZeitreihe(Fixture.Json("nwp-v2-1h-1km.steyr.json"));

        Assert.Equal(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero), reihe.Referenzzeit);
        Assert.Equal(54, reihe.Zeitpunkte.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 19, 0, 0, TimeSpan.Zero), reihe.Zeitpunkte[0]);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero), reihe.Zeitpunkte[^1]);
        Assert.Equal(48.042, reihe.Gitterpunkt.Lat, 3);
        Assert.Equal(14.414, reihe.Gitterpunkt.Lon, 3);
        Assert.All(Ressource.Prognose.Parameter, p => Assert.Equal(54, reihe.Parameter[p].Werte.Count));
        Assert.Equal(16.5, reihe.Wert("2t", 0));
        Assert.Equal(0.013, reihe.Wert("tp", 11));
        Assert.Equal(3235.1, reihe.Wert("snowlmt", 0));
        Assert.Equal("degree Celsius", reihe.Parameter["2t"].Einheit);
        Assert.Equal("kg m-2", reihe.Parameter["tp"].Einheit);
        Assert.Equal("m s-1", reihe.Parameter["10fg"].Einheit);
        Assert.Equal("s", reihe.Parameter["sund"].Einheit);
        Assert.Null(reihe.Wert("gibt-es-nicht", 0));
    }

    [Fact]
    public void Nowcast_wird_aus_der_gespeicherten_Antwort_gelesen()
    {
        var reihe = GeoSphereParser.LiesZeitreihe(Fixture.Json("nowcast-v1-15min-1km.steyr.json"));

        Assert.Equal(new DateTimeOffset(2026, 10, 2, 18, 30, 0, TimeSpan.Zero), reihe.Referenzzeit);
        Assert.Equal(11, reihe.Zeitpunkte.Count);
        Assert.Equal(TimeSpan.FromMinutes(15), reihe.Zeitpunkte[1] - reihe.Zeitpunkte[0]);
        Assert.All(Ressource.Nowcast.Parameter, p => Assert.Equal(11, reihe.Parameter[p].Werte.Count));
        Assert.Equal(15.1, reihe.Wert("t2m", 0));
        Assert.Equal(0.0, reihe.Wert("rr", 0));
        Assert.Equal(1.82, reihe.Wert("fx", 0));
        Assert.Equal(255, reihe.Wert("pt", 0));
    }

    [Fact]
    public void Fehlende_Werte_bleiben_null()
    {
        using var dokument = System.Text.Json.JsonDocument.Parse("""
            {"reference_time":"2026-10-02T12:00+00:00","timestamps":["2026-10-02T13:00+00:00","2026-10-02T14:00+00:00"],
             "features":[{"geometry":{"type":"Point","coordinates":[14.4,48.0]},
               "properties":{"parameters":{"2t":{"name":"t","unit":"degree Celsius","data":[null,12.5]}}}}]}
            """);

        var reihe = GeoSphereParser.LiesZeitreihe(dokument.RootElement);

        Assert.Null(reihe.Wert("2t", 0));
        Assert.Equal(12.5, reihe.Wert("2t", 1));
    }

    [Fact]
    public void Metadaten_der_Prognose_nennen_Lauf_Laenge_und_alle_abgerufenen_Parameter()
    {
        var metadaten = GeoSphereParser.LiesMetadaten(Fixture.Json("nwp-v2-1h-1km.metadata.json"));

        Assert.Equal(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero), metadaten.LetzterLauf);
        Assert.Equal(61, metadaten.Vorhersagelaenge);
        Assert.Equal("1H", metadaten.Frequenz);
        Assert.All(Ressource.Prognose.Parameter, p => Assert.Contains(metadaten.Parameter, m => m.Name == p));
        Assert.Equal("kg m-2", metadaten.Parameter.Single(p => p.Name == "tp").Einheit);
    }

    [Fact]
    public void Metadaten_des_Nowcast_nennen_Lauf_Laenge_und_alle_abgerufenen_Parameter()
    {
        var metadaten = GeoSphereParser.LiesMetadaten(Fixture.Json("nowcast-v1-15min-1km.metadata.json"));

        Assert.Equal(new DateTimeOffset(2026, 10, 2, 18, 30, 0, TimeSpan.Zero), metadaten.LetzterLauf);
        Assert.Equal(13, metadaten.Vorhersagelaenge);
        Assert.Equal("15min", metadaten.Frequenz);
        Assert.All(Ressource.Nowcast.Parameter, p => Assert.Contains(metadaten.Parameter, m => m.Name == p));
    }
}

public class UmrechnungTests
{
    // u weht nach Osten, v nach Norden; die Richtung nennt, woher der Wind kommt.
    [Theory]
    [InlineData(0, -5, 0, "N")]
    [InlineData(-5, 0, 90, "O")]
    [InlineData(0, 5, 180, "S")]
    [InlineData(5, 0, 270, "W")]
    [InlineData(-5, -5, 45, "NO")]
    [InlineData(5, 5, 225, "SW")]
    public void Wind_aus_u_und_v(double u, double v, double grad, string richtung)
    {
        var (kmh, richtungGrad) = Umrechnung.Wind(u, v);

        Assert.Equal(Math.Sqrt(u * u + v * v) * 3.6, kmh, 6);
        Assert.NotNull(richtungGrad);
        Assert.Equal(grad, richtungGrad.Value, 6);
        Assert.Equal(richtung, Umrechnung.Himmelsrichtung(richtungGrad.Value));
    }

    [Fact]
    public void Windstille_hat_keine_Richtung()
    {
        var (kmh, richtung) = Umrechnung.Wind(0, 0);

        Assert.Equal(0, kmh);
        Assert.Null(richtung);
    }

    [Theory]
    [InlineData(350, "N")]
    [InlineData(22.4, "N")]
    [InlineData(22.6, "NO")]
    [InlineData(135, "SO")]
    [InlineData(315, "NW")]
    [InlineData(360, "N")]
    public void Himmelsrichtung_in_acht_Sektoren(double grad, string erwartet)
    {
        Assert.Equal(erwartet, Umrechnung.Himmelsrichtung(grad));
    }

    [Fact]
    public void Einheiten()
    {
        Assert.Equal(36, Umrechnung.KmhAusMs(10), 9);
        Assert.Equal(18, Umrechnung.KmhAusMs(5), 9);
        Assert.Equal(2.5, Umrechnung.MmAusKgProM2(2.5));
        Assert.Equal(60, Umrechnung.MinutenAusSekunden(3600));
        Assert.Equal(0.5, Umrechnung.MinutenAusSekunden(30));
    }
}
