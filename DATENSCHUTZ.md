# Datenschutzerklärung

[English version below](#privacy-policy)

Stand: 3. Oktober 2026. Der mit ⚠ markierte Abschnitt wird mit der Inbetriebnahme des gehosteten
Servers bestätigt.

Amtsblick ist ein privates Open-Source-Projekt und kein offizielles Angebot einer Behörde.

## Verantwortlicher

- Betreiber: Harald Rohan, Österreich
- Kontakt in Datenschutzfragen: [haraldrohan@gmail.com](mailto:haraldrohan@gmail.com)

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
| Methode, Pfad, Statuscode, Dauer | technischer Betrieb und Fehlersuche | nur als laufende Konsolenausgabe des Servers; sie wird nicht aufbewahrt, weil kein Protokollspeicher eingerichtet ist |

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

Der Server läuft bei Microsoft Azure (Azure Container Apps) in der Region Österreich („Austria East").
Vertragspartner ist Microsoft Ireland Operations Limited; die Auftragsverarbeitung regelt der
[Datenschutznachtrag von Microsoft](https://www.microsoft.com/licensing/docs/view/Microsoft-Products-and-Services-Data-Protection-Addendum-DPA).
Microsoft verarbeitet am Eingang der Plattform technische Verbindungsdaten wie die IP-Adresse, um die
Anfrage zuzustellen; Näheres in der [Datenschutzerklärung von Microsoft](https://privacy.microsoft.com/de-de/privacystatement).
⚠ Dieser Abschnitt wird mit der Inbetriebnahme bestätigt.

### Startseite des Servers

Die Startseite und diese Erklärung unter `/datenschutz` liefert derselbe Server aus. Sie setzen keine
Cookies und binden keine fremden Inhalte ein.

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

## Ihre Rechte

Sie haben nach der DSGVO das Recht auf Auskunft, Berichtigung, Löschung, Einschränkung der
Verarbeitung und Widerspruch. Da Amtsblick keine Daten zu Personen speichert, kann eine Auskunft in
der Regel nur bestätigen, dass nichts vorliegt. Beschwerden können Sie bei der österreichischen
Datenschutzbehörde einbringen ([dsb.gv.at](https://www.dsb.gv.at)).

## Änderungen

Diese Erklärung wird angepasst, wenn sich der Dienst ändert. Der Verlauf ist im
[Repository](https://github.com/haraldrohan/Amtsblick/commits/main/DATENSCHUTZ.md) nachvollziehbar.

---

# Privacy policy

Last updated: 3 October 2026. The section marked ⚠ will be confirmed when the hosted server goes live.
The German version above is authoritative.

Amtsblick is a private open-source project and not an official service of any public authority.

## Controller

- Operator: Harald Rohan, Austria
- Contact for privacy matters: [haraldrohan@gmail.com](mailto:haraldrohan@gmail.com)

## In short

Amtsblick reads public data and passes it on. There are no accounts, no cookies, no tracking and no
telemetry. Requests and the places you ask about are not stored.

## The hosted server

If you use Amtsblick as a connector via the hosted server's address, the following is processed:

| Data | Purpose | Retention |
|---|---|---|
| IP address of the requesting party | sending the response; limiting requests per address (60 per minute) | in memory only, for the one-minute counting window; not written to logs |
| Request content (tool and parameters, such as the place name) | answering the request | in memory only while the request is handled; not written to logs |
| Method, path, status code, duration | technical operation and troubleshooting | live console output of the server only; it is not retained because no log store is configured |

With connectors in claude.ai, requests normally come from Anthropic's servers rather than from your
device. In that case Amtsblick does not learn your IP address.

The legal basis is the legitimate interest in operating the service securely (Art. 6(1)(f) GDPR).

### What the server sends to third parties

- **GeoSphere Austria:** the coordinate of the requested place, rounded to 0.01° (about 1 km). By
  default this is the centre of the municipality. The request comes from the server, not from you;
  GeoSphere does not learn who asked.
- **eHYD (gis.lfrz.gv.at):** at most once per hour, a download of the complete set of current water
  levels, unrelated to any individual request.
- **Statistik Austria:** only when the server is built, to load the municipal boundaries.

Each of these requests carries a User-Agent with the project address and the operator's contact
address, but nothing about users.

### Hosting

The server runs on Microsoft Azure (Azure Container Apps) in the Austria East region. The contracting
party is Microsoft Ireland Operations Limited; processing on our behalf is governed by the
[Microsoft Data Protection Addendum](https://www.microsoft.com/licensing/docs/view/Microsoft-Products-and-Services-Data-Protection-Addendum-DPA).
Microsoft processes technical connection data such as the IP address at the platform edge in order to
deliver the request; see the [Microsoft privacy statement](https://privacy.microsoft.com/en-us/privacystatement).
⚠ This section will be confirmed when the server goes live.

### The server's start page

The start page and this policy at `/datenschutz` are served by the same server. They set no cookies
and embed no third-party content.

## Local use

If you run Amtsblick on your own computer (MCP bundle, NuGet package or from source), you do not use
the hosted server. In that case:

- The program runs on your machine. The operator of Amtsblick receives no data.
- Requests to GeoSphere Austria, eHYD and – on first start – Statistik Austria originate from your
  computer. These parties see your IP address and the User-Agent with the contact address you
  entered yourself.
- Your computer stores the municipal data and the latest water levels, nothing else.

## The AI assistant

You hold your conversation with an AI assistant such as Claude. What it processes and stores is
governed by its provider's own privacy policy. Amtsblick only sees the individual tool calls, not
the conversation.

## Your rights

Under the GDPR you have the right of access, rectification, erasure, restriction of processing and
objection. Because Amtsblick stores no data about individuals, a response to an access request will
normally confirm that nothing is held. You may lodge a complaint with the Austrian Data Protection
Authority ([dsb.gv.at](https://www.dsb.gv.at)).

## Changes

This policy is updated when the service changes. The history is available in the
[repository](https://github.com/haraldrohan/Amtsblick/commits/main/DATENSCHUTZ.md).
