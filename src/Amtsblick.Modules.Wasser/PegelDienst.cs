using System.Text.Json;
using Amtsblick.Core;
using Amtsblick.Core.Ort;
using Amtsblick.Modules.Wasser.Ehyd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amtsblick.Modules.Wasser;

/// <summary>Zuletzt geholter Pegelbestand.</summary>
/// <param name="Abgerufen">Zeitpunkt des letzten erfolgreichen Abrufs; null, wenn es noch keinen gab.</param>
/// <param name="Veraltet">Der letzte Abrufversuch ist gescheitert; die Messstellen sind der Stand davor.</param>
public sealed record Pegelstand(
    IReadOnlyList<Messstelle> Messstellen,
    DateTimeOffset? Abgerufen,
    bool Veraltet,
    string? Fehler,
    string? Weg);

/// <summary>
/// Hält den Pegelbestand und setzt das Abrufmuster für eHYD durch: höchstens ein Abrufversuch des
/// Gesamtbestands pro 60 Minuten. Der erste Aufruf von <see cref="StandAsync"/> löst ihn aus,
/// gleichzeitige Aufrufe warten auf denselben Abruf, danach wird eine Stunde lang nur der Speicher
/// gelesen. Ohne Aufruf findet kein Abruf statt. Auch ein gescheiterter Versuch zählt.
/// Mit <paramref name="speicherPfad"/> überdauern Bestand und Zeitpunkt des letzten Versuchs einen
/// Neustart, sodass die Grenze auch für häufig neu gestartete stdio-Instanzen gilt.
/// </summary>
/// <param name="istAktiv">
/// Wird vor jedem möglichen Abruf gefragt, damit das Abschalten des Moduls in der Konfiguration
/// ohne Neustart wirkt. Ohne Angabe ist das Modul aktiv.
/// </param>
/// <param name="speicherPfad">JSON-Datei für den letzten Stand; null speichert nichts.</param>
public sealed class PegelDienst(
    EhydClient client,
    GemeindeVerzeichnis gemeinden,
    IOptions<AmtsblickOptionen> optionen,
    TimeProvider zeit,
    Func<bool>? istAktiv = null,
    ILogger<PegelDienst>? log = null,
    string? speicherPfad = null)
{
    public static readonly TimeSpan Abstand = TimeSpan.FromMinutes(60);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Lock _sperre = new();
    private Task? _laufend;
    private bool _geladen;
    private DateTimeOffset? _letzterVersuch;
    private Pegelstand _stand = new([], null, false, null, null);

    public async Task<Pegelstand> StandAsync(CancellationToken ct = default)
    {
        if (istAktiv is not null && !istAktiv())
        {
            return new Pegelstand([], null, false, "Das Modul Wasser ist per Konfiguration abgeschaltet.", null);
        }

        Task? laufend;
        lock (_sperre)
        {
            if (!_geladen)
            {
                _geladen = true;
                LadeGespeichertenStand();
            }

            if (string.IsNullOrWhiteSpace(optionen.Value.Kontakt))
            {
                return _stand with
                {
                    Fehler = "Keine Kontaktadresse konfiguriert (Amtsblick:Kontakt). Ohne sie ruft das Modul Wasser eHYD nicht ab.",
                };
            }

            var jetzt = zeit.GetUtcNow();
            if (_laufend is null or { IsCompleted: true } && (_letzterVersuch is null || jetzt - _letzterVersuch >= Abstand))
            {
                _letzterVersuch = jetzt;
                _laufend = AktualisiereAsync();
            }

            laufend = _laufend;
        }

        if (laufend is { IsCompleted: false })
        {
            await laufend.WaitAsync(ct);
        }

        return _stand;
    }

    // Nicht an das Abbruchsignal eines Aufrufers gebunden: andere warten auf dasselbe Ergebnis.
    private async Task AktualisiereAsync()
    {
        try
        {
            var (messstellen, weg) = await client.HoleGesamtbestandAsync(CancellationToken.None);
            var zugeordnet = messstellen.Select(MitGemeinde).ToList();
            _stand = new Pegelstand(zugeordnet, zeit.GetUtcNow(), Veraltet: false, Fehler: null, weg);
        }
        catch (Exception fehler) when (EhydClient.IstAbruffehler(fehler))
        {
            log?.LogWarning("Abruf von eHYD gescheitert, nächster Versuch frühestens in 60 Minuten: {Fehler}", fehler.Message);
            _stand = _stand with { Veraltet = _stand.Abgerufen is not null, Fehler = $"Abruf von eHYD fehlgeschlagen ({fehler.Message})." };
        }

        SpeichereStand();
    }

    // Punkt-in-Polygon: jede Messstelle bekommt die GKZ der Gemeinde, in der sie liegt.
    private Messstelle MitGemeinde(Messstelle messstelle) =>
        messstelle.Ort is { } ort && gemeinden.Finde(ort) is { } gemeinde
            ? messstelle with { Gkz = gemeinde.Gkz, Gemeinde = gemeinde.Name, Bundesland = gemeinde.Bundesland }
            : messstelle;

    private void LadeGespeichertenStand()
    {
        if (speicherPfad is null || !File.Exists(speicherPfad))
        {
            return;
        }

        try
        {
            var gespeichert = JsonSerializer.Deserialize<GespeicherterStand>(File.ReadAllText(speicherPfad), Json);
            if (gespeichert is null)
            {
                return;
            }

            _letzterVersuch = gespeichert.LetzterVersuch;
            client.CollectionId ??= gespeichert.CollectionId;

            // Gemeinden neu zuordnen, falls sie seither neu importiert wurden.
            _stand = new Pegelstand(
                gespeichert.Messstellen.Select(m => MitGemeinde(m with { Gkz = null, Gemeinde = null, Bundesland = null })).ToList(),
                gespeichert.Abgerufen,
                Veraltet: gespeichert.Fehler is not null && gespeichert.Abgerufen is not null,
                gespeichert.Fehler,
                gespeichert.Weg);
        }
        catch (Exception fehler) when (fehler is IOException or JsonException or UnauthorizedAccessException)
        {
            log?.LogWarning("Gespeicherter Pegelstand nicht lesbar, wird ignoriert: {Fehler}", fehler.Message);
        }
    }

    // Erst in eine Hilfsdatei schreiben und dann umbenennen, damit nie eine halbe Datei liegen bleibt.
    private void SpeichereStand()
    {
        if (speicherPfad is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(speicherPfad))!);
            var stand = new GespeicherterStand(
                _letzterVersuch, _stand.Abgerufen, _stand.Fehler, _stand.Weg, client.CollectionId, _stand.Messstellen);
            var hilfsdatei = speicherPfad + ".neu";
            File.WriteAllText(hilfsdatei, JsonSerializer.Serialize(stand, Json));
            File.Move(hilfsdatei, speicherPfad, overwrite: true);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            log?.LogWarning("Pegelstand konnte nicht gespeichert werden: {Fehler}", fehler.Message);
        }
    }

    private sealed record GespeicherterStand(
        DateTimeOffset? LetzterVersuch,
        DateTimeOffset? Abgerufen,
        string? Fehler,
        string? Weg,
        string? CollectionId,
        IReadOnlyList<Messstelle> Messstellen);
}
