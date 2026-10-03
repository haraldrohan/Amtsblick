# Amtsblick

<!-- mcp-name: io.github.haraldrohan/amtsblick -->

Amtsblick ist ein MCP-Server, der amtliche österreichische Daten über den Ort verknüpft. Ein
KI-Assistent fragt nach einer Gemeinde und bekommt Wetter und Pegelstände dazu – jeweils mit
Quellenvermerk, Stand und Einheiten.

**Amtsblick ist ein privates Open-Source-Projekt und kein offizielles Angebot einer Behörde.**

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
| `quellen()` | Quellen, Lizenzen, Vermerke, Zeitpunkt des letzten Abrufs und verbleibendes Anfragekontingent |
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

## Einbinden

Es gibt vier Wege. Der Stand der einzelnen Wege steht jeweils dabei; was noch nicht veröffentlicht
ist, ist so gekennzeichnet.

| Weg | Für wen | Voraussetzung | Stand |
|---|---|---|---|
| Connector per URL | claude.ai, Claude Desktop, Claude mobil | keine | folgt mit dem gehosteten Server |
| MCP Bundle (`.mcpb`) | Claude Desktop, lokal | keine, auch kein .NET | folgt mit dem ersten Release |
| NuGet-Paket über `dnx` | Claude Code, VS Code, Visual Studio | .NET 10 SDK | folgt mit dem ersten Release |
| Aus dem Quelltext | Entwicklung | .NET 10 SDK | verfügbar |

### Kontaktadresse

Der User-Agent lautet `Amtsblick/<version> (+<Repo-URL>; <Kontaktadresse>)`. Die Kontaktadresse
gehört dem Betreiber der jeweiligen Instanz und steht deshalb nicht im Repository. Wer Amtsblick
lokal betreibt, setzt sie über die Umgebungsvariable `Amtsblick__Kontakt`.
**Ohne Kontaktadresse ruft das Modul Wasser nichts ab;** Wetter und Ortssuche funktionieren auch ohne.
Beim gehosteten Server ist sie vom Betreiber gesetzt.

### claude.ai, Claude Desktop und Claude mobil: Connector per URL

Einstellungen → Connectors → eigenen Connector hinzufügen, als URL die Adresse des gehosteten Servers
mit dem Pfad `/mcp`. Eine Anmeldung ist nicht nötig. Die Adresse wird hier eingetragen, sobald der
Server in Betrieb ist.

### Claude Desktop lokal: MCP Bundle

Die Datei `amtsblick-<version>-<plattform>.mcpb` aus dem
[GitHub-Release](https://github.com/haraldrohan/Amtsblick/releases) laden und doppelklicken. Claude
Desktop fragt bei der Installation nach der Kontaktadresse. Ein installiertes .NET ist nicht nötig.
Plattformen: `win-x64`, `osx-arm64` (Apple Silicon), `osx-x64` (Intel-Mac), `linux-x64`; je rund 45 MB.

Beim ersten Start lädt Amtsblick einmalig die Gemeindegrenzen der Statistik Austria (rund 90 MB
Download, 60 MB auf der Platte, ein bis zwei Minuten). Bis dahin antworten die Tools mit dem Hinweis,
dass die Gemeindedaten geladen werden. Sie liegen danach unter `%LOCALAPPDATA%\Amtsblick` bzw.
`~/.local/share/Amtsblick`.

### Claude Code

```sh
# gehosteter Server
claude mcp add --transport http amtsblick <URL>/mcp

# lokal über NuGet
claude mcp add amtsblick --env Amtsblick__Kontakt=ihre.adresse@example.org -- dnx Amtsblick@<version> --yes
```

### VS Code und Visual Studio

In `.vscode/mcp.json` bzw. `.mcp.json`:

```json
{
  "servers": {
    "amtsblick": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Amtsblick@<version>", "--yes"],
      "env": { "Amtsblick__Kontakt": "ihre.adresse@example.org" }
    }
  }
}
```

Für den gehosteten Server stattdessen `{ "type": "http", "url": "<URL>/mcp" }`.

### Aus dem Quelltext

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet build -c Release
dotnet test
```

Der Server startet ohne Argument über stdio. Für Claude Code im Wurzelordner eine Datei `.mcp.json`
anlegen (sie ist von Git ausgenommen, weil sie die Kontaktadresse enthält):

```json
{
  "mcpServers": {
    "amtsblick": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["C:\Pfad\zu\Amtsblick\src\Amtsblick.Server\bin\Release\net10.0\Amtsblick.Server.dll"],
      "env": { "Amtsblick__Kontakt": "ihre.adresse@example.org" }
    }
  }
}
```

Für Claude Desktop gehört derselbe Eintrag in die `claude_desktop_config.json`. Solange ein Client
den Server geladen hat, ist die DLL gesperrt; vor einem neuen Build den Client schließen oder den
Server aus einer Kopie starten (`dotnet publish -o <Ordner>`).

Die Gemeinden lädt der Server beim ersten Start selbst. Von Hand geht es mit
`scripts/import-gemeinden.sh` (Windows: `scripts\import-gemeinden.ps1`); im Repository landen sie
unter `data/`.

## Selbst hosten

```sh
# direkt
Amtsblick__Kontakt=betrieb@example.org dotnet run --project src/Amtsblick.Server -c Release -- --http

# als Container
docker build -t amtsblick .
docker run --rm -p 8080:8080 -e Amtsblick__Kontakt=betrieb@example.org amtsblick
```

Der MCP-Endpunkt ist `/mcp` (Streamable HTTP, zustandslos), der Zustand steht unter `/health`.
Unter `/` liefert der Server eine kurze Startseite, unter `/datenschutz` die Datenschutzerklärung.
Direkt gestartet lauscht der Server auf `http://localhost:5210`, im Container auf Port 8080.

- **HTTPS** stellt der Hosting-Anbieter oder ein vorgeschalteter Reverse Proxy bereit; der Server
  selbst spricht nur HTTP.
- **Ohne Anmeldung:** Der Server liest nur öffentliche Daten. Dafür gilt ein Rate-Limit je Client-IP
  (`Http:AnfragenProMinute`, Standard 60); darüber antwortet er mit 429.
- **Origin-Prüfung:** Browser-Anfragen werden nur von `Http:ErlaubteUrspruenge` (Standard
  `https://claude.ai`, `https://claude.com`) und von localhost angenommen, sonst 403. Anfragen ohne
  Origin-Header (Server zu Server) sind erlaubt.
- **Hostnamen:** `AllowedHosts` soll den öffentlichen Hostnamen nennen, nicht `*` (Schutz vor
  DNS-Rebinding). Hinter einem Proxy `Http:HinterProxy` einschalten, damit das Rate-Limit die
  Client-IP aus `X-Forwarded-For` nimmt.
- **Logs:** nur Methode, Pfad, Statuscode und Dauer. Keine IP-Adresse, keine Anfrageinhalte, keine Orte.
- **Pegel vorladen:** Im Container ist `Module:Wasser:Vorladen` eingeschaltet; der Bestand wird
  stündlich geholt, das Abrufmuster bleibt bei einem Abruf pro Stunde.
- **Eigene Instanz:** Wer Amtsblick selbst hostet, ist dafür der Betreiber. `Http:Betreiber` mit den
  eigenen Angaben setzen und `DATENSCHUTZ.md` vor dem Bauen durch die eigene Erklärung ersetzen; die
  Datei im Repository gilt für die Instanz des Projektinhabers.

## Konfiguration

Abschnitt `Amtsblick` in `src/Amtsblick.Server/appsettings.json`; jede Einstellung lässt sich auch als
Umgebungsvariable setzen (`:` wird zu `__`).

| Einstellung | Standard | Bedeutung |
|---|---|---|
| `Kontakt` | leer | Kontaktadresse für den User-Agent; Pflicht für das Modul Wasser |
| `RepoUrl` | `https://github.com/haraldrohan/Amtsblick` | Projekt-URL im User-Agent |
| `DatenVerzeichnis` | leer | Ort der Gemeindedaten und des letzten Pegelstands; leer = `data/` im Repository, sonst `Amtsblick` im lokalen Anwendungsdatenordner des Benutzers |
| `AutoImport` | `true` | fehlende Gemeindedaten beim Start selbst laden |
| `Module:Wetter:Aktiv` | `true` | Modul Wetter samt `lage_am_ort` |
| `Module:Wasser:Aktiv` | `true` | Modul Wasser |
| `Module:Wasser:Vorladen` | `false` | nur gehostet: Pegel stündlich im Hintergrund vorladen |
| `Http:AnfragenProMinute` | `60` | Rate-Limit je Client-IP für den HTTP-Transport |
| `Http:ErlaubteUrspruenge` | `https://claude.ai`, `https://claude.com` | Ursprünge, deren Browser-Anfragen angenommen werden |
| `Http:HinterProxy` | `false` | Client-IP aus `X-Forwarded-For` des vorgeschalteten Proxys nehmen |
| `Http:Betreiber` | leer | Name, Anschrift und Kontakt des Betreibers für die Offenlegung auf der Startseite |

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

Die Pflichten aus CC BY 4.0 erfüllt jede Antwort zweifach: am Ende der Zusammenfassung steht der
Vermerk mit Lizenz, Link zum Lizenztext und dem Zusatz, dass die Daten umgerechnet und zusammengefasst
bzw. aufbereitet wurden; ausführlich steht es im Feld `quellen`: Namensnennung im verlangten
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

## Datenschutz (Privacy Policy)

Amtsblick speichert keine Anfragen und keine Daten von Nutzern und sendet keine Telemetrie. Im Speicher
liegen nur die zuletzt abgerufenen Daten der Quellen, auf der Platte nur die Gemeinden und der letzte
Pegelstand. An die Quellen gehen nur Koordinaten (auf 0,01° gerundet) bzw. der Abruf des
Gesamtbestands. Der gehostete Server protokolliert nur Methode, Pfad, Statuscode und Dauer, ohne
IP-Adresse. Die vollständige Erklärung steht in [DATENSCHUTZ.md](DATENSCHUTZ.md), deutsch und
englisch; der gehostete Server liefert sie unter `/datenschutz` aus.

## Aufbau

```
src/Amtsblick.Core            Ortsmodell, Quellenvermerk, Cache, HTTP-Basis, Kontingent, Kern-Tools
src/Amtsblick.Modules.Wetter  GeoSphere-Adapter und Tools
src/Amtsblick.Modules.Wasser  eHYD-Adapter und Tools
src/Amtsblick.Server          MCP-Host (stdio und Streamable HTTP), lage_am_ort, Import, NuGet-Paket
tests/                        Tests je Projekt; Fixtures sind gespeicherte Antworten der echten Dienste
scripts/                      Import der Referenzdaten, Bau der Programmdateien und MCP Bundles
packaging/                    Manifest für das MCP Bundle
docs/                         Prüfprotokoll, Stand der Auslieferung
infra/                        Einrichtung und Beschreibung des Hostings in Azure
data/                         importierte Referenzdaten (nicht eingecheckt)
```

Die Tests laufen vollständig ohne Netz.

## Lizenz

Code: [MIT](LICENSE). Daten: Lizenz der jeweiligen Quelle, siehe [NOTICE](NOTICE).

Hinweise für Beiträge: [CONTRIBUTING.md](CONTRIBUTING.md).
