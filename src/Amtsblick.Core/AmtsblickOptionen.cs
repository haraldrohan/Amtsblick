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
    /// Verzeichnis der importierten Referenzdaten. Leer: der Ordner "data" im Repository, wenn das
    /// Programm aus einem Arbeitsverzeichnis des Repositorys läuft, sonst "Amtsblick" im lokalen
    /// Anwendungsdatenordner des Benutzers.
    /// </summary>
    public string DatenVerzeichnis { get; set; } = "";

    /// <summary>
    /// Fehlen die Gemeindedaten beim Start, lädt der Server sie selbst von der Statistik Austria
    /// (einmalig, rund 90 MB). Aus: nur der Aufruf mit dem Argument "import" lädt sie.
    /// </summary>
    public bool AutoImport { get; set; } = true;

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

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Amtsblick");
    }
}
