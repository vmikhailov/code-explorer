#!/bin/bash
set -e

# Colors for log statements
GREEN='\033[0;32m'
NC='\033[0;68m' # No Color

echo -e "${GREEN}=== 1. Compiling solution ===${NC}"
dotnet build -c Release

echo -e "${GREEN}=== 2. Running tests ===${NC}"
dotnet test -c Release --no-build

echo -e "${GREEN}=== 3. Building Docker image 'codeexplorer:latest' ===${NC}"
docker build -t codeexplorer:latest .

echo -e "${GREEN}=== Build and Docker image packaging completed successfully! ===${NC}"
