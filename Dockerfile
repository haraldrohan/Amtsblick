# Amtsblick als gehosteter MCP-Server (Streamable HTTP auf Port 8080, Endpunkt /mcp).
#
#   docker build -t amtsblick .
#   docker run --rm -p 8080:8080 -e Amtsblick__Kontakt=betrieb@example.org amtsblick
#   curl http://localhost:8080/health
#
# HTTPS und die eigene Domain übernimmt der Hosting-Anbieter bzw. ein vorgeschalteter Reverse Proxy.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Amtsblick.Server -c Release -o /app -p:PackAsTool=false --nologo

# Die Gemeindegrenzen der Statistik Austria (CC BY 4.0) werden beim Bauen geladen, damit der
# Container sofort bereit ist und beim Start nichts nachladen muss.
RUN Amtsblick__DatenVerzeichnis=/app/data dotnet /app/Amtsblick.Server.dll import

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# 1654 ist der unprivilegierte Benutzer "app" der .NET-Images. Ihm gehört /app/data, weil der
# Server dort den letzten Pegelstand ablegt.
COPY --from=build --chown=1654:1654 /app .

ENV Urls=http://+:8080 \
    Amtsblick__DatenVerzeichnis=/app/data \
    Amtsblick__AutoImport=false \
    Amtsblick__Module__Wasser__Vorladen=true \
    Amtsblick__Http__HinterProxy=true \
    AllowedHosts=*

# Beim Betrieb zu setzen:
#   Amtsblick__Kontakt   Kontaktadresse für den User-Agent (ohne sie keine Pegeldaten)
#   AllowedHosts         der öffentliche Hostname statt "*", wenn der Proxy den Host-Header durchreicht

USER 1654
EXPOSE 8080
ENTRYPOINT ["dotnet", "Amtsblick.Server.dll", "--http"]
