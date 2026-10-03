---
title: Datenschutzerklärung
---

# Datenschutzerklärung

> **Entwurf.** Stand: 3. Oktober 2026. Die mit ⚠ markierten Angaben sind noch offen und werden vor
> der Inbetriebnahme des gehosteten Servers ergänzt.

Amtsblick ist ein privates Open-Source-Projekt und kein offizielles Angebot einer Behörde.

## Verantwortlicher

⚠ Name und Anschrift des Betreibers: siehe [Impressum](impressum.html).
⚠ Kontakt in Datenschutzfragen: [Kontaktadresse wird ergänzt]

## Kurzfassung

Amtsblick liest öffentliche Daten und gibt sie weiter. Es gibt keine Konten, keine Cookies, kein
Tracking und keine Telemetrie. Anfragen und abgefragte Orte werden nicht gespeichert.

## Der gehostete Server

Wer Amtsblick als Connector über die Adresse des gehosteten Servers nutzt, löst folgende
Verarbeitungen aus:

| Daten | Zweck | Speicherung |
|---|---|---|
| IP-Adresse der anfragenden Stelle | Übertragung der Antwort; Begrenzung der Anfragen je Adresse (60 pro Minute) | nur im Arbeitsspeicher, für die Dauer des Zählfensters von einer Minute; nicht im Protokoll |
| Inhalt der Anfrage (Tool und Parameter, etwa der Ortsname) | Beantwortung der Anfrage | nur im Arbeitsspeicher während der Bearbeitung; nicht im Protokoll |
| Methode, Pfad, Statuscode, Dauer | technischer Betrieb und Fehlersuche | im Protokoll des Servers; ⚠ Aufbewahrung: [Dauer laut Hosting-Anbieter, höchstens 14 Tage] |

Bei Connectoren in claude.ai kommt die Anfrage in der Regel von Servern von Anthropic, nicht direkt
von Ihrem Gerät. Amtsblick erfährt dann Ihre IP-Adresse nicht.

Rechtsgrundlage ist das berechtigte Interesse am sicheren Betrieb des Dienstes (Art. 6 Abs. 1 lit. f
DSGVO).

### Was der Server an Dritte sendet

- **GeoSphere Austria:** die Koordinate des abgefragten Ortes, auf 0,01° gerundet (etwa 1 km).
  Standard ist der Mittelpunkt der Gemeinde. Die Anfrage kommt vom Server, nicht von Ihnen;
  GeoSphere erfährt nicht, wer gefragt hat.
- **eHYD (gis.lfrz.gv.at):** höchstens einmal pro Stunde der Abruf des gesamten Pegelbestands, ohne
  Bezug zu einer einzelnen Anfrage.
- **Statistik Austria:** nur beim Bau des Servers, zum Laden der Gemeindegrenzen.

Jede dieser Anfragen trägt einen User-Agent mit der Projektadresse und der Kontaktadresse des
Betreibers, aber keine Angaben über Nutzer.

### Hosting

⚠ Hosting-Anbieter: [wird nach der Auswahl eingetragen, mit Sitz, Region des Rechenzentrums und
Verweis auf den Auftragsverarbeitungsvertrag]. Vorgesehen ist ein Rechenzentrum in der EU. Ob der Anbieter an
seinem Eingang eigene Zugriffsprotokolle mit IP-Adressen führt und wie lange, wird dann hier ergänzt.

## Lokale Nutzung

Wer Amtsblick auf dem eigenen Rechner betreibt (MCP Bundle, NuGet-Paket oder aus dem Quelltext),
nutzt den gehosteten Server nicht. Dann gilt:

- Das Programm läuft bei Ihnen. Der Betreiber von Amtsblick erhält keine Daten.
- Die Anfragen an GeoSphere Austria, eHYD und – beim ersten Start – Statistik Austria gehen von
  Ihrem Rechner aus. Diese Stellen sehen Ihre IP-Adresse und den User-Agent mit der Kontaktadresse,
  die Sie selbst eingetragen haben.
- Auf Ihrem Rechner liegen die Gemeindedaten und der letzte Pegelstand, sonst nichts.

## Der KI-Assistent

Ihre Unterhaltung führen Sie mit einem KI-Assistenten, etwa Claude. Was dieser verarbeitet und
speichert, regelt dessen Anbieter in seiner eigenen Datenschutzerklärung. Amtsblick sieht nur die
einzelnen Tool-Aufrufe, nicht die Unterhaltung.

## Diese Website

Die Projektseite liegt bei GitHub Pages (GitHub, Inc.). GitHub verarbeitet beim Aufruf technische
Daten wie die IP-Adresse; siehe die
[Datenschutzerklärung von GitHub](https://docs.github.com/de/site-policy/privacy-policies/github-general-privacy-statement).
Die Seite setzt keine Cookies und bindet keine fremden Inhalte ein.

## Ihre Rechte

Sie haben nach der DSGVO das Recht auf Auskunft, Berichtigung, Löschung, Einschränkung der
Verarbeitung und Widerspruch. Da Amtsblick keine Daten zu Personen speichert, kann eine Auskunft in
der Regel nur bestätigen, dass nichts vorliegt. Beschwerden können Sie bei der österreichischen
Datenschutzbehörde einbringen ([dsb.gv.at](https://www.dsb.gv.at)).

## Änderungen

Diese Erklärung wird angepasst, wenn sich der Dienst ändert. Der Verlauf ist im
[Repository](https://github.com/haraldrohan/Amtsblick/commits/main/docs/datenschutz.md) nachvollziehbar.
