namespace Amtsblick.Core;

/// <summary>Konfigurationsabschnitt "Amtsblick".</summary>
public sealed class AmtsblickOptionen
{
    public const string Abschnitt = "Amtsblick";

    /// <summary>Projekt-URL für den User-Agent.</summary>
    public string RepoUrl { get; set; } = "https://github.com/haraldrohan/Amtsblick";

    /// <summary>
    /// Kontaktadresse des Betreibers dieser Instanz für den User-Agent (E-Mail oder URL).
    /// Ohne Kontaktadresse ruft das Modul Wasser nichts ab.
    /// </summary>
    public string Kontakt { get; set; } = "";

    /// <summary>
    /// Verzeichnis der importierten Referenzdaten. Leer: der Ordner "data" im Repository,
    /// sonst neben der Programmdatei.
    /// </summary>
    public string DatenVerzeichnis { get; set; } = "";

    public string ErmittleDatenVerzeichnis()
    {
        if (!string.IsNullOrWhiteSpace(DatenVerzeichnis))
        {
            return Path.GetFullPath(DatenVerzeichnis);
        }

        for (var ordner = new DirectoryInfo(AppContext.BaseDirectory); ordner is not null; ordner = ordner.Parent)
        {
            if (File.Exists(Path.Combine(ordner.FullName, "Amtsblick.sln")))
            {
                return Path.Combine(ordner.FullName, "data");
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "data");
    }
}
