#!/usr/bin/env bash
set -euo pipefail

REPO="vmikhailov/code-explorer"
INSTALL_DIR="${CE_INSTALL_DIR:-$HOME/.local/bin}"

GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${CYAN}=== CodeExplorer ('ce') Installer ===${NC}"

# Detect OS and architecture
OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
  Darwin)
    if [ "$ARCH" = "arm64" ]; then
      TARGET="osx-arm64"
    else
      TARGET="osx-x64"
    fi
    ;;
  Linux)
    if [ "$ARCH" = "aarch64" ] || [ "$ARCH" = "arm64" ]; then
      TARGET="linux-arm64"
    else
      TARGET="linux-x64"
    fi
    ;;
  *)
    echo -e "${RED}Error: Unsupported operating system '$OS'.${NC}"
    echo "For Windows, please run install.ps1 in PowerShell."
    exit 1
    ;;
esac

echo -e "Platform detected: ${YELLOW}$TARGET${NC}"

# Fetch latest release download URL
echo "Fetching latest release from GitHub ($REPO)..."
RELEASE_JSON="$(curl -sSL "https://api.github.com/repos/$REPO/releases/latest")"

DOWNLOAD_URL="$(echo "$RELEASE_JSON" | grep -o "https://[^\"]*ce-$TARGET\.tar\.gz" | head -n 1 || true)"

if [ -z "$DOWNLOAD_URL" ]; then
  echo -e "${RED}Error: Could not find release asset for target '$TARGET'.${NC}"
  echo "Please check available releases at: https://github.com/$REPO/releases"
  exit 1
fi

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

echo "Downloading $DOWNLOAD_URL..."
curl -sSL "$DOWNLOAD_URL" -o "$TMP_DIR/ce.tar.gz"

echo "Extracting binary..."
tar -xzf "$TMP_DIR/ce.tar.gz" -C "$TMP_DIR"

mkdir -p "$INSTALL_DIR"
mv "$TMP_DIR/ce" "$INSTALL_DIR/ce"
chmod +x "$INSTALL_DIR/ce"

echo -e "${GREEN}✓ Successfully installed CodeExplorer to $INSTALL_DIR/ce${NC}"

# Verify PATH
case ":$PATH:" in
  *":$INSTALL_DIR:"*) ;;
  *)
    echo ""
    echo -e "${YELLOW}Warning: $INSTALL_DIR is not in your PATH.${NC}"
    echo "Add the following line to your ~/.zshrc or ~/.bashrc:"
    echo -e "  ${CYAN}export PATH=\"\$PATH:$INSTALL_DIR\"${NC}"
    ;;
esac

echo ""
echo "To verify installation, run:"
echo "  ce --version"
