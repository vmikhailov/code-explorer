#!/usr/bin/env bash
set -euo pipefail

TARGET_HOST="${TARGET_HOST:-calendars.cc}"
TARGET_USER="${TARGET_USER:-root}"
TARGET_DIR="${TARGET_DIR:-/var/www/code-explorer}"
SSH_KEY="${SSH_KEY:-$HOME/.ssh/hermes_mikhailov}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "🚀 Deploying CodeExplorer site to ${TARGET_USER}@${TARGET_HOST}:${TARGET_DIR}..."

tar -czf - \
  index.html \
  favicon.svg \
  logo.svg \
  all_services.png \
  selected_services.png \
  all_services_clean.png \
  selected_services_clean.png \
  project_flow.png \
  CNAME | \
  ssh -i "$SSH_KEY" -o BatchMode=yes -o StrictHostKeyChecking=accept-new "${TARGET_USER}@${TARGET_HOST}" \
    "tar -xzf - -C '${TARGET_DIR}' && chown -R root:root '${TARGET_DIR}' && chmod 755 '${TARGET_DIR}' && chmod 644 '${TARGET_DIR}'/*"

echo "✅ Live deployment successful: https://code-explorer.mikhailov.cc"
