#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
ROOT="$(cd "$HERE/../.." && pwd -P)"
DOTNET="${VALHEIM_DOTNET:-dotnet}"
cd "$ROOT"
"$DOTNET" restore "$HERE/AppleNativeFallback.csproj" --locked-mode
"$DOTNET" build "$HERE/AppleNativeFallback.csproj" -c Release --no-restore
TARGET="$ROOT/bin/BepInEx/patchers/AppleNativeFallback.dll"
mkdir -p "$(dirname "$TARGET")"
cp "$HERE/bin/Release/net46/AppleNativeFallback.dll" "$TARGET"
(cd "$(dirname "$TARGET")" && shasum -a 256 AppleNativeFallback.dll > AppleNativeFallback.sha256)
