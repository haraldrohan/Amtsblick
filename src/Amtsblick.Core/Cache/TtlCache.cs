using System.Collections.Concurrent;

namespace Amtsblick.Core.Cache;

public sealed record CacheEintrag<T>(T Wert, DateTimeOffset Abgerufen, DateTimeOffset GueltigBis);

/// <summary>
/// In-Memory-Cache mit Ablaufzeit je Eintrag. Gleichzeitige Anfragen zum selben Schlüssel teilen sich
/// einen Ladevorgang, sodass ein Upstream-Abruf alle Nutzer bedient. Abgelaufene Einträge bleiben
/// lesbar, damit bei Störung oder knappem Kontingent der letzte Stand geliefert werden kann.
/// </summary>
public sealed class TtlCache<TKey, TValue>(TimeProvider zeit, int maxEintraege = 5000)
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, CacheEintrag<TValue>> _eintraege = new();
    private readonly ConcurrentDictionary<TKey, Lazy<Task<CacheEintrag<TValue>>>> _laufend = new();

    /// <summary>Liefert den Eintrag, auch wenn er abgelaufen ist.</summary>
    public CacheEintrag<TValue>? Lies(TKey schluessel) => _eintraege.GetValueOrDefault(schluessel);

    public bool IstFrisch(CacheEintrag<TValue> eintrag) => zeit.GetUtcNow() < eintrag.GueltigBis;

    /// <summary>Frischer Eintrag aus dem Cache, sonst laden.</summary>
    public Task<CacheEintrag<TValue>> HoleAsync(
        TKey schluessel, Func<CancellationToken, Task<(TValue Wert, TimeSpan Ttl)>> laden, CancellationToken ct = default) =>
        Lies(schluessel) is { } eintrag && IstFrisch(eintrag)
            ? Task.FromResult(eintrag)
            : LadeAsync(schluessel, laden, ct);

    /// <summary>
    /// Lädt neu und ersetzt den Eintrag. Läuft für den Schlüssel bereits ein Ladevorgang, wird dessen
    /// Ergebnis mitbenutzt. Der Ladevorgang selbst ist nicht an das Abbruchsignal eines einzelnen
    /// Aufrufers gebunden, weil andere auf dasselbe Ergebnis warten können.
    /// </summary>
    public async Task<CacheEintrag<TValue>> LadeAsync(
        TKey schluessel, Func<CancellationToken, Task<(TValue Wert, TimeSpan Ttl)>> laden, CancellationToken ct = default)
    {
        var lazy = _laufend.GetOrAdd(schluessel, s => new Lazy<Task<CacheEintrag<TValue>>>(() => Lade(s, laden)));
        return await lazy.Value.WaitAsync(ct);
    }

    private async Task<CacheEintrag<TValue>> Lade(
        TKey schluessel, Func<CancellationToken, Task<(TValue Wert, TimeSpan Ttl)>> laden)
    {
        try
        {
            var (wert, ttl) = await laden(CancellationToken.None);
            var jetzt = zeit.GetUtcNow();
            var eintrag = new CacheEintrag<TValue>(wert, jetzt, jetzt + ttl);
            _eintraege[schluessel] = eintrag;
            if (_eintraege.Count > maxEintraege)
            {
                Raeume(jetzt);
            }

            return eintrag;
        }
        finally
        {
            _laufend.TryRemove(schluessel, out _);
        }
    }

    // Entfernt zuerst Abgelaufenes; reicht das nicht, die ältesten Einträge.
    private void Raeume(DateTimeOffset jetzt)
    {
        foreach (var (schluessel, eintrag) in _eintraege)
        {
            if (eintrag.GueltigBis <= jetzt)
            {
                _eintraege.TryRemove(schluessel, out _);
            }
        }

        foreach (var (schluessel, _) in _eintraege.OrderBy(e => e.Value.Abgerufen).Take(Math.Max(0, _eintraege.Count - maxEintraege)))
        {
            _eintraege.TryRemove(schluessel, out _);
        }
    }
}
