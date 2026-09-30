#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
ROOT="$(cd "$HERE/../../.." && pwd -P)"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
GAME="$STAGE/game with spaces"
mkdir -p "$GAME/valheim.app" "$GAME/BepInEx/core" "$STAGE/tools"
touch "$GAME/BepInEx/core/Mono.Cecil.dll"
# Stub process status for fixtures; the real installer always queries pgrep.
printf '#!/bin/sh\nexit 1\n' > "$STAGE/tools/pgrep"
chmod +x "$STAGE/tools/pgrep"
export PATH="$STAGE/tools:$PATH"
TARGET="$GAME/BepInEx/patchers/AppleNativeFallback.dll"
RECEIPT="$GAME/BepInEx/patchers/AppleNativeFallback.installed.sha256"
"$ROOT/apple-fallback.sh" enable "$GAME"
cmp "$TARGET" "$ROOT/bin/BepInEx/patchers/AppleNativeFallback.dll"
"$ROOT/apple-fallback.sh" enable "$GAME"
"$ROOT/apple-fallback.sh" disable "$GAME"
test ! -e "$TARGET"
"$ROOT/apple-fallback.sh" disable "$GAME"
printf 'unmanaged' > "$TARGET"
if "$ROOT/apple-fallback.sh" enable "$GAME"; then echo "Overwrote unknown patch" >&2; exit 1; fi
if "$ROOT/apple-fallback.sh" disable "$GAME"; then echo "Removed unknown patch" >&2; exit 1; fi
test "$(cat "$TARGET")" = unmanaged
# Recognized receipt allows upgrading/removing a previous managed release.
shasum -a 256 "$TARGET" | awk '{print $1}' > "$RECEIPT"
"$ROOT/apple-fallback.sh" enable "$GAME"
cmp "$TARGET" "$ROOT/bin/BepInEx/patchers/AppleNativeFallback.dll"
printf '#!/bin/sh\nexit 0\n' > "$STAGE/tools/pgrep"
if "$ROOT/apple-fallback.sh" disable "$GAME"; then echo "Allowed change while running" >&2; exit 1; fi
printf '#!/bin/sh\nexit 1\n' > "$STAGE/tools/pgrep"
"$ROOT/apple-fallback.sh" disable "$GAME"
test ! -e "$TARGET"
echo "Installer checks passed: spaces, idempotence, ownership, upgrades, running-process guard, removal."
