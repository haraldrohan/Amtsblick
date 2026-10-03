#!/usr/bin/env sh
# Lädt Gemeindegrenzen und Bezirksnamen der Statistik Austria (CC BY 4.0) und legt sie in
# data/gemeinden.sqlite ab. Einmal nach dem Klonen ausführen und danach bei neuem Gebietsstand.
set -eu
wurzel="$(cd "$(dirname "$0")/.." && pwd)"
exec dotnet run --project "$wurzel/src/Amtsblick.Server" -c Release -- import
