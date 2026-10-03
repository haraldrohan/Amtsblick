using System.Globalization;
using System.Text;

namespace Amtsblick.Core.Ort;

public static class Namensnormalisierung
{
    /// <summary>
    /// Vergleichsform eines Orts- oder Gewässernamens: Kleinbuchstaben, Umlaute ausgeschrieben
    /// (ä → ae, ß → ss), übrige Akzente entfernt, "St."/"St"/"Skt." → "sankt", Satz- und Bindezeichen
    /// als Leerzeichen.
    /// </summary>
    public static string Normalisiere(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var sb = new StringBuilder(text.Length + 8);
        foreach (var c in text.ToLowerInvariant().Normalize(NormalizationForm.FormC))
        {
            switch (c)
            {
                case 'ä': sb.Append("ae"); break;
                case 'ö': sb.Append("oe"); break;
                case 'ü': sb.Append("ue"); break;
                case 'ß': sb.Append("ss"); break;
                default:
                    if (char.IsLetterOrDigit(c))
                    {
                        sb.Append(OhneAkzent(c));
                    }
                    else
                    {
                        sb.Append(' ');
                    }

                    break;
            }
        }

        var woerter = sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w is "st" or "skt" ? "sankt" : w);
        return string.Join(' ', woerter);
    }

    private static string OhneAkzent(char c)
    {
        if (c < 128)
        {
            return c.ToString();
        }

        var zerlegt = c.ToString().Normalize(NormalizationForm.FormD);
        return string.Concat(zerlegt.Where(z => CharUnicodeInfo.GetUnicodeCategory(z) != UnicodeCategory.NonSpacingMark));
    }

    /// <summary>Damerau-Levenshtein-Abstand (Vertauschung benachbarter Zeichen zählt als ein Fehler).</summary>
    public static int Abstand(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var kosten = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + kosten);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[a.Length, b.Length];
    }
}
