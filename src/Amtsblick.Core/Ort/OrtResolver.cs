namespace Amtsblick.Core.Ort;

public enum TrefferArt
{
    /// <summary>Name stimmt nach Normalisierung vollständig überein.</summary>
    Exakt,

    /// <summary>Die Eingabe ist ein Namensteil ("Sankt Johann" in "St. Johann im Pongau").</summary>
    Teilname,

    /// <summary>Ähnlicher Name (Tippfehler).</summary>
    Unscharf,

    /// <summary>Eingabe war eine Gemeindekennziffer.</summary>
    Gkz,

    /// <summary>Eingabe war eine Koordinate.</summary>
    Koordinate,
}

public sealed record OrtTreffer(Gemeinde Gemeinde, TrefferArt Art, int Abstand = 0);

/// <summary>
/// Ergebnis für Tools, die genau einen Ort brauchen. Ist <see cref="Gemeinde"/> null, erklärt
/// <see cref="Hinweis"/> warum, und <see cref="Treffer"/> enthält die Kandidaten.
/// </summary>
public sealed record OrtAufloesung(
    Gemeinde? Gemeinde,
    Koordinate Punkt,
    IReadOnlyList<OrtTreffer> Treffer,
    string? Hinweis)
{
    public bool Eindeutig => Gemeinde is not null;
}

/// <summary>Freitext, GKZ oder Koordinate → Gemeinde.</summary>
public sealed class OrtResolver
{
    private readonly GemeindeVerzeichnis _verzeichnis;
    private volatile Suchindex? _index;

    public OrtResolver(GemeindeVerzeichnis verzeichnis)
    {
        _verzeichnis = verzeichnis;
    }

    // Der Suchindex wird beim ersten Gebrauch gebaut und neu gebaut, wenn das Verzeichnis ersetzt wurde.
    private List<(Gemeinde Gemeinde, string Norm, string[] Woerter)> Eintraege
    {
        get
        {
            var index = _index;
            var version = _verzeichnis.Version;
            if (index is null || index.Version != version)
            {
                index = new Suchindex(version, _verzeichnis.Alle
                    .Select(g =>
                    {
                        var norm = Namensnormalisierung.Normalisiere(g.Name);
                        return (g, norm, norm.Split(' '));
                    })
                    .ToList());
                _index = index;
            }

            return index.Eintraege;
        }
    }

    private sealed record Suchindex(int Version, List<(Gemeinde Gemeinde, string Norm, string[] Woerter)> Eintraege);

    /// <summary>Koordinate → Gemeinde per Punkt-in-Polygon.</summary>
    public Gemeinde? Finde(Koordinate punkt) => _verzeichnis.Finde(punkt);

    /// <summary>
    /// Treffer in dieser Reihenfolge: Koordinate, GKZ, exakter Name, Namensteil, unscharf.
    /// Die erste Stufe mit Ergebnis gewinnt. Ein Zusatz nach einem Komma ("Krumbach, Vorarlberg")
    /// schränkt auf Bundesland oder Bezirk ein.
    /// </summary>
    public IReadOnlyList<OrtTreffer> Finde(string? text, int max = 10)
    {
        text = text?.Trim() ?? "";
        if (Koordinate.TryParse(text, out var punkt))
        {
            return Finde(punkt) is { } g ? [new OrtTreffer(g, TrefferArt.Koordinate)] : [];
        }

        if (text.Length == 5 && text.All(char.IsAsciiDigit))
        {
            return _verzeichnis.NachGkz(text) is { } g ? [new OrtTreffer(g, TrefferArt.Gkz)] : [];
        }

        var komma = text.IndexOf(',');
        var gesucht = Namensnormalisierung.Normalisiere(komma < 0 ? text : text[..komma]);
        var zusatz = Namensnormalisierung.Normalisiere(komma < 0 ? "" : text[(komma + 1)..]);
        if (gesucht.Length == 0)
        {
            return [];
        }

        var treffer = Eintraege
            .Where(e => e.Norm == gesucht)
            .Select(e => new OrtTreffer(e.Gemeinde, TrefferArt.Exakt))
            .ToList();

        if (treffer.Count == 0)
        {
            var woerter = gesucht.Split(' ');
            treffer = Eintraege
                .Where(e => EnthaeltWortfolge(e.Woerter, woerter))
                .Select(e => new OrtTreffer(e.Gemeinde, TrefferArt.Teilname))
                .ToList();
        }

        if (treffer.Count == 0)
        {
            var grenze = gesucht.Length <= 4 ? 1 : gesucht.Length <= 9 ? 2 : 3;
            var wortzahl = gesucht.Count(c => c == ' ') + 1;
            treffer = Eintraege
                .Select(e => new OrtTreffer(e.Gemeinde, TrefferArt.Unscharf, UnscharferAbstand(gesucht, wortzahl, e.Norm, e.Woerter)))
                .Where(t => t.Abstand <= grenze)
                .ToList();
        }

        if (zusatz.Length > 0)
        {
            var eingeschraenkt = treffer
                .Where(t => Namensnormalisierung.Normalisiere(t.Gemeinde.Bundesland).StartsWith(zusatz, StringComparison.Ordinal)
                         || Namensnormalisierung.Normalisiere(t.Gemeinde.Bezirk).Contains(zusatz, StringComparison.Ordinal))
                .ToList();
            if (eingeschraenkt.Count > 0)
            {
                treffer = eingeschraenkt;
            }
        }

        return treffer
            .OrderBy(t => t.Abstand)
            .ThenBy(t => t.Gemeinde.Name, StringComparer.Create(new System.Globalization.CultureInfo("de-AT"), ignoreCase: true))
            .ThenBy(t => t.Gemeinde.Gkz, StringComparer.Ordinal)
            .Take(max)
            .ToList();
    }

    /// <summary>
    /// Löst die Eingabe auf genau eine Gemeinde auf oder liefert die Kandidaten samt Begründung.
    /// Der Punkt ist die eingegebene Koordinate, sonst der Gemeindemittelpunkt.
    /// </summary>
    public OrtAufloesung Loese(string? text)
    {
        if (_verzeichnis.IstLeer)
        {
            return new OrtAufloesung(null, default, [],
                _verzeichnis.Ladehinweis
                ?? "Referenzdaten fehlen: Die Gemeinden wurden noch nicht importiert (Aufruf mit dem Argument \"import\").");
        }

        var treffer = Finde(text);
        if (Koordinate.TryParse(text, out var punkt))
        {
            return treffer.Count == 1
                ? new OrtAufloesung(treffer[0].Gemeinde, punkt, treffer, null)
                : new OrtAufloesung(null, punkt, treffer, "Die Koordinate liegt in keiner österreichischen Gemeinde.");
        }

        switch (treffer.Count)
        {
            case 0:
                return new OrtAufloesung(null, default, treffer, $"Kein Ort zu \"{text}\" gefunden.");
            case 1:
            case > 1 when treffer[0].Art == TrefferArt.Unscharf && treffer[0].Abstand < treffer[1].Abstand:
                var g = treffer[0].Gemeinde;
                var hinweis = treffer[0].Art == TrefferArt.Unscharf
                    ? $"\"{text}\" wurde als {g.Name} ({g.Bundesland}) interpretiert."
                    : null;
                return new OrtAufloesung(g, g.Mittelpunkt, treffer, hinweis);
            default:
                return new OrtAufloesung(null, default, treffer,
                    $"\"{text}\" ist mehrdeutig. Bitte genauer angeben: voller Gemeindename, Zusatz mit Bundesland (\"Name, Bundesland\") oder GKZ.");
        }
    }

    private static bool EnthaeltWortfolge(string[] name, string[] gesucht)
    {
        for (var start = 0; start + gesucht.Length <= name.Length; start++)
        {
            if (name.AsSpan(start, gesucht.Length).SequenceEqual(gesucht))
            {
                return true;
            }
        }

        return false;
    }

    // Vergleicht mit dem ganzen Namen und mit seinem Anfang gleicher Wortzahl,
    // damit "Sankt Johan" auch "St. Johann im Pongau" findet.
    private static int UnscharferAbstand(string gesucht, int wortzahl, string norm, string[] woerter)
    {
        var abstand = Namensnormalisierung.Abstand(gesucht, norm);
        if (woerter.Length > wortzahl)
        {
            abstand = Math.Min(abstand, Namensnormalisierung.Abstand(gesucht, string.Join(' ', woerter, 0, wortzahl)));
        }

        return abstand;
    }
}
