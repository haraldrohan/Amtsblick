using System.Collections.Concurrent;

namespace Amtsblick.Core.Kontingent;

/// <summary>Zuletzt gemeldete Restanfragen einer Quelle laut ihren Antwort-Headern.</summary>
public sealed record KontingentStand(
    string Quelle,
    int? RestStunde,
    int? LimitStunde,
    int? RestSekunde,
    int? LimitSekunde,
    DateTimeOffset Stand);

/// <summary>Führt je Quelle die Restanfragen aus den Headern <c>x-ratelimit-*</c> mit.</summary>
public sealed class KontingentRegister(TimeProvider zeit)
{
    private readonly ConcurrentDictionary<string, KontingentStand> _staende = new();

    public IReadOnlyList<KontingentStand> Alle => _staende.Values.OrderBy(s => s.Quelle).ToList();

    public KontingentStand? Fuer(string quelle) => _staende.GetValueOrDefault(quelle);

    public void Melde(string quelle, HttpResponseMessage antwort)
    {
        var restStunde = Lies(antwort, "x-ratelimit-remaining-hour");
        var restSekunde = Lies(antwort, "x-ratelimit-remaining-second");
        if (restStunde is null && restSekunde is null)
        {
            return;
        }

        _staende[quelle] = new KontingentStand(
            quelle,
            restStunde,
            Lies(antwort, "x-ratelimit-limit-hour"),
            restSekunde,
            Lies(antwort, "x-ratelimit-limit-second"),
            zeit.GetUtcNow());
    }

    /// <summary>
    /// Wahr, wenn die Quelle zuletzt weniger als <paramref name="reserve"/> Restanfragen für die Stunde
    /// gemeldet hat. Eine Meldung, die älter als eine Stunde ist, gilt nicht mehr: das Fenster ist dann
    /// abgelaufen und nur eine neue Anfrage kann den Stand auffrischen.
    /// </summary>
    public bool IstKnapp(string quelle, int reserve) =>
        Fuer(quelle) is { RestStunde: { } rest } stand
        && rest < reserve
        && zeit.GetUtcNow() - stand.Stand < TimeSpan.FromHours(1);

    private static int? Lies(HttpResponseMessage antwort, string name) =>
        antwort.Headers.TryGetValues(name, out var werte) && int.TryParse(werte.FirstOrDefault(), out var wert)
            ? wert
            : null;
}
