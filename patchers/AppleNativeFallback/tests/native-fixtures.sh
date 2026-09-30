#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
DOTNET="${VALHEIM_DOTNET:-dotnet}"
if [ "$(uname -s)" != Darwin ] || [ "$(uname -m)" != arm64 ]; then
    echo "Native fixture checks require ARM64 macOS; portable checks run separately."
    exit 0
fi
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$STAGE/available" "$STAGE/wrong-arch" "$STAGE/missing-export"
cat > "$STAGE/probe.c" <<'EOF'
struct RuntimeEnvironment { int os, major, minor, patch; };
struct RuntimeEnvironment AppleCore_GetRuntimeEnvironment(void) { return (struct RuntimeEnvironment){2, 14, 0, 0}; }
EOF
cat > "$STAGE/missing.c" <<'EOF'
int unrelated_export(void) { return 0; }
EOF
SDK="$(xcrun --sdk macosx --show-sdk-path)"
clang -isysroot "$SDK" -dynamiclib -arch arm64 "$STAGE/probe.c" -o "$STAGE/available/libAppleCoreNativeMac.dylib"
clang -isysroot "$SDK" -dynamiclib -arch x86_64 "$STAGE/probe.c" -o "$STAGE/wrong-arch/libAppleCoreNativeMac.dylib"
clang -isysroot "$SDK" -dynamiclib -arch arm64 "$STAGE/missing.c" -o "$STAGE/missing-export/libAppleCoreNativeMac.dylib"
APPLE_FALLBACK_NATIVE_FIXTURES="$STAGE" "$DOTNET" run --project "$HERE/Tests.csproj" -c Release
