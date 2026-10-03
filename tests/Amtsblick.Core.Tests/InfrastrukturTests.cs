using System.Net;
using System.Text.Json;
using Amtsblick.Core.Cache;
using Amtsblick.Core.Http;
using Amtsblick.Core.Kontingent;
using Amtsblick.Core.Quellen;
using Amtsblick.Tests;
using Microsoft.Extensions.Time.Testing;

namespace Amtsblick.Core.Tests;

public class CacheTests
{
    [Fact]
    public async Task Gleichzeitige_Anfragen_teilen_sich_einen_Ladevorgang()
    {
        var cache = new TtlCache<string, int>(new FakeTimeProvider());
        var freigabe = new TaskCompletionSource();
        var geladen = 0;

        async Task<(int, TimeSpan)> Laden(CancellationToken ct)
        {
            Interlocked.Increment(ref geladen);
            await freigabe.Task;
            return (42, TimeSpan.FromMinutes(15));
        }

        var aufrufe = Enumerable.Range(0, 20).Select(_ => cache.HoleAsync("k", Laden)).ToList();
        freigabe.SetResult();
        var ergebnisse = await Task.WhenAll(aufrufe);

        Assert.Equal(1, geladen);
        Assert.All(ergebnisse, e => Assert.Equal(42, e.Wert));
    }

    [Fact]
    public async Task Nach_Ablauf_der_TTL_wird_neu_geladen_und_der_alte_Stand_bleibt_bis_dahin_lesbar()
    {
        var zeit = new FakeTimeProvider();
        var cache = new TtlCache<string, int>(zeit);
        var geladen = 0;
        Task<(int, TimeSpan)> Laden(CancellationToken ct) => Task.FromResult((++geladen, TimeSpan.FromMinutes(15)));

        await cache.HoleAsync("k", Laden);
        zeit.Advance(TimeSpan.FromMinutes(14));
        Assert.Equal(1, (await cache.HoleAsync("k", Laden)).Wert);

        zeit.Advance(TimeSpan.FromMinutes(2));
        var abgelaufen = cache.Lies("k")!;
        Assert.Equal(1, abgelaufen.Wert);
        Assert.False(cache.IstFrisch(abgelaufen));
        Assert.Equal(2, (await cache.HoleAsync("k", Laden)).Wert);
    }

    [Fact]
    public async Task Fehler_beim_Laden_wird_nicht_gespeichert()
    {
        var cache = new TtlCache<string, int>(new FakeTimeProvider());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => cache.HoleAsync("k", _ => throw new HttpRequestException("kaputt")));

        Assert.Null(cache.Lies("k"));
        Assert.Equal(7, (await cache.HoleAsync("k", _ => Task.FromResult((7, TimeSpan.FromMinutes(1))))).Wert);
    }
}

public class KontingentTests
{
    private static HttpResponseMessage MitHeadern(int stunde, int sekunde)
    {
        var antwort = new HttpResponseMessage(HttpStatusCode.OK);
        antwort.Headers.Add("X-RateLimit-Remaining-Hour", stunde.ToString());
        antwort.Headers.Add("X-RateLimit-Limit-Hour", "240");
        antwort.Headers.Add("X-RateLimit-Remaining-Second", sekunde.ToString());
        antwort.Headers.Add("X-RateLimit-Limit-Second", "5");
        return antwort;
    }

    [Fact]
    public void Restanfragen_werden_je_Quelle_aus_den_Headern_gelesen()
    {
        var register = new KontingentRegister(new FakeTimeProvider());

        register.Melde("geosphere", MitHeadern(238, 4));
        register.Melde("ehyd", new HttpResponseMessage(HttpStatusCode.OK));

        var stand = Assert.Single(register.Alle);
        Assert.Equal("geosphere", stand.Quelle);
        Assert.Equal(238, stand.RestStunde);
        Assert.Equal(240, stand.LimitStunde);
        Assert.Equal(4, stand.RestSekunde);
        Assert.Equal(5, stand.LimitSekunde);
        Assert.Null(register.Fuer("ehyd"));
    }

    [Fact]
    public void Knapp_gilt_unter_der_Reserve_und_nur_innerhalb_des_Stundenfensters()
    {
        var zeit = new FakeTimeProvider();
        var register = new KontingentRegister(zeit);

        register.Melde("geosphere", MitHeadern(20, 4));
        Assert.False(register.IstKnapp("geosphere", 20));

        register.Melde("geosphere", MitHeadern(19, 4));
        Assert.True(register.IstKnapp("geosphere", 20));

        zeit.Advance(TimeSpan.FromMinutes(61));
        Assert.False(register.IstKnapp("geosphere", 20));
    }

    [Fact]
    public async Task Anfragetakt_zaehlt_die_Anfragen_der_letzten_Stunde()
    {
        var zeit = new FakeTimeProvider();
        var takt = new Anfragetakt(zeit, proSekunde: 4);

        await takt.WarteAsync(default);
        zeit.Advance(TimeSpan.FromMinutes(30));
        await takt.WarteAsync(default);
        Assert.Equal(2, takt.AnfragenLetzteStunde);

        zeit.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal(1, takt.AnfragenLetzteStunde);
    }

    [Fact]
    public async Task Anfragetakt_bremst_die_fuenfte_Anfrage_derselben_Sekunde()
    {
        var zeit = new FakeTimeProvider();
        var takt = new Anfragetakt(zeit, proSekunde: 4);
        for (var i = 0; i < 4; i++)
        {
            await takt.WarteAsync(default);
        }

        var fuenfte = takt.WarteAsync(default);
        Assert.False(fuenfte.IsCompleted);

        zeit.Advance(TimeSpan.FromSeconds(1));
        await fuenfte.WaitAsync(TimeSpan.FromSeconds(5));
    }
}

public class HttpTests
{
    private static async Task<(HttpResponseMessage Antwort, int Aufrufe)> Sende(int wiederholungen, params HttpStatusCode[] folge)
    {
        var aufrufe = 0;
        var netz = new AufzeichnenderHandler(_ => new HttpResponseMessage(folge[Math.Min(aufrufe++, folge.Length - 1)]));
        using var http = new HttpClient(
            new RetryHandler(wiederholungen, TimeSpan.FromMilliseconds(1), TimeProvider.System) { InnerHandler = netz });
        return (await http.GetAsync("https://example.invalid/"), aufrufe);
    }

    [Fact]
    public async Task Retry_bei_5xx_bis_zum_Erfolg()
    {
        var (antwort, aufrufe) = await Sende(2, HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadGateway, HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal(3, aufrufe);
    }

    [Fact]
    public async Task Retry_bei_429()
    {
        var (antwort, aufrufe) = await Sende(2, HttpStatusCode.TooManyRequests, HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal(2, aufrufe);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Kein_Retry_bei_anderen_Fehlern(HttpStatusCode status)
    {
        var (antwort, aufrufe) = await Sende(2, status);

        Assert.Equal(status, antwort.StatusCode);
        Assert.Equal(1, aufrufe);
    }

    [Fact]
    public async Task Retry_endet_nach_der_erlaubten_Zahl_von_Wiederholungen()
    {
        var (antwort, aufrufe) = await Sende(2, HttpStatusCode.InternalServerError);

        Assert.Equal(HttpStatusCode.InternalServerError, antwort.StatusCode);
        Assert.Equal(3, aufrufe);
    }

    [Fact]
    public void UserAgent_nennt_Version_Repo_und_Kontakt()
    {
        var mit = UserAgent.Erzeuge(new AmtsblickOptionen { RepoUrl = "https://example.org/repo", Kontakt = "betrieb@example.org" });
        var ohne = UserAgent.Erzeuge(new AmtsblickOptionen { RepoUrl = "https://example.org/repo" });

        Assert.Equal($"Amtsblick/{UserAgent.Version} (+https://example.org/repo; betrieb@example.org)", mit);
        Assert.Equal($"Amtsblick/{UserAgent.Version} (+https://example.org/repo)", ohne);
        Assert.Matches(@"^\d+\.\d+\.\d+$", UserAgent.Version);
    }
}

public class ToolAntwortTests
{
    [Fact]
    public void Antwort_beginnt_mit_der_Zusammenfassung_und_endet_mit_Quellen_und_Hinweisen()
    {
        var quelle = new Quellenvermerk("Quelle X", "CC BY 4.0", "Datenquelle: X", "https://example.org", "ds-1", "2026-10-02");

        var json = ToolAntwort.Erzeuge("Kurz gesagt: ärgerlich.", new { TempMax = 21.5, Leer = (string?)null }, [quelle], ["Hinweis A", null, ""]);

        Assert.DoesNotContain('\n', json);
        Assert.Contains("ärgerlich", json);
        using var dokument = JsonDocument.Parse(json);
        var namen = dokument.RootElement.EnumerateObject().Select(e => e.Name).ToList();
        Assert.Equal(new[] { "zusammenfassung", "temp_max", "quellen", "hinweise" }, namen);
        var gelesen = dokument.RootElement.GetProperty("quellen")[0];
        Assert.Equal("Datenquelle: X", gelesen.GetProperty("vermerk").GetString());
        Assert.Equal("CC BY 4.0", gelesen.GetProperty("lizenz").GetString());
        Assert.Equal("https://example.org", gelesen.GetProperty("link").GetString());
        Assert.Equal("2026-10-02", gelesen.GetProperty("stand").GetString());
        Assert.Equal(1, dokument.RootElement.GetProperty("hinweise").GetArrayLength());
    }
}
