using Amtsblick.Core.Ort;
using Amtsblick.Tests;

namespace Amtsblick.Core.Tests;

public class OrtResolverTests
{
    private readonly OrtResolver _orte = new(Testgemeinden.Verzeichnis());

    [Fact]
    public void Steyr_trifft_exakt_und_nur_die_Stadt()
    {
        var treffer = _orte.Finde("Steyr");

        var einziger = Assert.Single(treffer);
        Assert.Equal("40201", einziger.Gemeinde.Gkz);
        Assert.Equal(TrefferArt.Exakt, einziger.Art);
        Assert.Equal("Oberösterreich", einziger.Gemeinde.Bundesland);
    }

    [Theory]
    [InlineData("St. Pölten")]
    [InlineData("Sankt Pölten")]
    [InlineData("st poelten")]
    [InlineData("  ST.PÖLTEN ")]
    public void St_und_Sankt_und_Umlaute_sind_gleichwertig(string eingabe)
    {
        var aufloesung = _orte.Loese(eingabe);

        Assert.True(aufloesung.Eindeutig);
        Assert.Equal("30201", aufloesung.Gemeinde!.Gkz);
        Assert.Null(aufloesung.Hinweis);
    }

    [Fact]
    public void Sankt_Johann_ist_mehrdeutig_und_kommt_als_Liste()
    {
        var aufloesung = _orte.Loese("Sankt Johann");

        Assert.False(aufloesung.Eindeutig);
        Assert.Equal(8, aufloesung.Treffer.Count);
        Assert.All(aufloesung.Treffer, t => Assert.Equal(TrefferArt.Teilname, t.Art));
        Assert.Contains(aufloesung.Treffer, t => t.Gemeinde.Name == "St. Johann im Pongau");
        Assert.Contains(aufloesung.Treffer, t => t.Gemeinde.Name == "Sankt Johann im Saggautal");
        Assert.Contains(aufloesung.Treffer, t => t.Gemeinde.Name == "Söding-Sankt Johann");
        Assert.Contains("mehrdeutig", aufloesung.Hinweis);
    }

    [Fact]
    public void Zusatz_mit_Bundesland_macht_mehrdeutigen_Namen_eindeutig()
    {
        Assert.Equal("50418", _orte.Loese("St. Johann, Salzburg").Gemeinde?.Gkz);
        Assert.Equal(2, _orte.Finde("Sankt Johann, Tirol").Count);
    }

    [Theory]
    [InlineData("Steir", "40201")]
    [InlineData("Stery", "40201")]
    [InlineData("Sankt Pölden", "30201")]
    [InlineData("Garstn", "41506")]
    public void Tippfehler_werden_unscharf_gefunden_und_als_Interpretation_gekennzeichnet(string eingabe, string gkz)
    {
        var aufloesung = _orte.Loese(eingabe);

        Assert.True(aufloesung.Eindeutig);
        Assert.Equal(gkz, aufloesung.Gemeinde!.Gkz);
        Assert.Equal(TrefferArt.Unscharf, aufloesung.Treffer[0].Art);
        Assert.Contains("interpretiert", aufloesung.Hinweis);
    }

    [Fact]
    public void Unbekannter_Ort_liefert_nichts()
    {
        var aufloesung = _orte.Loese("Xyzzyhausen");

        Assert.False(aufloesung.Eindeutig);
        Assert.Empty(aufloesung.Treffer);
    }

    [Fact]
    public void Gkz_wird_direkt_aufgeloest()
    {
        Assert.Equal("Garsten", _orte.Loese("41506").Gemeinde?.Name);
    }

    [Fact]
    public void Koordinate_wird_per_Punkt_in_Polygon_aufgeloest_und_bleibt_der_Bezugspunkt()
    {
        // Pegel Steyr (Ortskai) laut eHYD
        var aufloesung = _orte.Loese("48.04331855, 14.42493069");

        Assert.Equal("40201", aufloesung.Gemeinde?.Gkz);
        Assert.Equal(new Koordinate(48.04331855, 14.42493069), aufloesung.Punkt);
    }

    [Fact]
    public void Koordinate_ausserhalb_aller_Gemeinden_ist_nicht_eindeutig()
    {
        var aufloesung = _orte.Loese("48.30, 14.29");

        Assert.False(aufloesung.Eindeutig);
        Assert.Contains("keiner", aufloesung.Hinweis);
    }

    [Fact]
    public void Ohne_importierte_Gemeinden_weist_der_Hinweis_auf_den_Import_hin()
    {
        var aufloesung = new OrtResolver(GemeindeVerzeichnis.Leer).Loese("Steyr");

        Assert.False(aufloesung.Eindeutig);
        Assert.Contains("import", aufloesung.Hinweis, StringComparison.OrdinalIgnoreCase);
    }
}
