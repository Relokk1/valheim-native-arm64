#!/usr/bin/env bash
# Valheim Native ARM64 — установка модов без Rosetta на Apple Silicon
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
DEFAULT_GAME="$HOME/Library/Application Support/Steam/steamapps/common/Valheim"
GAME="${1:-$DEFAULT_GAME}"
PACK_URL="https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/5.4.2333/"

info() { printf '\033[1;36m==>\033[0m %s\n' "$1"; }
err()  { printf '\033[1;31mОшибка:\033[0m %s\n' "$1" >&2; }

if [ "$(uname -m)" != "arm64" ]; then
    err "Этот установщик только для Mac на Apple Silicon (M1/M2/M3/M4). Обнаружено: $(uname -m)"
    exit 1
fi

if [ ! -d "$GAME/valheim.app" ]; then
    err "Valheim не найден в: $GAME"
    echo "Укажите путь вручную:  ./install.sh \"/путь/к/папке/Valheim\"" >&2
    exit 1
fi

info "Valheim найден: $GAME"

# Проверяем, что у игры есть нативный arm64-слайс
if ! lipo -info "$GAME/valheim.app/Contents/MacOS/Valheim" 2>/dev/null | grep -q arm64; then
    err "В этой сборке Valheim нет нативного arm64. Обновите игру в Steam."
    exit 1
fi

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

info "Скачиваю BepInExPack (конфиги и патчеры для Valheim)..."
curl -fsSL -o "$STAGE/pack.zip" "$PACK_URL"
unzip -q -o "$STAGE/pack.zip" -d "$STAGE/pack"

# Бэкап существующей установки
if [ -d "$GAME/BepInEx" ]; then
    BACKUP="$GAME/BepInEx.backup-$(date +%Y%m%d-%H%M%S)"
    info "Найдена старая установка — сохраняю копию в $(basename "$BACKUP")"
    mv "$GAME/BepInEx" "$BACKUP"
fi

info "Устанавливаю BepInEx..."
cp -R "$STAGE/pack/BepInExPack_Valheim/BepInEx" "$GAME/"

# Заменяем core на пересобранный под ARM64 (MonoMod v25 + HarmonyX 2.16)
info "Ставлю ARM64-совместимое ядро (MonoMod v25 + HarmonyX 2.16.1)..."
rm -rf "$GAME/BepInEx/core"
mkdir -p "$GAME/BepInEx/core"
cp "$REPO/bin/BepInEx/core/"*.dll "$GAME/BepInEx/core/"

info "Ставлю UnityDoorstop 4.5.0 (universal)..."
cp "$REPO/bin/libdoorstop.dylib" "$GAME/libdoorstop.dylib"

# macOS требует Type = GameObject для Unity-игр
if [ -f "$GAME/BepInEx/config/BepInEx.cfg" ]; then
    sed -i '' 's/^Type = Application$/Type = GameObject/' "$GAME/BepInEx/config/BepInEx.cfg"
fi

info "Снимаю карантин Gatekeeper..."
xattr -dr com.apple.quarantine "$GAME/BepInEx" "$GAME/libdoorstop.dylib" 2>/dev/null || true

info "Создаю скрипт запуска..."
cp "$REPO/play.sh" "$GAME/play.sh"
chmod +x "$GAME/play.sh"

mkdir -p "$GAME/BepInEx/plugins"

cat <<EOF

\033[1;32mГотово!\033[0m

Запуск игры с модами:
  "$GAME/play.sh"

Установка модов — распакуйте .dll в:
  $GAME/BepInEx/plugins/

Лог для диагностики:
  $GAME/BepInEx/LogOutput.log
EOF
