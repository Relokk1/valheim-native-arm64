#!/usr/bin/env bash
# Запуск Valheim с модами нативно на Apple Silicon (без Rosetta)
set -euo pipefail

GAME="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
EXEC="$GAME/valheim.app/Contents/MacOS/Valheim"
DOORSTOP="$GAME/libdoorstop.dylib"
PRELOADER="$GAME/BepInEx/core/BepInEx.Preloader.dll"

for f in "$EXEC" "$DOORSTOP" "$PRELOADER"; do
    [ -f "$f" ] || { echo "Не найдено: $f" >&2; exit 1; }
done

# Steam должен быть запущен — игра инициализирует steam_api
if ! pgrep -x steam_osx >/dev/null 2>&1; then
    echo "Запускаю Steam..."
    open -a Steam
    sleep 12
fi

cd "$GAME"

export DOORSTOP_ENABLED=1
export DOORSTOP_TARGET_ASSEMBLY="$PRELOADER"
export ARCHPREFERENCE="arm64,x86_64"

# arch -arm64 форсирует нативный слайс universal binary.
# arch вырезает переменные DYLD_*, поэтому пробрасываем их через -e.
exec arch -arm64 \
    -e DYLD_LIBRARY_PATH="$GAME" \
    -e DYLD_INSERT_LIBRARIES="$DOORSTOP" \
    "$EXEC" "$@"
