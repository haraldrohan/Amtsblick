using System.Buffers.Binary;
using System.Text.Json;

namespace Amtsblick.Core.Ort;

/// <summary>
/// Fläche einer Gemeinde als Multipolygon in WGS84. Je Polygon ist der erste Ring der Außenring,
/// alle weiteren sind Löcher.
/// </summary>
public sealed class Flaeche
{
    private const uint WkbPolygon = 3;
    private const uint WkbMultiPolygon = 6;

    public Flaeche(IReadOnlyList<IReadOnlyList<Koordinate[]>> polygone)
    {
        if (polygone.Count == 0 || polygone.Any(p => p.Count == 0 || p[0].Length < 3))
        {
            throw new ArgumentException("Eine Fläche braucht mindestens ein Polygon mit Außenring.", nameof(polygone));
        }

        Polygone = polygone;
        var punkte = polygone.SelectMany(p => p[0]).ToList();
        MinLat = punkte.Min(p => p.Lat);
        MaxLat = punkte.Max(p => p.Lat);
        MinLon = punkte.Min(p => p.Lon);
        MaxLon = punkte.Max(p => p.Lon);
    }

    public IReadOnlyList<IReadOnlyList<Koordinate[]>> Polygone { get; }
    public double MinLat { get; }
    public double MaxLat { get; }
    public double MinLon { get; }
    public double MaxLon { get; }

    public bool Enthaelt(Koordinate punkt)
    {
        if (punkt.Lat < MinLat || punkt.Lat > MaxLat || punkt.Lon < MinLon || punkt.Lon > MaxLon)
        {
            return false;
        }

        return Polygone.Any(polygon => ImPolygon(polygon, punkt));
    }

    /// <summary>
    /// Flächenschwerpunkt des größten Polygons. Liegt er außerhalb (z. B. bei sichelförmigen Gemeinden),
    /// wird ein Punkt auf der breitesten Stelle derselben Breite innerhalb der Fläche gewählt.
    /// </summary>
    public Koordinate Mittelpunkt()
    {
        var groesstes = Polygone.MaxBy(p => Math.Abs(Ringflaeche(p[0])))!;
        var schwerpunkt = Schwerpunkt(groesstes[0]);
        if (ImPolygon(groesstes, schwerpunkt))
        {
            return schwerpunkt;
        }

        var schnitte = new List<double>();
        foreach (var ring in groesstes)
        {
            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            {
                var (a, b) = (ring[i], ring[j]);
                if (a.Lat > schwerpunkt.Lat != b.Lat > schwerpunkt.Lat)
                {
                    schnitte.Add(a.Lon + (schwerpunkt.Lat - a.Lat) / (b.Lat - a.Lat) * (b.Lon - a.Lon));
                }
            }
        }

        schnitte.Sort();
        var beste = schwerpunkt;
        double breite = -1;
        for (var i = 0; i + 1 < schnitte.Count; i += 2)
        {
            if (schnitte[i + 1] - schnitte[i] > breite)
            {
                breite = schnitte[i + 1] - schnitte[i];
                beste = new Koordinate(schwerpunkt.Lat, (schnitte[i] + schnitte[i + 1]) / 2);
            }
        }

        return beste;
    }

    /// <summary>Liest eine GeoJSON-Geometrie vom Typ Polygon oder MultiPolygon (Koordinaten als [lon, lat]).</summary>
    public static Flaeche AusGeoJson(JsonElement geometrie)
    {
        var typ = geometrie.GetProperty("type").GetString();
        var koordinaten = geometrie.GetProperty("coordinates");
        var polygone = typ switch
        {
            "Polygon" => [LiesPolygon(koordinaten)],
            "MultiPolygon" => koordinaten.EnumerateArray().Select(LiesPolygon).ToList(),
            _ => throw new FormatException($"Geometrietyp '{typ}' wird nicht unterstützt."),
        };
        return new Flaeche(polygone);

        static IReadOnlyList<Koordinate[]> LiesPolygon(JsonElement polygon) =>
            polygon.EnumerateArray()
                .Select(ring => ring.EnumerateArray()
                    .Select(p => new Koordinate(p[1].GetDouble(), p[0].GetDouble()))
                    .ToArray())
                .ToList();
    }

    /// <summary>Well-Known Binary (Little Endian, MultiPolygon, x = Länge, y = Breite).</summary>
    public byte[] AlsWkb()
    {
        using var strom = new MemoryStream();
        using var w = new BinaryWriter(strom);
        w.Write((byte)1);
        w.Write(WkbMultiPolygon);
        w.Write((uint)Polygone.Count);
        foreach (var polygon in Polygone)
        {
            w.Write((byte)1);
            w.Write(WkbPolygon);
            w.Write((uint)polygon.Count);
            foreach (var ring in polygon)
            {
                w.Write((uint)ring.Length);
                foreach (var punkt in ring)
                {
                    w.Write(punkt.Lon);
                    w.Write(punkt.Lat);
                }
            }
        }

        w.Flush();
        return strom.ToArray();
    }

    public static Flaeche AusWkb(ReadOnlySpan<byte> wkb)
    {
        var pos = 0;
        var typ = LiesKopf(wkb, ref pos);
        if (typ == WkbPolygon)
        {
            return new Flaeche([LiesPolygon(wkb, ref pos)]);
        }

        if (typ != WkbMultiPolygon)
        {
            throw new FormatException($"WKB-Typ {typ} wird nicht unterstützt.");
        }

        var anzahl = LiesUInt(wkb, ref pos);
        var polygone = new List<IReadOnlyList<Koordinate[]>>((int)anzahl);
        for (var i = 0; i < anzahl; i++)
        {
            if (LiesKopf(wkb, ref pos) != WkbPolygon)
            {
                throw new FormatException("MultiPolygon enthält ein Element, das kein Polygon ist.");
            }

            polygone.Add(LiesPolygon(wkb, ref pos));
        }

        return new Flaeche(polygone);

        static uint LiesKopf(ReadOnlySpan<byte> daten, ref int pos)
        {
            if (daten[pos++] != 1)
            {
                throw new FormatException("Nur Little-Endian-WKB wird unterstützt.");
            }

            return LiesUInt(daten, ref pos);
        }

        static uint LiesUInt(ReadOnlySpan<byte> daten, ref int pos)
        {
            var wert = BinaryPrimitives.ReadUInt32LittleEndian(daten[pos..]);
            pos += 4;
            return wert;
        }

        static IReadOnlyList<Koordinate[]> LiesPolygon(ReadOnlySpan<byte> daten, ref int pos)
        {
            var ringe = new Koordinate[LiesUInt(daten, ref pos)][];
            for (var r = 0; r < ringe.Length; r++)
            {
                var ring = new Koordinate[LiesUInt(daten, ref pos)];
                for (var p = 0; p < ring.Length; p++)
                {
                    var lon = BinaryPrimitives.ReadDoubleLittleEndian(daten[pos..]);
                    var lat = BinaryPrimitives.ReadDoubleLittleEndian(daten[(pos + 8)..]);
                    pos += 16;
                    ring[p] = new Koordinate(lat, lon);
                }

                ringe[r] = ring;
            }

            return ringe;
        }
    }

    private static bool ImPolygon(IReadOnlyList<Koordinate[]> polygon, Koordinate punkt)
    {
        if (!ImRing(polygon[0], punkt))
        {
            return false;
        }

        for (var i = 1; i < polygon.Count; i++)
        {
            if (ImRing(polygon[i], punkt))
            {
                return false;
            }
        }

        return true;
    }

    // Strahlverfahren: zählt die Kanten, die ein Strahl vom Punkt nach Osten schneidet.
    private static bool ImRing(Koordinate[] ring, Koordinate punkt)
    {
        var innen = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            var (a, b) = (ring[i], ring[j]);
            if (a.Lat > punkt.Lat != b.Lat > punkt.Lat
                && punkt.Lon < (b.Lon - a.Lon) * (punkt.Lat - a.Lat) / (b.Lat - a.Lat) + a.Lon)
            {
                innen = !innen;
            }
        }

        return innen;
    }

    private static double Ringflaeche(Koordinate[] ring)
    {
        double summe = 0;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            summe += ring[j].Lon * ring[i].Lat - ring[i].Lon * ring[j].Lat;
        }

        return summe / 2;
    }

    private static Koordinate Schwerpunkt(Koordinate[] ring)
    {
        var flaeche = Ringflaeche(ring);
        if (Math.Abs(flaeche) < 1e-12)
        {
            return new Koordinate(ring.Average(p => p.Lat), ring.Average(p => p.Lon));
        }

        double lat = 0, lon = 0;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            var kreuz = ring[j].Lon * ring[i].Lat - ring[i].Lon * ring[j].Lat;
            lon += (ring[j].Lon + ring[i].Lon) * kreuz;
            lat += (ring[j].Lat + ring[i].Lat) * kreuz;
        }

        return new Koordinate(lat / (6 * flaeche), lon / (6 * flaeche));
    }
}
