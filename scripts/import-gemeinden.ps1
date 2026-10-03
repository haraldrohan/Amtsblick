# Lädt Gemeindegrenzen und Bezirksnamen der Statistik Austria (CC BY 4.0) und legt sie in
# data/gemeinden.sqlite ab. Einmal nach dem Klonen ausführen und danach bei neuem Gebietsstand.
$ErrorActionPreference = 'Stop'
$wurzel = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $wurzel 'src/Amtsblick.Server') -c Release -- import
exit $LASTEXITCODE
