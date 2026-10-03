using System.Net;
using System.Reflection;
using Amtsblick.Core;
using Amtsblick.Core.Http;
using Markdig;

namespace Amtsblick.Server;

/// <summary>
/// Die beiden Webseiten des gehosteten Servers: eine kurze Startseite und die Datenschutzerklärung.
/// Die Erklärung ist die Datei DATENSCHUTZ.md aus dem Repository, beim Bauen eingebettet, damit die
/// ausgelieferte Fassung immer der im Repository entspricht. Keine Cookies, keine fremden Inhalte.
/// </summary>
public static class Seiten
{
    private const string Repo = "https://github.com/haraldrohan/Amtsblick";

    private static readonly Lazy<string> DatenschutzHtml = new(() =>
    {
        using var strom = Assembly.GetExecutingAssembly().GetManifestResourceStream("DATENSCHUTZ.md")
            ?? throw new InvalidOperationException("DATENSCHUTZ.md ist nicht eingebettet.");
        using var leser = new StreamReader(strom);
        var pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseAutoIdentifiers().Build();
        return Rahmen("Datenschutzerklärung – Amtsblick", Markdown.ToHtml(leser.ReadToEnd(), pipeline)
            + "<p><a href=\"/\">Zur Startseite</a></p>");
    });

    public static IResult Start() => Results.Content(
        Rahmen("Amtsblick", $"""
            <h1>Amtsblick</h1>
            <p>Amtliche österreichische Daten nach Ort, als MCP-Server für KI-Assistenten: Wetterprognose und
            Nowcast der GeoSphere Austria und aktuelle Pegelstände des Hydrographischen Dienstes (eHYD), je Gemeinde.</p>
            <p><strong>{WebUtility.HtmlEncode(ToolAntwort.Pflichthinweis)}</strong></p>
            <h2>Einbinden</h2>
            <p>Als Connector per URL: die Adresse dieser Seite mit dem Pfad <code>/mcp</code> eintragen.
            Eine Anmeldung ist nicht nötig. Weitere Wege (Claude Desktop lokal, Claude Code, VS Code) stehen in der
            <a href="{Repo}#einbinden">Anleitung im Repository</a>.</p>
            <h2>Mehr</h2>
            <ul>
              <li><a href="{Repo}">Quelltext, Quellen und Lizenzen auf GitHub</a></li>
              <li><a href="/datenschutz">Datenschutzerklärung / Privacy policy</a></li>
              <li><a href="/health">Zustand des Servers</a></li>
            </ul>
            <p>Keine amtliche Unwetter- oder Hochwasserwarnung. Maßgeblich sind die Warndienste von GeoSphere Austria
            und der Länder. Alle Daten stehen unter CC BY 4.0 der jeweiligen Quelle.</p>
            <p><small>Amtsblick {WebUtility.HtmlEncode(UserAgent.Version)}</small></p>
            """),
        "text/html; charset=utf-8");

    public static IResult Datenschutz() => Results.Content(DatenschutzHtml.Value, "text/html; charset=utf-8");

    private static string Rahmen(string titel, string inhalt) => $$"""
        <!doctype html>
        <html lang="de">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{{WebUtility.HtmlEncode(titel)}}</title>
        <style>
          body { font: 16px/1.55 system-ui, sans-serif; max-width: 46rem; margin: 2rem auto; padding: 0 1rem; color: #1b1b1b; }
          h1, h2, h3 { line-height: 1.25; }
          a { color: #144e63; }
          table { border-collapse: collapse; margin: 1rem 0; }
          th, td { border: 1px solid #c8c8c8; padding: .4rem .6rem; text-align: left; vertical-align: top; }
          code { background: #f1f1f1; padding: .1rem .3rem; border-radius: 3px; }
          hr { margin: 3rem 0; border: 0; border-top: 1px solid #c8c8c8; }
        </style>
        </head>
        <body>
        {{inhalt}}
        </body>
        </html>
        """;
}
