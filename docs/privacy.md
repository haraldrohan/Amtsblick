---
title: Privacy policy
lang: en
---

# Privacy policy

> **Draft.** Last updated: 3 October 2026. Items marked ⚠ are still open and will be completed before
> the hosted server goes live. The [German version](datenschutz.html) is authoritative.

Amtsblick is a private open-source project and not an official service of any public authority.

## Controller

⚠ Name and address of the operator: see the [legal notice](impressum.html).
⚠ Contact for privacy matters: [contact address to be added]

## In short

Amtsblick reads public data and passes it on. There are no accounts, no cookies, no tracking and no
telemetry. Requests and the places you ask about are not stored.

## The hosted server

If you use Amtsblick as a connector via the hosted server's address, the following is processed:

| Data | Purpose | Retention |
|---|---|---|
| IP address of the requesting party | sending the response; limiting requests per address (60 per minute) | in memory only, for the one-minute counting window; not written to logs |
| Request content (tool and parameters, such as the place name) | answering the request | in memory only while the request is handled; not written to logs |
| Method, path, status code, duration | technical operation and troubleshooting | server log; ⚠ retention: [as set by the hosting provider, 14 days at most] |

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

⚠ Hosting provider: [to be added once chosen, with registered office, data-centre region and a
reference to the data processing agreement]. A data centre in the EU is planned. Whether the
provider keeps its own access logs with IP addresses at its edge, and for how long, will be added here.

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

## This website

The project site is hosted on GitHub Pages (GitHub, Inc.). GitHub processes technical data such as
your IP address when you visit; see
[GitHub's privacy statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).
The site sets no cookies and embeds no third-party content.

## Your rights

Under the GDPR you have the right of access, rectification, erasure, restriction of processing and
objection. Because Amtsblick stores no data about individuals, a response to an access request will
normally confirm that nothing is held. You may lodge a complaint with the Austrian Data Protection
Authority ([dsb.gv.at](https://www.dsb.gv.at)).

## Changes

This policy is updated when the service changes. The history is available in the
[repository](https://github.com/haraldrohan/Amtsblick/commits/main/docs/privacy.md).
