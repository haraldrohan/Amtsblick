namespace Amtsblick.Modules.Wetter;

public static class Umrechnung
{
    private static readonly string[] Richtungen = ["N", "NO", "O", "SO", "S", "SW", "W", "NW"];

    public static double KmhAusMs(double meterProSekunde) => meterProSekunde * 3.6;

    /// <summary>1 kg Wasser je m² entspricht 1 mm Niederschlagshöhe.</summary>
    public static double MmAusKgProM2(double kgProM2) => kgProM2;

    public static double MinutenAusSekunden(double sekunden) => sekunden / 60;

    /// <summary>
    /// Wind aus den Komponenten u (nach Osten) und v (nach Norden) in m/s. Die Richtung ist
    /// meteorologisch, also die Richtung, aus der der Wind weht (0° = Nord, 90° = Ost);
    /// bei Windstille gibt es keine.
    /// </summary>
    public static (double Kmh, double? RichtungGrad) Wind(double u, double v)
    {
        var geschwindigkeit = Math.Sqrt(u * u + v * v);
        if (geschwindigkeit < 1e-9)
        {
            return (0, null);
        }

        var grad = Math.Atan2(-u, -v) * 180 / Math.PI;
        return (KmhAusMs(geschwindigkeit), (grad + 360) % 360);
    }

    /// <summary>Achtteilige Windrose: N, NO, O, SO, S, SW, W, NW.</summary>
    public static string Himmelsrichtung(double grad) =>
        Richtungen[(int)Math.Round(((grad % 360) + 360) % 360 / 45, MidpointRounding.AwayFromZero) % 8];
}
