using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Amtsblick.Core.Ort;

/// <summary>SQLite-Ablage der importierten Gemeinden (data/gemeinden.sqlite).</summary>
public static class GemeindeDatenbank
{
    public const string Dateiname = "gemeinden.sqlite";

    public static void Speichere(string pfad, IEnumerable<Gemeinde> gemeinden, DateOnly gebietsstand, DateTimeOffset importiert)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pfad))!);
        using var verbindung = Oeffne(pfad, SqliteOpenMode.ReadWriteCreate);
        using var transaktion = verbindung.BeginTransaction();

        Ausfuehren(verbindung, """
            DROP TABLE IF EXISTS gemeinde;
            DROP TABLE IF EXISTS meta;
            CREATE TABLE gemeinde (
                gkz TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                bezirk TEXT NOT NULL,
                bundesland TEXT NOT NULL,
                lat REAL NOT NULL,
                lon REAL NOT NULL,
                polygon BLOB
            );
            CREATE TABLE meta (schluessel TEXT PRIMARY KEY, wert TEXT NOT NULL);
            """);

        using (var einfuegen = verbindung.CreateCommand())
        {
            einfuegen.CommandText = "INSERT INTO gemeinde VALUES ($gkz, $name, $bezirk, $bundesland, $lat, $lon, $polygon)";
            var gkz = einfuegen.Parameters.Add("$gkz", SqliteType.Text);
            var name = einfuegen.Parameters.Add("$name", SqliteType.Text);
            var bezirk = einfuegen.Parameters.Add("$bezirk", SqliteType.Text);
            var bundesland = einfuegen.Parameters.Add("$bundesland", SqliteType.Text);
            var lat = einfuegen.Parameters.Add("$lat", SqliteType.Real);
            var lon = einfuegen.Parameters.Add("$lon", SqliteType.Real);
            var polygon = einfuegen.Parameters.Add("$polygon", SqliteType.Blob);
            foreach (var g in gemeinden)
            {
                gkz.Value = g.Gkz;
                name.Value = g.Name;
                bezirk.Value = g.Bezirk;
                bundesland.Value = g.Bundesland;
                lat.Value = g.Mittelpunkt.Lat;
                lon.Value = g.Mittelpunkt.Lon;
                polygon.Value = g.Flaeche is null ? DBNull.Value : g.Flaeche.AlsWkb();
                einfuegen.ExecuteNonQuery();
            }
        }

        SetzeMeta(verbindung, "gebietsstand", gebietsstand.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        SetzeMeta(verbindung, "importiert", importiert.ToString("o", CultureInfo.InvariantCulture));
        transaktion.Commit();
    }

    /// <summary>Lädt alle Gemeinden. Fehlt die Datei, ist das Verzeichnis leer.</summary>
    public static GemeindeVerzeichnis Lade(string pfad)
    {
        if (!File.Exists(pfad))
        {
            return GemeindeVerzeichnis.Leer;
        }

        using var verbindung = Oeffne(pfad, SqliteOpenMode.ReadOnly);
        var gemeinden = new List<Gemeinde>();
        using (var abfrage = verbindung.CreateCommand())
        {
            abfrage.CommandText = "SELECT gkz, name, bezirk, bundesland, lat, lon, polygon FROM gemeinde";
            using var leser = abfrage.ExecuteReader();
            while (leser.Read())
            {
                gemeinden.Add(new Gemeinde(
                    leser.GetString(0),
                    leser.GetString(1),
                    leser.GetString(2),
                    leser.GetString(3),
                    new Koordinate(leser.GetDouble(4), leser.GetDouble(5)),
                    leser.IsDBNull(6) ? null : Flaeche.AusWkb((byte[])leser[6])));
            }
        }

        DateOnly? gebietsstand = null;
        using (var abfrage = verbindung.CreateCommand())
        {
            abfrage.CommandText = "SELECT wert FROM meta WHERE schluessel = 'gebietsstand'";
            if (abfrage.ExecuteScalar() is string wert
                && DateOnly.TryParseExact(wert, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var datum))
            {
                gebietsstand = datum;
            }
        }

        return new GemeindeVerzeichnis(gemeinden, gebietsstand);
    }

    private static SqliteConnection Oeffne(string pfad, SqliteOpenMode modus)
    {
        // Pooling aus, damit die Datei nach dem Schließen nicht gesperrt bleibt.
        var verbindung = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = pfad,
            Mode = modus,
            Pooling = false,
        }.ToString());
        verbindung.Open();
        return verbindung;
    }

    private static void Ausfuehren(SqliteConnection verbindung, string sql)
    {
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = sql;
        befehl.ExecuteNonQuery();
    }

    private static void SetzeMeta(SqliteConnection verbindung, string schluessel, string wert)
    {
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = "INSERT INTO meta VALUES ($s, $w)";
        befehl.Parameters.AddWithValue("$s", schluessel);
        befehl.Parameters.AddWithValue("$w", wert);
        befehl.ExecuteNonQuery();
    }
}
