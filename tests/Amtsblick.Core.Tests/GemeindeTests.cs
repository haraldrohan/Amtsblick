using System.Text.Json;
using Amtsblick.Core.Ort;
using Amtsblick.Tests;

namespace Amtsblick.Core.Tests;

public class GemeindeTests
{
    private static readonly Koordinate PegelSteyrOrtskai = new(48.04331855, 14.42493069);

    [Fact]
    public void Punkt_in_Polygon_ordnet_den_Pegel_Steyr_Ortskai_der_Stadt_Steyr_zu()
    {
        var verzeichnis = new GemeindeVerzeichnis(Testgemeinden.MitGrenzen());

        Assert.Equal("40201", verzeichnis.Finde(PegelSteyrOrtskai)?.Gkz);
        Assert.Null(verzeichnis.Finde(new Koordinate(48.30, 14.29)));
    }

    [Fact]
    public void Mittelpunkt_liegt_in_der_eigenen_Gemeinde()
    {
        Assert.All(Testgemeinden.MitGrenzen(), g => Assert.True(g.Flaeche!.Enthaelt(g.Mittelpunkt), g.Name));
    }

    [Fact]
    public void Mittelpunkt_einer_Sichel_liegt_auf_der_Flaeche_statt_im_Schwerpunkt()
    {
        // U-Form: der Schwerpunkt fällt in die Aussparung.
        Koordinate[] ring =
        [
            new(0, 0), new(0, 3), new(3, 3), new(3, 2), new(1, 2), new(1, 1), new(3, 1), new(3, 0), new(0, 0),
        ];
        var flaeche = new Flaeche([[ring]]);

        Assert.True(flaeche.Enthaelt(flaeche.Mittelpunkt()));
    }

    [Fact]
    public void Loch_im_Polygon_gehoert_nicht_zur_Flaeche()
    {
        Koordinate[] aussen = [new(0, 0), new(0, 4), new(4, 4), new(4, 0), new(0, 0)];
        Koordinate[] loch = [new(1, 1), new(1, 3), new(3, 3), new(3, 1), new(1, 1)];
        var flaeche = new Flaeche([[aussen, loch]]);

        Assert.False(flaeche.Enthaelt(new Koordinate(2, 2)));
        Assert.True(flaeche.Enthaelt(new Koordinate(0.5, 0.5)));
    }

    [Fact]
    public void Wkb_erhaelt_die_Geometrie()
    {
        var steyr = Testgemeinden.MitGrenzen().Single(g => g.Gkz == "40201").Flaeche!;

        var gelesen = Flaeche.AusWkb(steyr.AlsWkb());

        Assert.Equal(steyr.Polygone.Sum(p => p.Sum(r => r.Length)), gelesen.Polygone.Sum(p => p.Sum(r => r.Length)));
        Assert.Equal(steyr.Polygone[0][0][5], gelesen.Polygone[0][0][5]);
        Assert.True(gelesen.Enthaelt(PegelSteyrOrtskai));
    }

    [Fact]
    public void SQLite_speichert_und_laedt_alle_Felder()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"amtsblick-test-{Guid.NewGuid():N}.sqlite");
        try
        {
            GemeindeDatenbank.Speichere(pfad, Testgemeinden.MitGrenzen(), new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow);

            var verzeichnis = GemeindeDatenbank.Lade(pfad);

            Assert.Equal(3, verzeichnis.Alle.Count);
            Assert.Equal(new DateOnly(2026, 1, 1), verzeichnis.Gebietsstand);
            var steyr = verzeichnis.NachGkz("40201")!;
            Assert.Equal("Steyr", steyr.Name);
            Assert.Equal("Stadt Steyr", steyr.Bezirk);
            Assert.Equal("Oberösterreich", steyr.Bundesland);
            Assert.InRange(steyr.Mittelpunkt.Lat, 48.0, 48.1);
            Assert.InRange(steyr.Mittelpunkt.Lon, 14.35, 14.5);
            Assert.Equal("40201", verzeichnis.Finde(PegelSteyrOrtskai)?.Gkz);
        }
        finally
        {
            File.Delete(pfad);
        }
    }

    [Fact]
    public void Fehlende_Datenbank_ergibt_leeres_Verzeichnis()
    {
        Assert.True(GemeindeDatenbank.Lade(Path.Combine(Path.GetTempPath(), "gibt-es-nicht.sqlite")).IstLeer);
    }

    [Fact]
    public void Bezirksnamen_werden_lesbar_geschrieben()
    {
        using var dokument = JsonDocument.Parse("""
            {"features":[
              {"properties":{"g_id":"101","g_name":"Eisenstadt(Stadt)"}},
              {"properties":{"g_id":"923","g_name":"Wien 23.,Liesing"}},
              {"properties":{"g_id":"415","g_name":"Steyr-Land"}}]}
            """);

        var bezirke = GemeindeImport.LiesBezirke(dokument.RootElement);

        Assert.Equal("Eisenstadt (Stadt)", bezirke["101"]);
        Assert.Equal("Wien 23., Liesing", bezirke["923"]);
        Assert.Equal("Steyr-Land", bezirke["415"]);
    }

    [Fact]
    public void Wien_wird_als_Ganzes_ergaenzt_und_der_Bezirk_bleibt_der_genauere_Treffer()
    {
        Koordinate[] innereStadt = [new(48.20, 16.36), new(48.20, 16.38), new(48.22, 16.38), new(48.22, 16.36), new(48.20, 16.36)];
        Koordinate[] leopoldstadt = [new(48.20, 16.38), new(48.20, 16.42), new(48.24, 16.42), new(48.24, 16.38), new(48.20, 16.38)];
        List<Gemeinde> bezirke =
        [
            new("90101", "Wien-Innere Stadt", "Wien 1., Innere Stadt", "Wien", new Koordinate(48.21, 16.37), new Flaeche([[innereStadt]])),
            new("90201", "Wien-Leopoldstadt", "Wien 2., Leopoldstadt", "Wien", new Koordinate(48.22, 16.40), new Flaeche([[leopoldstadt]])),
        ];

        var orte = new OrtResolver(new GemeindeVerzeichnis(GemeindeImport.MitWienGesamt(bezirke)));

        var wien = orte.Loese("Wien");
        Assert.Equal(GemeindeImport.GkzWien, wien.Gemeinde?.Gkz);
        Assert.Equal(new Koordinate(48.21, 16.37), wien.Punkt);
        Assert.Equal("90201", orte.Finde(new Koordinate(48.22, 16.40))?.Gkz);
    }

    [Fact]
    public void Entfernung_Steyr_Linz_betraegt_rund_30_km()
    {
        var km = new Koordinate(48.0427, 14.4213).EntfernungKm(new Koordinate(48.3064, 14.2861));

        Assert.InRange(km, 29, 32);
    }
}
