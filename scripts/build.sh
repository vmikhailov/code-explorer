#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

GREEN='\033[0;32m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}=== 1. Compiling solution (Release, /warnaserror) ===${NC}"
dotnet build "$SOLUTION_DIR/CodeExplorer.slnx" -c Release /warnaserror

echo -e "${CYAN}=== 2. Running test suite (/warnaserror) ===${NC}"
dotnet test "$SOLUTION_DIR/CodeExplorer.slnx" -c Release --no-build /warnaserror

echo -e "${GREEN}✓ Build and all tests completed successfully!${NC}"
