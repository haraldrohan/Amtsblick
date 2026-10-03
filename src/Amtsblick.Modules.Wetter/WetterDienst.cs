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
/// <param name="WirdGeladen">GeoSphere antwortet langsam: der Abruf läuft im Hintergrund weiter und füllt den Cache.</param>
public sealed record Abruf(
    Zeitreihe? Reihe, DateTimeOffset? Abgerufen, bool NurCache, bool Veraltet, string? Fehler, bool WirdGeladen = false);

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

    /// <summary>
    /// So lange wartet ein Aufrufer auf Daten. Dauert der Abruf länger, bekommt er eine Antwort ohne
    /// diese Daten; der Abruf läuft weiter und steht der nächsten Anfrage aus dem Cache zur Verfügung.
    /// </summary>
    public static readonly TimeSpan Geduld = TimeSpan.FromSeconds(8);

    /// <summary>Wie <see cref="Geduld"/>, für die Metadaten; ohne sie wird mit dem letzten bekannten Lauf gearbeitet.</summary>
    public static readonly TimeSpan MetadatenGeduld = TimeSpan.FromSeconds(3);

    private const string LangsamText =
        "GeoSphere antwortet derzeit langsam; die Daten werden im Hintergrund geladen und stehen in Kürze bereit.";

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
    /// Metadaten der Ressource, höchstens alle 15 Minuten neu geholt. Null, wenn sie nicht oder nicht
    /// rechtzeitig zu bekommen sind; ein Fehlschlag wird 15 Minuten gemerkt, damit eine Störung kein
    /// Kontingent verbraucht.
    /// </summary>
    public async Task<Metadaten?> MetadatenAsync(Ressource ressource, CancellationToken ct = default)
    {
        if (KontingentKnapp)
        {
            return _metadaten.Lies(ressource.Id)?.Wert;
        }

        var laden = _metadaten.HoleAsync(ressource.Id, async abbruch =>
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
        });

        return await InnerhalbAsync(laden, MetadatenGeduld, ct)
            ? (await laden).Wert
            : _metadaten.Lies(ressource.Id)?.Wert;
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

        var laden = LadeUndMerkeAsync(schluessel, ressource, punkt);
        if (!await InnerhalbAsync(laden, Geduld, ct))
        {
            return new Abruf(eintrag?.Wert, eintrag?.Abgerufen, NurCache: false, Veraltet: eintrag is not null, LangsamText, WirdGeladen: true);
        }

        var (neu, fehler) = await laden;
        return neu is not null
            ? new Abruf(neu.Wert, neu.Abgerufen, NurCache: false, Veraltet: false, null)
            : Gescheitert(eintrag, fehler!);
    }

    // Läuft unabhängig vom Aufrufer zu Ende, damit auch ein langsamer Abruf den Cache füllt.
    private async Task<(CacheEintrag<Zeitreihe>? Eintrag, string? Fehler)> LadeUndMerkeAsync(
        (string Ressource, Koordinate Punkt) schluessel, Ressource ressource, Koordinate punkt)
    {
        try
        {
            var neu = await _reihen.LadeAsync(
                schluessel,
                async abbruch => (await client.HoleZeitreiheAsync(ressource, punkt, abbruch), ressource.Ttl));
            _fehler.TryRemove(schluessel, out _);
            return (neu, null);
        }
        catch (Exception fehler) when (IstAbruffehler(fehler))
        {
            log?.LogWarning("Abruf von {Ressource} für {Punkt} gescheitert: {Fehler}", ressource.Id, punkt, fehler.Message);
            _fehler[schluessel] = (zeit.GetUtcNow(), fehler.Message);
            return (null, fehler.Message);
        }
    }

    // Wahr, wenn die Aufgabe innerhalb der Frist fertig wird. Die Aufgabe selbst läuft in jedem Fall weiter.
    private async Task<bool> InnerhalbAsync(Task aufgabe, TimeSpan frist, CancellationToken ct)
    {
        if (aufgabe.IsCompleted)
        {
            return true;
        }

        using var abbruch = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ablauf = Task.Delay(frist, zeit, abbruch.Token);
        var erste = await Task.WhenAny(aufgabe, ablauf);
        await abbruch.CancelAsync();
        ct.ThrowIfCancellationRequested();
        return erste == aufgabe;
    }

    private static Abruf Gescheitert(CacheEintrag<Zeitreihe>? eintrag, string fehler) =>
        eintrag is null
            ? new Abruf(null, null, NurCache: false, Veraltet: false, $"GeoSphere nicht erreichbar ({fehler}).")
            : new Abruf(eintrag.Wert, eintrag.Abgerufen, NurCache: false, Veraltet: true, fehler);

    private static bool IstAbruffehler(Exception fehler) =>
        fehler is HttpRequestException or TaskCanceledException or JsonException
            or KeyNotFoundException or FormatException or InvalidOperationException or IndexOutOfRangeException;
}
