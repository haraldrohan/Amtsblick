using System.Collections.Concurrent;
using System.Text.Json;
using Amtsblick.Core.Cache;
using Amtsblick.Core.Kontingent;
using Amtsblick.Core.Ort;
using Amtsblick.Modules.Wetter.GeoSphere;
using Microsoft.Extensions.Logging;

namespace Amtsblick.Modules.Wetter;

/// <summary>Ergebnis eines Abrufs samt Angabe, wie frisch es ist und woher es kommt.</summary>
/// <param name="Reihe">Null, wenn weder Upstream noch Cache etwas liefern.</param>
/// <param name="NurCache">Kontingent fast ausgeschöpft: es wurde bewusst nicht angefragt.</param>
/// <param name="Veraltet">Cache-Eintrag ist abgelaufen und konnte nicht erneuert werden.</param>
public sealed record Abruf(Zeitreihe? Reihe, DateTimeOffset? Abgerufen, bool NurCache, bool Veraltet, string? Fehler);

/// <summary>
/// Zeitreihen der GeoSphere mit Cache und Kontingentschutz. Ein Abruf je Ressource und Punkt
/// (auf 0,01° gerundet) bedient alle Nutzer.
/// </summary>
public sealed class WetterDienst(
    GeoSphereClient client, KontingentRegister kontingent, Anfragetakt takt, TimeProvider zeit, ILogger<WetterDienst>? log = null)
{
    /// <summary>Unter so vielen Restanfragen pro Stunde wird nur noch aus dem Cache geantwortet.</summary>
    public const int Reserve = 20;

    public const int LimitProStunde = 240;

    private static readonly TimeSpan MetadatenTtl = TimeSpan.FromMinutes(15);

    /// <summary>So lange wird nach einem gescheiterten Abruf derselbe Punkt nicht erneut angefragt.</summary>
    private static readonly TimeSpan Fehlerpause = TimeSpan.FromMinutes(1);

    private readonly TtlCache<(string Ressource, Koordinate Punkt), Zeitreihe> _reihen = new(zeit);
    private readonly TtlCache<string, Metadaten?> _metadaten = new(zeit);
    private readonly ConcurrentDictionary<(string Ressource, Koordinate Punkt), (DateTimeOffset Zeit, string Text)> _fehler = new();

    /// <summary>
    /// Wahr, wenn GeoSphere zuletzt weniger als <see cref="Reserve"/> Restanfragen gemeldet hat oder die
    /// eigene Zählung der letzten Stunde so nah am Limit liegt.
    /// </summary>
    public bool KontingentKnapp =>
        kontingent.IstKnapp(GeoSphereClient.Quelle, Reserve) || takt.AnfragenLetzteStunde >= LimitProStunde - Reserve;

    /// <summary>
    /// Metadaten der Ressource, höchstens alle 15 Minuten neu geholt. Null, wenn sie nicht zu bekommen
    /// sind; auch das wird 15 Minuten gemerkt, damit eine Störung kein Kontingent verbraucht.
    /// </summary>
    public async Task<Metadaten?> MetadatenAsync(Ressource ressource, CancellationToken ct = default)
    {
        if (KontingentKnapp)
        {
            return _metadaten.Lies(ressource.Id)?.Wert;
        }

        var eintrag = await _metadaten.HoleAsync(ressource.Id, async abbruch =>
        {
            try
            {
                return (await client.HoleMetadatenAsync(ressource, abbruch), MetadatenTtl);
            }
            catch (Exception fehler) when (IstAbruffehler(fehler))
            {
                log?.LogWarning("Metadaten von {Ressource} nicht abrufbar: {Fehler}", ressource.Id, fehler.Message);
                return (null, MetadatenTtl);
            }
        }, ct);
        return eintrag.Wert;
    }

    public async Task<Abruf> HoleAsync(Ressource ressource, Koordinate punkt, CancellationToken ct = default)
    {
        punkt = punkt.Gerundet(2);
        var schluessel = (ressource.Id, punkt);
        var eintrag = _reihen.Lies(schluessel);

        if (KontingentKnapp)
        {
            return eintrag is null
                ? new Abruf(null, null, NurCache: true, Veraltet: false, "Anfragekontingent fast ausgeschöpft und kein Stand im Cache.")
                : new Abruf(eintrag.Wert, eintrag.Abgerufen, NurCache: true, Veraltet: !_reihen.IstFrisch(eintrag), null);
        }

        // Ein neuer Lauf macht den Eintrag vor Ablauf der TTL ungültig.
        var metadaten = await MetadatenAsync(ressource, ct);
        if (eintrag is not null && _reihen.IstFrisch(eintrag)
            && (metadaten is null || eintrag.Wert.Referenzzeit >= metadaten.LetzterLauf))
        {
            return new Abruf(eintrag.Wert, eintrag.Abgerufen, NurCache: false, Veraltet: false, null);
        }

        // Kurz nach einem Fehlschlag nicht erneut anfragen: eine Störung soll kein Kontingent aufzehren.
        if (_fehler.TryGetValue(schluessel, out var letzter) && zeit.GetUtcNow() - letzter.Zeit < Fehlerpause)
        {
            return Gescheitert(eintrag, letzter.Text);
        }

        try
        {
            var neu = await _reihen.LadeAsync(
                schluessel,
                async abbruch => (await client.HoleZeitreiheAsync(ressource, punkt, abbruch), ressource.Ttl),
                ct);
            _fehler.TryRemove(schluessel, out _);
            return new Abruf(neu.Wert, neu.Abgerufen, NurCache: false, Veraltet: false, null);
        }
        catch (Exception fehler) when (IstAbruffehler(fehler))
        {
            log?.LogWarning("Abruf von {Ressource} für {Punkt} gescheitert: {Fehler}", ressource.Id, punkt, fehler.Message);
            _fehler[schluessel] = (zeit.GetUtcNow(), fehler.Message);
            return Gescheitert(eintrag, fehler.Message);
        }
    }

    private static Abruf Gescheitert(CacheEintrag<Zeitreihe>? eintrag, string fehler) =>
        eintrag is null
            ? new Abruf(null, null, NurCache: false, Veraltet: false, $"GeoSphere nicht erreichbar ({fehler}).")
            : new Abruf(eintrag.Wert, eintrag.Abgerufen, NurCache: false, Veraltet: true, fehler);

    private static bool IstAbruffehler(Exception fehler) =>
        fehler is HttpRequestException or TaskCanceledException or JsonException
            or KeyNotFoundException or FormatException or InvalidOperationException or IndexOutOfRangeException;
}
