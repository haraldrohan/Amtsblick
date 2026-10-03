---
title: Auslieferung
---

# Auslieferung bis zum Nutzer

Stand: 3. Oktober 2026. Dieses Dokument hält fest, was für die Auslieferung vorbereitet ist, was
noch eine Entscheidung braucht und wie die Erstveröffentlichung abläuft.

**Veröffentlicht oder deployt ist noch nichts.** Paket, Bundles und Container sind gebaut und
geprüft; NuGet, MCP Registry, Hosting und GitHub Pages warten auf die Freigabe des Projektinhabers.

## Stand je Schritt

| Schritt | Stand | Geprüft durch |
|---|---|---|
| 1 Tool-Metadaten | fertig | Tests am HTTP-Server: Titel, `readOnlyHint`, `destructiveHint`, `openWorldHint`, Server-Info, Quellenvermerk in der Zusammenfassung |
| 2 Gehosteter Server | Server und Container fertig; Hosting-Ziel offen | Tests (Rate-Limit, Origin, Health); Workflow „Container prüfen" baut das Image und fragt den laufenden Container ab |
| 3 Datenschutz, Projektseite, Impressum | Entwürfe in `docs/`; GitHub Pages noch nicht eingeschaltet | – |
| 4 NuGet-Paket | fertig, lokal gepackt | `dnx Amtsblick@0.1.0-beta --yes --add-source <Ordner>` startet den Server, acht Tools |
| 5 Programmdateien und MCP Bundle | gebaut für vier Plattformen | Windows-Datei über stdio abgefragt; Installation in Claude Desktop per Doppelklick noch nicht geprüft |
| 6 Einbindung dokumentiert | README und Projektseite | – |
| 7 Release-Automatik | Workflow `release.yml`; Deployment-Schritt fehlt bis zur Wahl des Hosting-Ziels | noch kein Lauf (läuft nur bei einem Tag) |
| 8 Erstveröffentlichung | offen, gemeinsam mit dem Projektinhaber | – |
| 9 Connectors-Verzeichnis | Checkliste und Texte unten | – |

## Abweichungen vom Zusatzauftrag

- **Registry-Beschreibung:** Das Schema begrenzt `description` auf 100 Zeichen. Der Pflichthinweis hat
  allein 91. In `server.json` steht deshalb die Kurzform „Privates Projekt, kein offizielles
  Behördenangebot"; der volle Wortlaut steht in der Paket-README, die der Eintrag verlinkt.
- **Ein MCP Bundle je Plattform:** Das Manifest unterscheidet nur Betriebssysteme, nicht Prozessoren.
  Ein gemeinsames Bundle könnte Apple Silicon und Intel-Mac nicht trennen und wäre rund 180 MB groß.
  Es gibt deshalb vier Bundles zu je rund 45 MB.
- **Vermerk-Zusatz:** „Daten umgerechnet und zusammengefasst" steht bei GeoSphere. Bei eHYD heißt es
  „Daten aufbereitet und zusammengefasst", bei der Statistik Austria „Daten aufbereitet", weil dort
  nichts umgerechnet wird und die Angabe nach CC BY 4.0 stimmen muss.
- **NuGet-Paket ist nicht eigenständig:** Es braucht die .NET-Laufzeit, die mit `dnx` ohnehin
  vorhanden ist, und bleibt dadurch bei 18 MB statt rund 180 MB für vier Plattformen.
- **Referenzdaten:** Bundle und NuGet-Paket laden die Gemeinden beim ersten Start (rund 90 MB
  Download, 60 MB auf der Platte, ein bis zwei Minuten). Der Container enthält sie bereits.
- **stdio ist Standard:** Der Server startet ohne Argument über stdio, damit `dnx Amtsblick --yes`
  genügt; HTTP braucht `--http`.

## Hosting-Optionen

Anforderung: ein kleiner .NET-Container (Image 331 MB, rund 180 MB Arbeitsspeicher), dauerhaft
laufend wegen des stündlichen Vorladens, HTTPS mit eigener Domain, Rechenzentrum in der EU.
Preise aus öffentlichen Quellen vom Oktober 2026, vor der Bestellung beim Anbieter zu prüfen.

| | Azure Container Apps | Hetzner Cloud (VPS) | Fly.io |
|---|---|---|---|
| Region | Westeuropa (Niederlande) oder Deutschland; Österreich-Region prüfen | Deutschland oder Finnland | Frankfurt |
| Anbieter | Microsoft (USA, EU-Vertragspartner in Irland) | Hetzner (Deutschland) | Fly.io (USA) |
| Kosten pro Monat | rund 3–6 USD bei 0,25 vCPU und 0,5 GB dauerhaft; Freikontingent von 180 000 vCPU-Sekunden und 360 000 GiB-Sekunden angerechnet | rund 6–7 EUR pauschal (CX23 mit 2 vCPU, 4 GB, plus IPv4) | rund 4–8 USD (shared-cpu-1x, 512 MB bis 1 GB, Frankfurt mit Aufschlag) |
| HTTPS und Domain | verwaltetes Zertifikat kostenlos, eigene Domain per DNS-Eintrag | selbst: Caddy oder ähnlicher Proxy mit Let's Encrypt | Zertifikat inklusive, eigene Domain per DNS-Eintrag |
| Aufwand Einrichtung | mittel: Ressourcengruppe, Umgebung, App, Domain | hoch: Server, Docker, Proxy, Firewall, Updates | gering: eine Konfigurationsdatei |
| Aufwand Betrieb | gering, keine Serverpflege | laufend: Sicherheitsupdates, Überwachung | gering |
| Deployment aus GitHub Actions | fertige Action, Anmeldung per OIDC | per SSH und `docker compose pull` | `flyctl deploy` mit Token |
| Protokolle | Log Analytics, Aufbewahrung einstellbar | vollständig in eigener Hand | beim Anbieter, kurze Aufbewahrung |
| Datenschutz | Auftragsverarbeitung über Microsoft-Vertrag, EU-Region | EU-Anbieter, Auftragsverarbeitungsvertrag online | US-Anbieter, EU-Region; Vertrag und Drittlandbezug prüfen |

Einschätzung: Am wenigsten Arbeit im Betrieb machen Azure Container Apps und Fly.io. Am klarsten
für die Datenschutzerklärung ist Hetzner, kostet aber laufende Serverpflege. Die Azure-Kommandozeile
ist auf dem Entwicklungsrechner bereits installiert.

Zusätzlich nötig: eine Domain (zum Beispiel `amtsblick.at`, am 2. Oktober 2026 frei) und die
Kontaktadresse als Einstellung `Amtsblick__Kontakt` beim Anbieter.

Quellen der Preise:
[Azure Container Apps](https://www.devzero.io/blog/azure-container-apps-pricing),
[Hetzner](https://comparedge.com/tools/hetzner/pricing),
[Fly.io](https://fly.io/docs/about/pricing/).

## Ablauf der Erstveröffentlichung

Erste öffentliche Version ist `0.1.0-beta`, weil NuGet-Versionen unveränderlich sind.

1. Hosting-Ziel wählen, Domain einrichten, Deployment-Schritt im Release-Workflow ergänzen.
2. Offene Angaben in Datenschutzerklärung und Impressum eintragen, GitHub Pages einschalten
   (Quelle: Ordner `docs/` im Zweig `main`).
3. Gehosteten Server deployen und in claude.ai als eigenen Connector per URL prüfen: acht Tools,
   die Beispielfragen zu Steyr.
4. Adresse des Servers als `remotes` in `.mcp/server.json` und in README und Projektseite eintragen.
5. Testlauf: `dotnet pack`, `dnx` gegen den lokalen Ordner, `mcp-publisher validate`.
6. NuGet-Konto: API-Schlüssel als Secret `NUGET_API_KEY` im Repository hinterlegen; Adresse des
   Servers als Variable `SERVER_URL`.
7. Tag `v0.1.0-beta` setzen. Der Workflow veröffentlicht Release, Paket, Image und Registry-Eintrag.
8. Nachprüfung: Paket auf NuGet.org als MCP-Server sichtbar, Eintrag in der Registry abrufbar,
   `.mcpb` in Claude Desktop per Doppelklick installierbar.

Offen aus Phase 1: Im Repository gibt es bereits ein GitHub-Release `v0.1.0` ohne Dateien. Nach den
Regeln für Versionsnummern liegt `0.1.0-beta` davor. Vor dem ersten Tag ist zu entscheiden, ob
dieses Release gelöscht oder die erste Paketversion höher angesetzt wird.

## Connectors-Verzeichnis von Claude

Anforderungen laut [Einreichungsseite](https://claude.com/docs/connectors/building/submission),
gelesen am 3. Oktober 2026. Die Einreichung selbst macht der Projektinhaber über
[claude.ai/directory/manage](https://claude.ai/directory/manage).

| Anforderung | Ist-Stand |
|---|---|
| Server entfernt erreichbar über HTTPS | offen: Hosting |
| Authentifizierung: OAuth 2.0 oder keine bei öffentlichen Daten | erfüllt: keine Anmeldung, nur öffentliche Daten |
| Jedes Tool mit `title` und `readOnlyHint` bzw. `destructiveHint` | erfüllt, durch Test abgesichert |
| In Claude als eigener Connector getestet, jedes Tool aufgerufen | offen: nach dem Deployment |
| Dokumentations-URL | vorbereitet: README bzw. Projektseite |
| URL der Datenschutzerklärung | vorbereitet: `docs/datenschutz.md`, `docs/privacy.md`; Pages einschalten, offene Angaben ergänzen |
| Support-Kontakt | offen: Kontaktadresse oder GitHub Issues |
| Icon | vorhanden: `assets/icon-512.png` |
| Testzugang für Prüfer | entfällt: keine Anmeldung; Testanleitung unten |
| Konto, das einreichen darf | offen: bezahlter Claude-Tarif des Projektinhabers |
| Verzeichnisbedingungen und -richtlinie akzeptiert, sieben Bestätigungen | bei der Einreichung |

Lokale Server als MCP Bundle nimmt das Verzeichnis nicht mehr an; das Bundle wird über das
GitHub-Release verteilt.

### Texte für die Einreichung

**Name** (höchstens 100 Zeichen): Amtsblick

**Einzeiler** (höchstens 200 Zeichen):
Weather forecasts and river gauge levels for Austrian municipalities, from official open data.
Private open-source project, not an official government service.

**Beschreibung** (höchstens 2000 Zeichen):

> Amtsblick connects official Austrian open data by place. Ask about a municipality and get the
> weather forecast, a 15-minute precipitation nowcast and current river gauge levels, each with its
> source, licence and timestamp.
>
> Data sources: GeoSphere Austria (forecast and nowcast), eHYD, the Austrian hydrographic service
> (current gauge levels with the flood stage assigned by the authority), and Statistik Austria
> (municipal boundaries). All data is licensed under CC BY 4.0; every answer names the source and
> states how the data was processed.
>
> Amtsblick is a private open-source project and not an official service of any public authority.
> Its answers are not official weather or flood warnings. The tools respond in German.
>
> No account is required. The connector only reads public data and stores no requests.

**Kategorien:** Wetter, Daten, Behörden und öffentlicher Sektor (je nach Auswahl im Portal)

**Anwendungsfälle:** Wetter für eine Gemeinde abfragen; Niederschlag der nächsten Stunden; Pegel in
der Nähe eines Ortes oder an einem Fluss; Überblick über die Hochwasserlage; Gemeinde, Bezirk und
Bundesland zu einem Namen oder einer Koordinate finden.

**Liest oder schreibt:** nur lesend.

**Datenverarbeitung:** Schnittstellen Dritter, die der Betreiber nicht kontrolliert (öffentliche
Open-Data-Dienste unter CC BY 4.0); keine Gesundheitsdaten, keine gesponserten Inhalte.

### Testanleitung für Prüfer (zehn Minuten)

1. Add the connector by URL; no sign-in is needed.
2. Ask: "Regnet es heute Abend in Steyr?" Expect `wetter_prognose` with temperatures, precipitation
   and the note "Datenquelle: GeoSphere Austria".
3. Ask: "Wie hoch ist die Enns in Steyr?" Expect `pegel_in_der_naehe` with the gauge "Steyr
   (Ortskai)", a value, the stage and the note "Datenquelle: ehyd.gv.at".
4. Ask: "Wie wird das Wetter in St. Johann?" Expect a list of eight municipalities to choose from.
5. Ask: "Gibt es gerade irgendwo Hochwasser?" Expect `hochwasserlage`.
6. Ask: "Woher stammen die Daten?" Expect `quellen` with three sources and licences.
7. The remaining tools are `ort_finden`, `niederschlag_jetzt`, `lage_am_ort` and `pegel_an_gewaesser`;
   example questions are in each tool description.

All tools are read-only. Gauge data is refreshed at most once per hour.
