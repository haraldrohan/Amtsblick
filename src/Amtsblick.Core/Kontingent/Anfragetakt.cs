namespace Amtsblick.Core.Kontingent;

/// <summary>
/// Eigene Buchführung über abgesetzte Anfragen an eine Quelle, unabhängig von deren Headern:
/// bremst auf eine Höchstzahl pro Sekunde und zählt die Anfragen der letzten Stunde.
/// </summary>
public sealed class Anfragetakt(TimeProvider zeit, int proSekunde)
{
    private readonly Queue<DateTimeOffset> _letzteStunde = new();
    private readonly Lock _sperre = new();

    public int AnfragenLetzteStunde
    {
        get
        {
            lock (_sperre)
            {
                Bereinige(zeit.GetUtcNow());
                return _letzteStunde.Count;
            }
        }
    }

    /// <summary>Wartet, bis eine weitere Anfrage in das Sekundenfenster passt, und verbucht sie.</summary>
    public async Task WarteAsync(CancellationToken ct)
    {
        while (true)
        {
            TimeSpan warten;
            lock (_sperre)
            {
                var jetzt = zeit.GetUtcNow();
                Bereinige(jetzt);
                var inSekunde = _letzteStunde.Where(t => jetzt - t < TimeSpan.FromSeconds(1)).ToList();
                if (inSekunde.Count < proSekunde)
                {
                    _letzteStunde.Enqueue(jetzt);
                    return;
                }

                warten = inSekunde[0] + TimeSpan.FromSeconds(1) - jetzt;
            }

            await Task.Delay(warten, zeit, ct);
        }
    }

    private void Bereinige(DateTimeOffset jetzt)
    {
        while (_letzteStunde.TryPeek(out var aeltester) && jetzt - aeltester >= TimeSpan.FromHours(1))
        {
            _letzteStunde.Dequeue();
        }
    }
}
