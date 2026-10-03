# Mitwirken

Beiträge sind willkommen. Amtsblick gibt Daten Dritter weiter; deshalb gelten ein paar Grundsätze, an
denen jeder Beitrag gemessen wird.

## Grundsätze

1. **Nur freigegebene Quellen.** Eine neue Datenquelle wird erst eingebunden, wenn sie in einem Issue
   vorgeschlagen und vom Projektinhaber freigegeben wurde.
2. **Die Lizenz muss klar sein, sonst wird die Quelle nicht verwendet.** Lizenz und verlangter
   Quellenvermerk werden bei der Quelle selbst nachgelesen und mit Fundstelle in
   [docs/vorab-pruefung.md](docs/vorab-pruefung.md) festgehalten. Ist die Lizenz unklar,
   widersprüchlich oder nur vom Hörensagen bekannt, bleibt die Quelle draußen. Infrage kommen nur
   Quellen unter CC BY oder einer gleichwertigen Lizenz, die ohne Vertrag und ohne Registrierung
   nutzbar sind.
3. **Vermerke wörtlich.** Jede Tool-Antwort nennt je Quelle den Vermerk im Wortlaut des Datengebers,
   die Lizenz mit Link zum Lizenztext, den Link zur Quelle und die Angabe, was Amtsblick an den Daten
   verändert hat. Diese Angabe muss je Datensatz stimmen.
4. **Keine eigenen Warnungen oder Warnstufen.** Amtsblick gibt Einstufungen der Quelle wieder und
   übersetzt Codes in Worte. Nicht dokumentierte Codes heißen „unbekannt"; es wird nie geraten.
   Jede Antwort mit Wetter- oder Pegeldaten trägt den Hinweis, dass sie keine amtliche Warnung ist.
5. **Schonender Abruf.** Jede Quelle bekommt ein festgelegtes Abrufmuster: Cache mit Ablaufzeit, ein
   Abruf für alle Nutzer, Beachtung der Limits, keine Wiederholungen ins Leere, User-Agent mit
   Projekt-URL und Kontaktadresse. Angesprochen werden nur die dokumentierten Endpunkte.
6. **Keine Nutzerdaten, keine Telemetrie.** Anfragen werden nicht gespeichert.
7. **Bezug zum Ort.** Ein Modul muss sich über Gemeindekennziffer oder Koordinate mit dem Kern oder
   mit einem bestehenden Modul verknüpfen lassen. Eine Einzelquelle ohne diese Verknüpfung kommt
   nicht hinein; der Kern ändert sich für ein neues Modul nicht.
8. **Keine persönlichen Angaben im Repository.** Kontaktadressen gehören in die lokale Konfiguration
   (`Amtsblick__Kontakt`, `.mcp.json`), nicht in versionierte Dateien.

## Ein neues Modul

Ein Modul folgt dem Muster von `Amtsblick.Modules.Wetter` und `Amtsblick.Modules.Wasser`:

- eigenes Projekt `src/Amtsblick.Modules.<Name>` mit Adapter (HTTP und Parser), Dienst (Cache,
  Abrufmuster), Auskunft (baut die Antworten) und Tools (MCP-Attribute);
- Registrierung über `Add<Name>Modul`, einzeln abschaltbar über `Amtsblick:Module:<Name>:Aktiv`;
- ein `IQuellenAnbieter`, damit die Quelle im Tool `quellen` erscheint;
- Orte immer über den `OrtResolver` auflösen;
- Antworten über `Teilantwort` bzw. `ToolAntwort`: kompaktes JSON mit `zusammenfassung`, `quellen`
  und `hinweise`, keine Rohdumps;
- Tool-Beschreibungen auf Deutsch mit zwei bis drei Beispielfragen.

Vor dem ersten Abruf: Lizenz, Vermerk, Limits und Antwortverhalten der Quelle prüfen und im
Prüfprotokoll festhalten. README (Quellentabelle, Limits, Abrufmuster) und NOTICE ergänzen.

## Tests

```sh
dotnet test
```

- Tests laufen ohne Netz. Parser werden gegen gespeicherte Antworten der echten Dienste geprüft
  (`tests/**/Fixtures`); solche Dateien einmalig von Hand holen und in der NOTICE als Daten der Quelle
  ausweisen.
- Zeitabhängiges Verhalten (Cache, Abrufmuster, Kontingent) mit `FakeTimeProvider` prüfen.
- Für jedes Abrufmuster gibt es einen Test, der die Zahl der Upstream-Abrufe zählt.

## Stil

Bezeichner, Kommentare und Dokumentation sind deutsch. Kommentare erklären, warum etwas so ist, nicht
was die Zeile tut. Code steht unter [MIT](LICENSE); mit einem Beitrag stimmen Sie dieser Lizenz zu.
