using Amtsblick.Core;
using Amtsblick.Core.Http;
using Amtsblick.Core.Kontingent;
using Amtsblick.Core.Ort;
using Amtsblick.Core.Quellen;
using Amtsblick.Modules.Wetter.GeoSphere;

namespace Amtsblick.Server;

/// <summary>
/// Endpunkt /health: Stand je Quelle und Restkontingent bei GeoSphere. Liest nur, was der Server
/// ohnehin weiß, und löst keinen Abruf bei einer Quelle aus.
/// </summary>
public static class Gesundheit
{
    public static IResult Antwort(
        GemeindeVerzeichnis gemeinden, IEnumerable<IQuellenAnbieter> anbieter, KontingentRegister kontingent)
    {
        var geosphere = kontingent.Fuer(GeoSphereClient.Quelle);
        return Results.Json(
            new
            {
                Status = gemeinden.IstLeer ? "eingeschränkt" : "bereit",
                Version = UserAgent.Version,
                Gemeinden = gemeinden.Alle.Count,
                Hinweis = gemeinden.Ladehinweis,
                Quellen = anbieter.SelectMany(a => a.Quellen()).Select(q => new
                {
                    q.Quelle,
                    q.Datensatz,
                    q.Stand,
                    q.LetzterAbruf,
                }),
                GeosphereKontingent = geosphere is null
                    ? null
                    : new { geosphere.RestStunde, geosphere.LimitStunde, Stand = Zeit.Iso(geosphere.Stand) },
            },
            ToolAntwort.Json);
    }
}
