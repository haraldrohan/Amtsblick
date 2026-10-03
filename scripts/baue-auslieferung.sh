#!/usr/bin/env bash
# Baut für jede Plattform eine eigenständige Programmdatei (ohne installiertes .NET lauffähig),
# ein Archiv davon und ein MCP Bundle (.mcpb) für Claude Desktop.
#
#   scripts/baue-auslieferung.sh [RID ...]     Standard: win-x64 osx-arm64 osx-x64 linux-x64
#
# Ergebnis in artifacts/release/. Die Bundles für macOS und Linux müssen unter Linux oder macOS
# gebaut werden, weil Windows das Ausführungsrecht der Programmdatei nicht ins Archiv schreibt;
# die Release-Automatik baut sie deshalb unter Linux.
set -euo pipefail

wurzel="$(cd "$(dirname "$0")/.." && pwd)"
cd "$wurzel"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props)"
rids=("$@")
[ ${#rids[@]} -eq 0 ] && rids=(win-x64 osx-arm64 osx-x64 linux-x64)
ziel="artifacts/release"
mkdir -p "$ziel"

for rid in "${rids[@]}"; do
  case "$rid" in
    win-*)   plattform=win32;  datei=Amtsblick.Server.exe ;;
    osx-*)   plattform=darwin; datei=Amtsblick.Server ;;
    linux-*) plattform=linux;  datei=Amtsblick.Server ;;
    *) echo "Unbekannte Plattform: $rid" >&2; exit 1 ;;
  esac

  echo "== $rid: Programmdatei"
  ausgabe="artifacts/publish/$rid"
  rm -rf "$ausgabe"
  dotnet publish src/Amtsblick.Server -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PackAsTool=false -p:DebugType=none -o "$ausgabe" --nologo -v q

  echo "== $rid: MCP Bundle"
  stufe="artifacts/mcpb/$rid"
  rm -rf "$stufe"
  mkdir -p "$stufe/server"
  cp "$ausgabe/$datei" "$ausgabe/appsettings.json" "$stufe/server/"
  cp assets/icon.png LICENSE NOTICE "$stufe/"
  sed -e "s/@VERSION@/$version/" -e "s/@PLATFORM@/$plattform/" -e "s/@BINARY@/$datei/g" \
    packaging/mcpb/manifest.json > "$stufe/manifest.json"
  chmod +x "$stufe/server/$datei" 2>/dev/null || true

  bundle="$ziel/amtsblick-$version-$rid.mcpb"
  rm -f "$bundle"
  if command -v npx >/dev/null 2>&1; then
    npx --yes @anthropic-ai/mcpb validate "$stufe/manifest.json"
    npx --yes @anthropic-ai/mcpb pack "$stufe" "$bundle"
  elif command -v zip >/dev/null 2>&1; then
    (cd "$stufe" && zip -q -r "$wurzel/$bundle" .)
  else
    # Nur zur Ansicht unter Windows ohne Node und zip; nicht zur Weitergabe.
    powershell -NoProfile -Command "Compress-Archive -Path '$stufe/*' -DestinationPath '$bundle.zip' -Force" \
      && mv "$bundle.zip" "$bundle"
  fi

  echo "== $rid: Archiv"
  if [ "$plattform" = win32 ] && ! command -v zip >/dev/null 2>&1; then
    powershell -NoProfile -Command "Compress-Archive -Path '$ausgabe/$datei','$ausgabe/appsettings.json','LICENSE','NOTICE' -DestinationPath '$ziel/amtsblick-$version-$rid.zip' -Force"
  elif [ "$plattform" = win32 ]; then
    zip -q -j "$ziel/amtsblick-$version-$rid.zip" "$ausgabe/$datei" "$ausgabe/appsettings.json" LICENSE NOTICE
  else
    chmod +x "$ausgabe/$datei" 2>/dev/null || true
    tar -czf "$ziel/amtsblick-$version-$rid.tar.gz" -C "$ausgabe" "$datei" appsettings.json -C "$wurzel" LICENSE NOTICE
  fi
done

ls -l "$ziel"
