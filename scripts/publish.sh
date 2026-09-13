#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$SOLUTION_DIR/src/UI/CodeExplorer/CodeExplorer.csproj"
BASE_OUTPUT_DIR="$SOLUTION_DIR/.Build/bin"
CONFIGURATION="Release"

TARGET="${1:-current}"

publish_target() {
    local rid="$1"
    local out_dir="$2"
    echo ""
    echo "==> Publishing single-file self-contained binary for: $rid"
    mkdir -p "$out_dir"
    dotnet publish "$PROJECT" \
        -c "$CONFIGURATION" \
        -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:IncludeAllContentForSelfExtract=true \
        -p:EnableCompressionInSingleFile=true \
        -p:PublishTrimmed=false \
        -p:DebugType=none \
        -o "$out_dir"
    echo "✓ Published: $rid -> $out_dir"
}

if [[ "$TARGET" == "all" ]]; then
    publish_target "win-x64" "$BASE_OUTPUT_DIR/win-x64"
    publish_target "linux-x64" "$BASE_OUTPUT_DIR/linux-x64"
    publish_target "linux-arm64" "$BASE_OUTPUT_DIR/linux-arm64"
    publish_target "osx-arm64" "$BASE_OUTPUT_DIR/osx-arm64"
    publish_target "osx-x64" "$BASE_OUTPUT_DIR/osx-x64"
elif [[ "$TARGET" == "current" ]]; then
    # Auto-detect current platform
    OS="$(uname -s)"
    ARCH="$(uname -m)"
    if [[ "$OS" == "Darwin" ]]; then
        if [[ "$ARCH" == "arm64" ]]; then RID="osx-arm64"; else RID="osx-x64"; fi
    elif [[ "$OS" == "Linux" ]]; then
        if [[ "$ARCH" == "aarch64" ]]; then RID="linux-arm64"; else RID="linux-x64"; fi
    else
        RID="win-x64"
    fi
    publish_target "$RID" "$BASE_OUTPUT_DIR"
else
    publish_target "$TARGET" "$BASE_OUTPUT_DIR/$TARGET"
fi

echo ""
echo "All done!"
