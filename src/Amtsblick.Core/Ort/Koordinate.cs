using System.Globalization;

namespace Amtsblick.Core.Ort;

/// <summary>Punkt in WGS84 (geografische Breite und Länge in Grad).</summary>
public readonly record struct Koordinate(double Lat, double Lon)
{
    private const double ErdradiusKm = 6371.0088;

    /// <summary>Großkreisentfernung in Kilometern (Haversine).</summary>
    public double EntfernungKm(Koordinate andere)
    {
        var dLat = Bogen(andere.Lat - Lat);
        var dLon = Bogen(andere.Lon - Lon);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Bogen(Lat)) * Math.Cos(Bogen(andere.Lat)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * ErdradiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public Koordinate Gerundet(int stellen) => new(Math.Round(Lat, stellen), Math.Round(Lon, stellen));

    /// <summary>Liest "48.04, 14.42" (Breite, Länge). Dezimaltrennzeichen ist der Punkt.</summary>
    public static bool TryParse(string? text, out Koordinate koordinate)
    {
        koordinate = default;
        var teile = text?.Split(',', StringSplitOptions.TrimEntries);
        if (teile is not { Length: 2 }
            || !double.TryParse(teile[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            || !double.TryParse(teile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
            || lat is < -90 or > 90 || lon is < -180 or > 180)
        {
            return false;
        }

        koordinate = new Koordinate(lat, lon);
        return true;
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Lat:0.#####},{Lon:0.#####}");

    private static double Bogen(double grad) => grad * Math.PI / 180;
}
