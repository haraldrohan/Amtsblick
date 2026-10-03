using Amtsblick.Core.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amtsblick.Core.Ort;

/// <summary>
/// Lädt die Gemeinden beim ersten Start selbst, wenn sie fehlen, damit ein frisch installierter Server
/// ohne Handgriff nutzbar wird. Der Import läuft im Hintergrund; bis er fertig ist, erklären die Tools
/// über <see cref="GemeindeVerzeichnis.Ladehinweis"/>, warum noch kein Ort gefunden wird.
/// </summary>
public sealed class GemeindeBereitsteller(
    GemeindeVerzeichnis verzeichnis,
    IHttpClientFactory fabrik,
    IOptions<AmtsblickOptionen> optionen,
    ILogger<GemeindeBereitsteller> log) : BackgroundService
{
    /// <summary>Name des HttpClients für den Import.</summary>
    public const string Quelle = "statistik";

    protected override async Task ExecuteAsync(CancellationToken stoppToken)
    {
        if (!verzeichnis.IstLeer || !optionen.Value.AutoImport)
        {
            return;
        }

        var pfad = Path.Combine(optionen.Value.ErmittleDatenVerzeichnis(), GemeindeDatenbank.Dateiname);
        verzeichnis.Ladehinweis =
            "Die Gemeindedaten werden gerade zum ersten Mal von der Statistik Austria geladen. "
            + "Das dauert ein bis zwei Minuten; bitte die Frage danach noch einmal stellen.";
        try
        {
            log.LogInformation("Gemeindedaten fehlen, Import nach {Pfad} beginnt", pfad);
            using var http = fabrik.CreateClient(Quelle);
            await new GemeindeImport(http).ImportiereAsync(pfad, meldung => log.LogInformation("{Meldung}", meldung), stoppToken);
            verzeichnis.Ersetze(GemeindeDatenbank.Lade(pfad));
            log.LogInformation("{Anzahl} Gemeinden geladen", verzeichnis.Alle.Count);
        }
        catch (OperationCanceledException) when (stoppToken.IsCancellationRequested)
        {
            // Server wird beendet; der nächste Start versucht es erneut.
        }
        catch (Exception fehler) when (fehler is HttpRequestException or TaskCanceledException or IOException
                                           or FormatException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            log.LogError("Import der Gemeindedaten gescheitert: {Fehler}", fehler.Message);
            verzeichnis.Ladehinweis =
                $"Die Gemeindedaten konnten nicht geladen werden ({fehler.Message}). "
                + "Nach einem Neustart des Servers wird es erneut versucht.";
        }
    }
}
