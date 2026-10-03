# Vorab-Prüfung der Quellen

Stand: 2. Oktober 2026. Geprüft vor dem ersten Einbau der jeweiligen Quelle.

## Name „Amtsblick"

| Ort | Ergebnis |
|---|---|
| NuGet | kein Paket `Amtsblick`, 0 Suchtreffer |
| GitHub | 0 Repositories, kein Benutzer und keine Organisation dieses Namens |
| `amtsblick.at` | nicht registriert (Whois nic.at: „nothing found") |

Keine Kollision.

## MCP C#-SDK

- NuGet `ModelContextProtocol` 2.2.0 (Hosting, stdio) und `ModelContextProtocol.AspNetCore` 2.2.0
  (Streamable HTTP).
- stdio: `AddMcpServer().WithStdioServerTransport()`; Logs müssen nach stderr.
- HTTP: `WithHttpTransport()` und `MapMcp()`; zustandsloser Betrieb ist Standard und für Server ohne
  Rückfragen an den Client empfohlen. Der ältere SSE-Transport wird nicht verwendet.
- Empfehlung des SDK für lokale HTTP-Server: `AllowedHosts` auf Loopback-Namen begrenzen.

## GeoSphere Austria – Dataset API

Gelesen: OpenAPI unter `https://dataset.api.hub.geosphere.at/v1/openapi.json`
(`/v1/openapi-docs` ist die Swagger-Oberfläche dazu).

- Punkt-Zeitreihen: `GET /timeseries/forecast/{resource_id}`.
- Koordinaten-Parameter: `lat_lon`, Wert `"LAT,LON"`. Der Parameter ist ein Array, **mehrere Punkte pro
  Anfrage sind erlaubt** (Parameter wiederholen). Amtsblick fragt einen Punkt je Anfrage ab.
- `parameters` ist Pflicht, kommagetrennt möglich.
- Antwortformat: GeoJSON (Standard, `output_format=geojson`), alternativ CSV. Aufbau: `reference_time`,
  `timestamps[]`, `features[].properties.parameters.<name>.data[]`.
- Die Antwort enthält nur Zeitpunkte ab jetzt; beim Abruf waren es 54 von 61 Stunden bzw. 11 von 13 Schritten.
- Alle Mengen (`tp`, `rain`, `sf`, `sund`, `10fg`) gelten laut Metadaten „in the last forecast interval",
  sind also keine aufsummierten Werte.
- Antwort-Header: `X-RateLimit-Remaining-Hour`, `X-RateLimit-Limit-Hour` (240),
  `X-RateLimit-Remaining-Second`, `X-RateLimit-Limit-Second` (5).
- Laufzeiten am Prüftag: Der jüngste Prognoselauf war mehrere Stunden alt (um 19:05 UTC der Lauf von
  12:00 UTC). Antwortzeiten schwankten zwischen unter 1 s und 14 s.
- **Code-Tabellen:** Für das Wettersymbol `sy` und den Niederschlagstyp `pt` wurde keine amtlich
  veröffentlichte Code-Tabelle gefunden. `sy` wird deshalb nicht abgerufen, `pt` nur als Rohwert
  weitergegeben.

## eHYD – Aktuelle Pegelstände

Endpunkt: `https://gis.lfrz.gv.at/api/geodata/i000501/ogc/features/v1` (OGC API Features).

**robots.txt** (`https://gis.lfrz.gv.at/robots.txt`, zuletzt geändert 4. August 2026):

```
User-agent: *
Disallow: /
```

`https://ehyd.gv.at/robots.txt` existiert nicht (404).

Einordnung: Der Endpunkt ist im INSPIRE-Geoportal als Downloaddienst „Aktuelle Pegelstände Österreich"
unter CC BY 4.0 veröffentlicht, mit dem Vermerk „Datenquelle: ehyd.gv.at". Die `robots.txt` richtet
sich an Crawler. Amtsblick folgt keinen Links und ruft nur den dokumentierten Endpunkt ab, höchstens
einmal pro Stunde und mit Kontaktadresse im User-Agent (Abrufmuster siehe README). Die Entscheidung,
unter diesen Bedingungen abzurufen, hat der Projektinhaber am 2. Oktober 2026 getroffen. Eine
Rückfrage beim Betreiber steht aus und wäre vor einem gehosteten Betrieb sinnvoll.

**Antwortverhalten** (am Prüftag von Hand mit Projekt-URL im User-Agent: zweimal `/collections`,
einmal `items`; dazu ein Abnahmelauf des Servers mit je einem Abruf):

| Anfrage | Ergebnis |
|---|---|
| `/collections?f=json` | 200, rund 390 KB, wird vom vorgelagerten Cache ausgeliefert (`age` > 0) |
| `/collections/i000501:pegel_aktuell/items?f=json&limit=5000` | 200 in 0,13 s, rund 150 KB, 300 Messstellen in einer Seite |

- Collection-ID: `i000501:pegel_aktuell` (neben sechs weiteren Collections für Grundwasser,
  Niederschlag und Stammdaten).
- `numberMatched` = `numberReturned` = 300; geblättert werden müsste über `startIndex`.
- Keine Kontingent-Header.
- **Felder:** Die OGC-API-Ausgabe enthält `hzbnr`, `messstelle`, `gewaesser`, `hydrodienst`, `internet`,
  `parameter`, `wert`, `einheit`, `zeitpunkt`, `gesamtcode`. Die im Auftrag ebenfalls genannten Felder
  `wertw_cm`, `lat`, `lon`, `prognose` und `land` kommen darin **nicht** vor; die Position steht in der
  Geometrie. Der Parser liest sie, falls eine Ausgabe sie liefert (etwa der WFS-Layer).
- Am Prüftag kamen nur die Codes 130, 131, 230 und 930 vor; fünf Messstellen hatten keinen Wert.
- Der WFS-Fallback wurde nicht live abgerufen, um den Host nicht zusätzlich zu belasten. Er ist nur
  gegen eine nachgestellte Antwort getestet.

## Statistik Austria – Gemeinden

- Datensätze `OGDEXT_GEM_1` (Gemeinden) und `OGDEXT_POLBEZ_1` (Politische Bezirke) auf
  data.statistik.gv.at, CC BY 4.0, Gebietsstand 1. Jänner 2026. Keine `robots.txt`-Einschränkung.
- Die OGD-Metadaten nennen je Gebietsstand einen WFS-Layer. Derselbe Layer lässt sich als GeoJSON in
  WGS84 anfordern (`outputFormat=application/json&srsName=EPSG:4326`), was eine Umprojektion aus
  MGI Lambert erspart. Der Import ermittelt den neuesten Layer aus den Metadaten.
- Attribute: `g_id` (GKZ bzw. Bezirkskennziffer) und `g_name`. Eine eigene Gemeindeliste ist nicht
  nötig: Bundesland ergibt sich aus der ersten Ziffer der GKZ, der Bezirk aus den ersten drei.
- 2114 Einträge; Wien ist in 23 Gemeindebezirke geteilt (GKZ 90101 bis 92301).
