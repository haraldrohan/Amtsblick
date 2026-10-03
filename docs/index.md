---
title: Amtsblick
---

# Amtsblick

Amtsblick verbindet amtliche österreichische Daten über den Ort. Ein KI-Assistent wie Claude fragt
nach einer Gemeinde und bekommt Wetter und Pegelstände dazu, mit Quelle, Lizenz und Stand.

**Amtsblick ist ein privates Open-Source-Projekt und kein offizielles Angebot einer Behörde.**

## Was Amtsblick kann

| Frage | Antwort aus |
|---|---|
| „Regnet es heute Abend in Steyr?" | Wetterprognose der GeoSphere Austria, bis 61 Stunden |
| „Regnet es in der nächsten Stunde in Linz?" | Nowcast in 15-Minuten-Schritten |
| „Wie hoch ist die Enns in Steyr?" | aktuelle Pegel des Hydrographischen Dienstes (eHYD) |
| „Gibt es gerade irgendwo Hochwasser?" | Messstellen ab erhöhter Wasserführung, nach Stufe |
| „Gib mir einen Überblick für Hallein." | Wetter, Nowcast und nächstgelegene Pegel in einem |
| „In welchem Bezirk liegt Mariazell?" | Gemeindeverzeichnis der Statistik Austria |
| „Woher stammen die Daten?" | Quellen, Lizenzen und Zeitpunkt des letzten Abrufs |

Orte sind Gemeinden. Tippfehler werden erkannt, „St." und „Sankt" sind gleichwertig, und bei
mehrdeutigen Namen wie „St. Johann" fragt Amtsblick nach.

## Einbinden

| Client | So geht es |
|---|---|
| claude.ai, Claude Desktop, Claude mobil | Einstellungen → Connectors → eigenen Connector per URL hinzufügen |
| Claude Desktop, lokal | `.mcpb`-Datei aus dem [GitHub-Release](https://github.com/haraldrohan/Amtsblick/releases) doppelklicken; kein .NET nötig |
| Claude Code | `claude mcp add --transport http amtsblick <URL>/mcp` oder lokal `claude mcp add amtsblick -- dnx Amtsblick@<version> --yes` |
| VS Code, Visual Studio | Eintrag in `mcp.json` mit URL oder `dnx` |

Die Adresse des gehosteten Servers und die Paketversion stehen in der
[README](https://github.com/haraldrohan/Amtsblick#einbinden), sobald beides veröffentlicht ist.
Dort steht auch die Anleitung für jeden Weg im Einzelnen.

## Quellen und Lizenzen

Alle Daten stehen unter [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/deed.de). Jede Antwort
nennt den Vermerk der Quelle, die Lizenz und was Amtsblick an den Daten verändert hat.

| Quelle | Daten | Vermerk |
|---|---|---|
| [GeoSphere Austria](https://data.hub.geosphere.at) | Wetterprognose, Nowcast | Datenquelle: GeoSphere Austria - https://data.hub.geosphere.at |
| [eHYD](https://ehyd.gv.at) | aktuelle Pegelstände | Datenquelle: [ehyd.gv.at](https://ehyd.gv.at) |
| [Statistik Austria](https://data.statistik.gv.at) | Gemeinden und Bezirke | Datenquelle: Statistik Austria — data.statistik.gv.at |

Amtsblick rechnet Einheiten um, fasst Werte zusammen und übersetzt Statuscodes in Worte. Die
Datengeber sind an Amtsblick nicht beteiligt und billigen weder das Projekt noch die Aufbereitung.

Die Quellen werden schonend abgerufen: Wetterdaten werden zwischengespeichert und bleiben innerhalb
der Limits der GeoSphere, der Pegelbestand wird höchstens einmal pro Stunde geholt.

## Haftungshinweis

Amtsblick ist kein amtlicher Dienst und kein Warndienst. Die Antworten geben Daten Dritter wieder,
können verspätet, unvollständig oder falsch sein und ersetzen weder amtliche Unwetter- noch
Hochwasserwarnungen. Maßgeblich sind die Warndienste von GeoSphere Austria und der Länder. Die
Software wird ohne Gewähr bereitgestellt.

## Mehr

- [Quelltext und Anleitung auf GitHub](https://github.com/haraldrohan/Amtsblick)
- [Datenschutzerklärung](datenschutz.html) · [Privacy policy](privacy.html)
- [Impressum](impressum.html)
- [Prüfprotokoll zu Quellen und Lizenzen](vorab-pruefung.html)
