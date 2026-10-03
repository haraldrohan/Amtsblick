# Amtsblick

Amtsblick ist ein MCP-Server, der amtliche österreichische Daten über den Ort verknüpft. Ein
KI-Assistent fragt nach einer Gemeinde und bekommt Wetter und Pegelstände dazu – jeweils mit
Quellenvermerk, Stand und Einheiten.

Phase 1 umfasst den gemeinsamen Kern (Ortsverzeichnis, Quellenvermerk, Cache, Kontingent), das Modul
**Wetter** (GeoSphere Austria) und das Modul **Wasser** (eHYD-Pegel). Weitere Module folgen nach
demselben Muster.

> **Haftungshinweis.** Amtsblick ist kein amtlicher Dienst und kein Warndienst. Die Antworten geben
> Daten Dritter wieder, können verspätet, unvollständig oder falsch sein und ersetzen weder amtliche
> Unwetter- noch Hochwasserwarnungen. Maßgeblich sind die Warndienste von GeoSphere Austria und der
> Länder. Die Software wird ohne Gewähr bereitgestellt (siehe [LICENSE](LICENSE)); wer sie betreibt,
> ist für die Einhaltung der Nutzungsbedingungen der Quellen selbst verantwortlich.
> Amtsblick ist ein privates Projekt. Statistik Austria, GeoSphere Austria, das BMLUK und die
> Hydrographischen Dienste der Länder sind daran nicht beteiligt und billigen weder das Projekt noch
> die Aufbereitung ihrer Daten.

## Tools

| Tool | Zweck |
|---|---|
| `ort_finden(text)` | Gemeinde nach Name, GKZ oder Koordinate suchen |
| `quellen()` | Quellen, Lizenzen, Vermerke und verbleibendes Anfragekontingent |
| `wetter_prognose(ort, stunden = 48)` | Prognose in 6-Stunden-Blöcken plus Tageswerte |
| `niederschlag_jetzt(ort)` | Niederschlag der nächsten 3 Stunden in 15-Minuten-Schritten |
| `lage_am_ort(ort)` | Wetter, Nowcast und nächstgelegene Pegel in einem Aufruf |
| `pegel_in_der_naehe(ort, radius_km = 15)` | Pegelmessstellen im Umkreis, nach Entfernung sortiert, höchstens 20 |
| `pegel_an_gewaesser(gewaesser)` | alle Messstellen eines Gewässers |
| `hochwasserlage(bundesland?)` | Messstellen ab erhöhter Wasserführung, nach Stufe gruppiert |

`ort` ist ein Gemeindename (auch mit Tippfehler, „St." und „Sankt" sind gleichwertig), optional mit
Zusatz `"Name, Bundesland"`, eine fünfstellige GKZ oder eine Koordinate `"Breite, Länge"`. Ist ein
Name mehrdeutig, kommt statt Daten die Liste der Kandidaten zurück.

Jede Antwort ist ein kompaktes JSON-Objekt. Es beginnt mit einer Zeile `zusammenfassung` und endet
mit `quellen` und `hinweise`. Jeder Eintrag in `quellen` enthält Quelle, Lizenz mit Link zum
Lizenztext, den vorgeschriebenen Vermerk, Link, Datensatz, gegebenenfalls DOI, Stand und eine Angabe,
wie Amtsblick die Daten aufbereitet hat.

## Schnellstart

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
# 1. Gemeinden der Statistik Austria importieren (einmalig, rund 90 MB Download, 60 MB SQLite)
scripts/import-gemeinden.sh        # Windows: scripts\import-gemeinden.ps1

# 2. Bauen und testen
dotnet build -c Release
dotnet test
```

Ohne Import laufen die Tools, melden aber bei jeder Ortsangabe, dass die Referenzdaten fehlen.

### Kontaktadresse setzen

Der User-Agent lautet `Amtsblick/<version> (+<Repo-URL>; <Kontaktadresse>)`. Die Kontaktadresse
gehört dem Betreiber der jeweiligen Instanz und steht deshalb nicht im Repository. Setzen Sie sie in
`appsettings.json` (`Amtsblick:Kontakt`) oder als Umgebungsvariable `Amtsblick__Kontakt`.
**Ohne Kontaktadresse ruft das Modul Wasser nichts ab.**

### Transport 1: stdio (Claude Desktop)

In `claude_desktop_config.json` (Claude Desktop → Einstellungen → Entwickler → Konfiguration bearbeiten):

```json
{
  "mcpServers": {
    "amtsblick": {
      "command": "dotnet",
      "args": [
        "C:\\Pfad\\zu\\Amtsblick\\src\\Amtsblick.Server\\bin\\Release\\net10.0\\Amtsblick.Server.dll",
        "--stdio"
      ],
      "env": {
        "Amtsblick__Kontakt": "ihre.adresse@example.org"
      }
    }
  }
}
```

Danach Claude Desktop neu starten. Der Server sucht Konfiguration und Daten relativ zur Programmdatei,
das Arbeitsverzeichnis spielt keine Rolle. Protokollausgaben gehen nach stderr.

### stdio in Claude Code

Im Wurzelordner des Repositorys eine Datei `.mcp.json` anlegen (sie ist von Git ausgenommen, weil sie
die Kontaktadresse enthält):

```json
{
  "mcpServers": {
    "amtsblick": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "C:\\Pfad\\zu\\Amtsblick\\src\\Amtsblick.Server\\bin\\Release\\net10.0\\Amtsblick.Server.dll",
        "--stdio"
      ],
      "env": {
        "Amtsblick__Kontakt": "ihre.adresse@example.org"
      }
    }
  }
}
```

Beim nächsten Öffnen des Ordners fragt Claude Code einmal, ob der Server zugelassen werden soll.
Solange Claude Code oder Claude Desktop den Server verwenden, ist die DLL gesperrt; für einen neuen
`dotnet build -c Release` den Client vorher schließen.

### Transport 2: Streamable HTTP

```sh
Amtsblick__Kontakt=ihre.adresse@example.org dotnet run --project src/Amtsblick.Server -c Release
```

Der MCP-Endpunkt ist `http://localhost:5210/mcp` (zustandslos). Einbinden zum Beispiel mit
`claude mcp add --transport http amtsblick http://localhost:5210/mcp`.

Für den Betrieb unter einem anderen Namen oder Port `Urls` und `AllowedHosts` anpassen.
`AllowedHosts` soll die tatsächlichen Hostnamen nennen, nicht `*` (Schutz vor DNS-Rebinding).
Der Server hat keine eigene Authentifizierung; öffentlich nur hinter einem Reverse Proxy betreiben.

## Konfiguration

Abschnitt `Amtsblick` in `src/Amtsblick.Server/appsettings.json`; jede Einstellung lässt sich auch als
Umgebungsvariable setzen (`:` wird zu `__`).

| Einstellung | Standard | Bedeutung |
|---|---|---|
| `Kontakt` | leer | Kontaktadresse für den User-Agent; Pflicht für das Modul Wasser |
| `RepoUrl` | `https://github.com/haraldrohan/Amtsblick` | Projekt-URL im User-Agent |
| `DatenVerzeichnis` | leer | Ort von `gemeinden.sqlite`; leer = `data/` im Repository |
| `Module:Wetter:Aktiv` | `true` | Modul Wetter samt `lage_am_ort` |
| `Module:Wasser:Aktiv` | `true` | Modul Wasser |
| `Module:Wasser:Vorladen` | `false` | nur gehostet: Pegel stündlich im Hintergrund vorladen |

Ein beim Start abgeschaltetes Modul wird nicht registriert: seine Tools erscheinen nicht und es ruft
nichts ab. `Module:Wasser:Aktiv` wirkt zusätzlich sofort: Wird der Wert in der `appsettings.json`
neben der Programmdatei im laufenden Betrieb auf `false` gesetzt, unterbleibt jeder weitere Abruf von
eHYD ohne Neustart.

## Quellen und Lizenzvermerke

Eingebunden sind ausschließlich die folgenden freigegebenen Quellen. Alle stehen unter
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/deed.de) und sind damit auch kommerziell frei
nutzbar, sofern die Namensnennung erfolgt. Die Daten bleiben unter der Lizenz der Quelle (siehe
[NOTICE](NOTICE)); der Code steht unter MIT.

| Quelle | Datensatz | Vermerk (wörtlich zu übernehmen) | Grundlage |
|---|---|---|---|
| [Statistik Austria](https://data.statistik.gv.at) | `OGDEXT_GEM_1`, `OGDEXT_POLBEZ_1` | Datenquelle: Statistik Austria — data.statistik.gv.at | [Nutzungsbedingungen](https://data.statistik.gv.at/web/?page=terms) |
| [GeoSphere Austria](https://data.hub.geosphere.at) | `nwp-v2-1h-1km` ([DOI](https://doi.org/10.60669/rv80-9d61)), `nowcast-v1-15min-1km` ([DOI](https://doi.org/10.60669/ahad-4y43)) | Datenquelle: GeoSphere Austria - https://data.hub.geosphere.at | [Nutzungsbedingungen](https://data.hub.geosphere.at/legal), Lizenz je Datensatz |
| [eHYD](https://ehyd.gv.at) (Rechteinhaber: BMLUK und Bundesländer) | `i000501:pegel_aktuell` | Datenquelle: [ehyd.gv.at](https://ehyd.gv.at) | [INSPIRE-Metadaten](https://geoportal.inspire.gv.at/metadatensuche/inspire/api/records/6a67faa7-3ad7-4faf-91e9-17a518d10685) |

Die Pflichten aus CC BY 4.0 erfüllt jede Antwort im Feld `quellen`: Namensnennung im verlangten
Wortlaut, Lizenz mit Link zum Lizenztext, Link zur Quelle und die Angabe, was Amtsblick verändert hat:

- Statistik Austria: Gemeindemittelpunkte aus den Grenzen berechnet, Schreibweise der Bezirksnamen
  vereinheitlicht, Wien als Ganzes ergänzt.
- GeoSphere Austria: Einheiten umgerechnet; bei der Prognose zusätzlich Wind aus u/v berechnet und
  Werte zu Zeitblöcken und Tageswerten zusammengefasst.
- eHYD: Statuscode in Lage, Tendenz und Aktualität übersetzt, Gemeinde und Entfernung ergänzt,
  Auswahl nach Ort, Gewässer oder Stufe.

Wer Antworten weitergibt oder veröffentlicht, übernimmt diese Angaben. Dazu trägt jede Wetter-Antwort
den Hinweis „Keine amtliche Unwetterwarnung." und jede Pegel-Antwort „Keine amtliche Warnung.
Maßgeblich sind die Warndienste des Landes."

## Limits und Abrufmuster

### GeoSphere Austria (Wetter)

- Limits der Dataset API: 5 Anfragen pro Sekunde und 240 pro Stunde, für die ganze Instanz gemeinsam;
  darüber antwortet die API mit 429.
- Amtsblick setzt höchstens 4 Anfragen pro Sekunde ab und wertet `x-ratelimit-remaining-hour` und
  `x-ratelimit-remaining-second` aus. Der Stand ist über `quellen()` abfragbar.
- **Unter 20 verbleibenden Anfragen** wird nur noch aus dem Cache geantwortet; die Antwort sagt das.
- Cache-Schlüssel ist Datensatz + Koordinate auf 0,01° gerundet; bei einem Ortsnamen gilt der
  Gemeindemittelpunkt. Die Prognose bleibt 3 Stunden gültig oder bis die Metadaten einen neueren
  Lauf melden, der Nowcast 15 Minuten. Gleichzeitige Anfragen zum selben Punkt teilen sich einen Abruf.
- Die Metadaten (`last_forecast_reftime`) werden je Datensatz höchstens alle 15 Minuten geprüft.
  Ist der Nowcast-Lauf älter als eine Stunde, antwortet `niederschlag_jetzt` mit dem Hinweis
  „Nowcast derzeit nicht aktuell" und weicht auf die Stundenprognose aus.
- Nach einem gescheiterten Abruf wird derselbe Punkt eine Minute lang nicht erneut angefragt.
- Antwortet GeoSphere langsam, wartet ein Tool-Aufruf höchstens 8 Sekunden auf Daten (3 Sekunden auf
  Metadaten) und antwortet dann mit dem, was vorliegt – `niederschlag_jetzt` etwa mit Stundenwerten
  aus der Prognose. Der Abruf läuft im Hintergrund zu Ende und füllt den Cache für die nächste Frage;
  ein zweiter Abruf wird dafür nicht ausgelöst.
- Abgerufene Parameter: Prognose `2t, tp, rain, sf, snowlmt, 10u, 10v, 10fg, tcc, sund`; Nowcast
  `rr, pt, t2m, ff, fx`. Umgerechnet wird kg/m² → mm (1:1), m/s → km/h, `10u`/`10v` → Geschwindigkeit
  und Richtung, Sekunden → Minuten. Das Wettersymbol `sy` wird nicht ausgegeben und der
  Niederschlagstyp `pt` nur als Rohwert, weil GeoSphere dazu keine amtliche Code-Tabelle veröffentlicht.

### eHYD (Pegel)

Die `robots.txt` von `gis.lfrz.gv.at` lautet `User-agent: *` / `Disallow: /`. Sie regelt das Crawlen
der Website; die Nutzung der API regelt die für diesen Dienst veröffentlichte Lizenz (CC BY 4.0).
Amtsblick crawlt nicht, sondern ruft genau den als Open-Data-Dienst veröffentlichten Endpunkt ab, und
zwar zurückhaltend so:

- **Höchstens ein Abruf des Gesamtbestands pro 60 Minuten.** Gehostet teilen sich alle Nutzer diesen
  Abruf. Der letzte Stand und der Zeitpunkt des letzten Versuchs liegen in `data/pegel_aktuell.json`,
  sodass die Grenze auch über Neustarts hinweg gilt – wichtig für stdio, wo der Client den Server
  bei jedem Programmstart neu startet. Mehrere Installationen mit eigenem Datenverzeichnis zählen
  getrennt.
- Der erste Tool-Aufruf löst den Abruf aus, danach wird 60 Minuten lang nur der Speicher gelesen.
  Gleichzeitige Aufrufe warten auf denselben Abruf.
- Ohne Tool-Aufruf findet kein Abruf statt, auch wenn die Instanz läuft. Der Hintergrund-Timer
  (`Module:Wasser:Vorladen`) ist für den gehosteten Betrieb gedacht und standardmäßig aus.
- Auch ein gescheiterter Versuch zählt als der Abruf der Stunde; es gibt keine Wiederholungen.
  Bis zum nächsten Versuch wird der letzte Stand geliefert und mit `veraltet: true` gekennzeichnet.
- Angesprochen werden nur drei feste Adressen: `/collections` (einmal, zur Ermittlung der
  Collection-ID, die danach mitgespeichert wird), `/collections/{id}/items` und – nur wenn die OGC API ausfällt – der WFS-Layer
  `i000501:pegel_aktuell`. Links aus den Antworten werden nicht verfolgt. Antwortet der Host mit
  403 oder 429, wird nicht auf dem zweiten Weg nachgefasst.
- Jede Anfrage trägt den User-Agent mit Kontaktadresse; ohne Kontaktadresse wird nicht abgerufen.
- Das Modul ist per Konfiguration sofort abschaltbar (siehe oben).

Jede Pegel-Antwort nennt den Stand des letzten Abrufs; die Werte können bis zu einer Stunde älter
sein als beim Hydrographischen Dienst. Lage (Nieder-/Mittelwasser, erhöhte Wasserführung,
Hochwasserstufe 1–3), Tendenz und Aktualität stammen aus dem `gesamtcode` der Quelle. Amtsblick
berechnet keine eigenen Warnstufen; nicht dokumentierte Codes erscheinen als „unbekannt".

Das Protokoll der Vorab-Prüfung beider Dienste steht in [docs/vorab-pruefung.md](docs/vorab-pruefung.md).

### Statistik Austria (Gemeinden)

Wird nur vom Import-Skript abgerufen, nie im laufenden Betrieb. Die Geodaten führen Wien als
23 Gemeindebezirke; Amtsblick ergänzt „Wien" (GKZ 90001) als Zusammenfassung mit dem Mittelpunkt
der Inneren Stadt, damit die Stadt als Ganzes auffindbar ist.

## Datenschutz

Amtsblick speichert keine Anfragen und keine Daten von Nutzern und sendet keine Telemetrie. Im Speicher
liegen nur die zuletzt abgerufenen Daten der Quellen, auf der Platte nur die Gemeinden und der letzte
Pegelstand; an die Quellen gehen nur Koordinaten (auf 0,01°
gerundet) bzw. der Abruf des Gesamtbestands.

## Aufbau

```
src/Amtsblick.Core            Ortsmodell, Quellenvermerk, Cache, HTTP-Basis, Kontingent, Kern-Tools
src/Amtsblick.Modules.Wetter  GeoSphere-Adapter und Tools
src/Amtsblick.Modules.Wasser  eHYD-Adapter und Tools
src/Amtsblick.Server          MCP-Host (stdio und Streamable HTTP), lage_am_ort, Import
tests/                        Tests je Projekt; Fixtures sind gespeicherte Antworten der echten Dienste
scripts/                      Import der Referenzdaten
data/                         importierte Referenzdaten (nicht eingecheckt)
```

Die Tests laufen vollständig ohne Netz.

## Lizenz

Code: [MIT](LICENSE). Daten: Lizenz der jeweiligen Quelle, siehe [NOTICE](NOTICE).

Hinweise für Beiträge: [CONTRIBUTING.md](CONTRIBUTING.md).
