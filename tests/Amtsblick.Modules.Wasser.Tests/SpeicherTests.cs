using System.Net;
using static Amtsblick.Modules.Wasser.Tests.Wasserumgebung;

namespace Amtsblick.Modules.Wasser.Tests;

/// <summary>Der letzte Stand überdauert einen Neustart, damit auch neu gestartete Instanzen bei einem Abruf pro Stunde bleiben.</summary>
public sealed class SpeicherTests : IDisposable
{
    private readonly string _pfad = Path.Combine(Path.GetTempPath(), $"amtsblick-pegel-{Guid.NewGuid():N}", "pegel_aktuell.json");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_pfad)!, recursive: true);

    [Fact]
    public async Task Eine_neu_gestartete_Instanz_liest_den_gespeicherten_Stand_statt_abzurufen()
    {
        var erste = new Wasserumgebung(speicherPfad: _pfad);
        await erste.Auskunft.HochwasserlageAsync();
        Assert.True(File.Exists(_pfad));

        erste.Zeit.Advance(TimeSpan.FromMinutes(30));
        var zweite = new Wasserumgebung(speicherPfad: _pfad, zeit: erste.Zeit);
        var antwort = Json(await zweite.Auskunft.InDerNaeheAsync("Steyr"));

        Assert.Empty(zweite.Netz.Anfragen);
        Assert.Equal("2026-10-02T21:07+02:00", antwort.GetProperty("stand_abruf").GetString());
        var ortskai = antwort.GetProperty("messstellen").EnumerateArray().Single(m => m.GetProperty("hzbnr").GetInt32() == SteyrOrtskai);
        Assert.Equal(134, ortskai.GetProperty("wert").GetDouble());
        Assert.Equal("Mittelwasser", ortskai.GetProperty("lage").GetString());
        Assert.Equal("40201", ortskai.GetProperty("gkz").GetString());
    }

    [Fact]
    public async Task Nach_60_Minuten_ruft_die_neue_Instanz_ab_und_braucht_dafuer_kein_collections_mehr()
    {
        var erste = new Wasserumgebung(speicherPfad: _pfad);
        await erste.Auskunft.HochwasserlageAsync();

        erste.Zeit.Advance(TimeSpan.FromMinutes(60));
        var zweite = new Wasserumgebung(speicherPfad: _pfad, zeit: erste.Zeit);
        await zweite.Auskunft.HochwasserlageAsync();

        Assert.Equal(1, zweite.Netz.Anzahl(Items));
        Assert.Equal(0, zweite.Netz.Anzahl(Collections));
    }

    [Fact]
    public async Task Ein_gescheiterter_Versuch_wird_mitgespeichert_und_bleibt_nach_dem_Neustart_veraltet()
    {
        var erste = new Wasserumgebung(speicherPfad: _pfad);
        await erste.Auskunft.HochwasserlageAsync();
        erste.OgcStatus = HttpStatusCode.ServiceUnavailable;
        erste.WfsStatus = HttpStatusCode.ServiceUnavailable;
        erste.Zeit.Advance(TimeSpan.FromMinutes(61));
        await erste.Auskunft.HochwasserlageAsync();

        erste.Zeit.Advance(TimeSpan.FromMinutes(10));
        var zweite = new Wasserumgebung(speicherPfad: _pfad, zeit: erste.Zeit);
        var antwort = Json(await zweite.Auskunft.HochwasserlageAsync());

        Assert.Empty(zweite.Netz.Anfragen);
        Assert.True(antwort.GetProperty("veraltet").GetBoolean());
        Assert.Equal("2026-10-02T21:07+02:00", antwort.GetProperty("stand_abruf").GetString());
    }

    [Fact]
    public async Task Eine_kaputte_Datei_wird_ignoriert()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_pfad)!);
        await File.WriteAllTextAsync(_pfad, "{ kein json");
        var umgebung = new Wasserumgebung(speicherPfad: _pfad);

        var stand = await umgebung.Dienst.StandAsync();

        Assert.Equal(300, stand.Messstellen.Count);
        Assert.Equal(1, umgebung.Netz.Anzahl(Items));
    }
}
