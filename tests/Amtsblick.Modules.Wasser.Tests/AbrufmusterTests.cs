using System.Net;
using Amtsblick.Core;
using Amtsblick.Core.Http;
using Amtsblick.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using static Amtsblick.Modules.Wasser.Tests.Wasserumgebung;

namespace Amtsblick.Modules.Wasser.Tests;

public class AbrufmusterTests
{
    [Fact]
    public async Task Beliebig_viele_auch_gleichzeitige_Tool_Aufrufe_in_einer_Stunde_erzeugen_einen_Upstream_Abruf()
    {
        var umgebung = new Wasserumgebung { Sperre = new TaskCompletionSource() };

        // 60 gleichzeitige Aufrufe aller drei Tools, während der erste Abruf noch läuft.
        var gleichzeitig = Enumerable.Range(0, 20)
            .SelectMany(_ => new[]
            {
                umgebung.Auskunft.InDerNaeheAsync("Steyr"),
                umgebung.Auskunft.AnGewaesserAsync("Enns"),
                umgebung.Auskunft.HochwasserlageAsync(),
            })
            .ToList();
        Assert.All(gleichzeitig, aufruf => Assert.False(aufruf.IsCompleted));
        umgebung.Sperre.SetResult();
        var antworten = await Task.WhenAll(gleichzeitig);
        Assert.All(antworten, a => Assert.Contains("Stand des Abrufs: 02.10. 21:07", a.Zusammenfassung));

        // Weitere 118 Aufrufe über 59 Minuten verteilt.
        for (var minute = 0; minute < 59; minute++)
        {
            umgebung.Zeit.Advance(TimeSpan.FromMinutes(1));
            await umgebung.Auskunft.InDerNaeheAsync("Garsten", 30);
            await umgebung.Auskunft.AnGewaesserAsync("Donau");
        }

        Assert.Equal(1, umgebung.Netz.Anzahl(Items));
        Assert.Equal(1, umgebung.Netz.Anzahl(Collections));
        Assert.Equal(0, umgebung.Netz.Anzahl(Wfs));
        Assert.Equal(2, umgebung.Netz.Anfragen.Count);
    }

    [Fact]
    public async Task Nach_60_Minuten_loest_der_naechste_Aufruf_genau_einen_weiteren_Abruf_aus()
    {
        var umgebung = new Wasserumgebung();
        await umgebung.Auskunft.HochwasserlageAsync();

        umgebung.Zeit.Advance(TimeSpan.FromMinutes(60));
        var antworten = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => umgebung.Auskunft.HochwasserlageAsync()));

        Assert.Equal(2, umgebung.Netz.Anzahl(Items));
        Assert.Equal(1, umgebung.Netz.Anzahl(Collections));
        Assert.All(antworten, a => Assert.Contains("Stand des Abrufs: 02.10. 22:07", a.Zusammenfassung));
    }

    [Fact]
    public async Task Ohne_Tool_Aufruf_ruft_eine_laufende_Instanz_nichts_ab()
    {
        var (dienste, netz, zeit) = Instanz(vorladen: null);
        var hintergrund = dienste.GetServices<IHostedService>().ToList();
        foreach (var dienst in hintergrund)
        {
            await dienst.StartAsync(default);
        }

        // Dienste sind aufgebaut und bereit, die Uhr läuft einen Tag weiter.
        _ = dienste.GetRequiredService<PegelAuskunft>();
        for (var stunde = 0; stunde < 24; stunde++)
        {
            zeit.Advance(TimeSpan.FromHours(1));
            await Task.Yield();
        }

        Assert.DoesNotContain(hintergrund, d => d is PegelVorlader);
        Assert.Empty(netz.Anfragen);

        // Erst der Tool-Aufruf löst den Abruf aus, mit Kontaktadresse im User-Agent.
        await dienste.GetRequiredService<PegelAuskunft>().HochwasserlageAsync();
        Assert.Equal(1, netz.Anzahl(Items));
        Assert.All(netz.UserAgents, ua => Assert.Equal(
            $"Amtsblick/{UserAgent.Version} (+https://github.com/haraldrohan/Amtsblick; betrieb@example.org)", ua));
    }

    [Fact]
    public async Task Der_Vorlader_ist_nur_mit_Schalter_aktiv_und_bleibt_bei_einem_Abruf_pro_Stunde()
    {
        var (dienste, netz, zeit) = Instanz(vorladen: true);
        var vorlader = Assert.Single(dienste.GetServices<IHostedService>().OfType<PegelVorlader>());

        await vorlader.StartAsync(default);
        await Bis(() => netz.Anzahl(Items) == 1);

        // Tool-Aufrufe in derselben Stunde lesen den vorgeladenen Stand.
        zeit.Advance(TimeSpan.FromMinutes(30));
        await dienste.GetRequiredService<PegelAuskunft>().HochwasserlageAsync();
        Assert.Equal(1, netz.Anzahl(Items));

        zeit.Advance(TimeSpan.FromMinutes(31));
        await Bis(() => netz.Anzahl(Items) == 2);
        await vorlader.StopAsync(default);
        Assert.Equal(2, netz.Anzahl(Items));
    }

    [Fact]
    public async Task Ohne_Kontaktadresse_wird_nicht_abgerufen()
    {
        var umgebung = new Wasserumgebung(kontakt: " ");

        var teil = await umgebung.Auskunft.InDerNaeheAsync("Steyr");

        Assert.Empty(umgebung.Netz.Anfragen);
        Assert.Contains("Kontaktadresse", teil.Zusammenfassung);
        Assert.Contains(PegelAuskunft.Hinweis, teil.Hinweise);
        Assert.Equal("Datenquelle: ehyd.gv.at", teil.Quellen[0].Vermerk);
    }

    [Fact]
    public async Task Abschalten_des_Moduls_wirkt_sofort()
    {
        var aktiv = true;
        var umgebung = new Wasserumgebung(istAktiv: () => aktiv);
        await umgebung.Auskunft.HochwasserlageAsync();

        aktiv = false;
        umgebung.Zeit.Advance(TimeSpan.FromHours(2));
        var teil = await umgebung.Auskunft.HochwasserlageAsync();

        Assert.Equal(1, umgebung.Netz.Anzahl(Items));
        Assert.Contains("abgeschaltet", teil.Zusammenfassung);
    }

    [Fact]
    public async Task Bei_Fehler_wird_der_letzte_Stand_als_veraltet_weitergeliefert_und_erst_nach_einer_Stunde_neu_versucht()
    {
        var umgebung = new Wasserumgebung();
        await umgebung.Auskunft.AnGewaesserAsync("Enns");

        umgebung.OgcStatus = HttpStatusCode.ServiceUnavailable;
        umgebung.WfsStatus = HttpStatusCode.ServiceUnavailable;
        umgebung.Zeit.Advance(TimeSpan.FromMinutes(61));
        var teil = await umgebung.Auskunft.AnGewaesserAsync("Enns");
        var antwort = Json(teil);

        Assert.True(antwort.GetProperty("veraltet").GetBoolean());
        Assert.Equal("2026-10-02T21:07+02:00", antwort.GetProperty("stand_abruf").GetString());
        Assert.Equal(7, antwort.GetProperty("messstellen").GetArrayLength());
        Assert.Contains("(veraltet)", teil.Zusammenfassung);
        Assert.Contains(teil.Hinweise, h => h.StartsWith("Veraltet:"));
        Assert.Equal(2, umgebung.Netz.Anzahl(Items));
        Assert.Equal(1, umgebung.Netz.Anzahl(Wfs));

        // Der gescheiterte Versuch zählt als der eine Abruf dieser Stunde.
        umgebung.OgcStatus = HttpStatusCode.OK;
        umgebung.Zeit.Advance(TimeSpan.FromMinutes(59));
        Assert.True(Json(await umgebung.Auskunft.AnGewaesserAsync("Enns")).GetProperty("veraltet").GetBoolean());
        Assert.Equal(2, umgebung.Netz.Anzahl(Items));

        umgebung.Zeit.Advance(TimeSpan.FromMinutes(1));
        var erholt = Json(await umgebung.Auskunft.AnGewaesserAsync("Enns"));
        Assert.False(erholt.GetProperty("veraltet").GetBoolean());
        Assert.Equal("2026-10-02T23:08+02:00", erholt.GetProperty("stand_abruf").GetString());
        Assert.Equal(3, umgebung.Netz.Anzahl(Items));
    }

    [Fact]
    public async Task Fehler_beim_ersten_Abruf_ergibt_eine_Antwort_ohne_Daten_und_keinen_zweiten_Versuch()
    {
        var umgebung = new Wasserumgebung
        {
            OgcStatus = HttpStatusCode.InternalServerError,
            WfsStatus = HttpStatusCode.InternalServerError,
        };

        var teil = await umgebung.Auskunft.InDerNaeheAsync("Steyr");
        await umgebung.Auskunft.InDerNaeheAsync("Steyr");

        Assert.Contains("nicht verfügbar", teil.Zusammenfassung);
        Assert.Contains(PegelAuskunft.Hinweis, teil.Hinweise);
        Assert.Equal(2, umgebung.Netz.Anfragen.Count);
    }

    [Fact]
    public async Task Faellt_die_OGC_API_aus_kommt_der_Bestand_ueber_WFS()
    {
        var umgebung = new Wasserumgebung { OgcStatus = HttpStatusCode.NotFound };

        var stand = await umgebung.Dienst.StandAsync();

        Assert.Equal("WFS", stand.Weg);
        Assert.Equal(300, stand.Messstellen.Count);
        Assert.False(stand.Veraltet);
        Assert.Equal(1, umgebung.Netz.Anzahl(Wfs));
        Assert.Contains("typeName=i000501:pegel_aktuell", umgebung.Netz.Anfragen.Last());
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Weist_der_Host_ab_wird_nicht_ueber_WFS_nachgefasst(HttpStatusCode status)
    {
        var umgebung = new Wasserumgebung { OgcStatus = status };

        var stand = await umgebung.Dienst.StandAsync();

        Assert.Null(stand.Abgerufen);
        Assert.Equal(0, umgebung.Netz.Anzahl(Wfs));
        Assert.Single(umgebung.Netz.Anfragen);
    }

    [Fact]
    public async Task Es_werden_nur_die_festgelegten_Adressen_angesprochen()
    {
        var umgebung = new Wasserumgebung();

        await umgebung.Auskunft.HochwasserlageAsync();

        Assert.Equal(
            new[]
            {
                "https://gis.lfrz.gv.at/api/geodata/i000501/ogc/features/v1/collections?f=json",
                "https://gis.lfrz.gv.at/api/geodata/i000501/ogc/features/v1/collections/i000501:pegel_aktuell/items?f=json&limit=1000&startIndex=0",
            },
            umgebung.Netz.Anfragen);
    }

    // Vollständige Instanz über die Registrierung, wie der Server sie aufbaut.
    private static (ServiceProvider Dienste, AufzeichnenderHandler Netz, FakeTimeProvider Zeit) Instanz(bool? vorladen)
    {
        var zeit = new FakeTimeProvider(new DateTimeOffset(2026, 10, 2, 19, 7, 0, TimeSpan.Zero));
        var netz = new AufzeichnenderHandler(anfrage => Fixture.Antwort(Fixture.Text(
            anfrage.RequestUri!.ToString().EndsWith(Collections, StringComparison.Ordinal) ? "collections.json" : "pegel_aktuell.items.json")));
        var einstellungen = new Dictionary<string, string?>
        {
            ["Amtsblick:Kontakt"] = "betrieb@example.org",
            ["Amtsblick:DatenVerzeichnis"] = Path.Combine(Path.GetTempPath(), $"amtsblick-test-{Guid.NewGuid():N}"),
        };
        if (vorladen is { } schalter)
        {
            einstellungen["Amtsblick:Module:Wasser:Vorladen"] = schalter.ToString();
        }

        var konfiguration = new ConfigurationBuilder().AddInMemoryCollection(einstellungen).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(zeit);
        services.AddAmtsblickKern(konfiguration);
        services.AddWasserModul(konfiguration);
        services.AddHttpClient(Ehyd.EhydClient.Quelle).ConfigurePrimaryHttpMessageHandler(() => netz);
        return (services.BuildServiceProvider(), netz, zeit);
    }

    private static async Task Bis(Func<bool> bedingung)
    {
        for (var i = 0; i < 500 && !bedingung(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(bedingung(), "Bedingung nicht innerhalb von 5 Sekunden erfüllt.");
    }
}
