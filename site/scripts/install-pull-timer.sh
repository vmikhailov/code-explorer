#!/usr/bin/env bash
set -euo pipefail

TARGET_HOST="${TARGET_HOST:-146.190.24.85}"
TARGET_USER="${TARGET_USER:-root}"
SSH_KEY="${SSH_KEY:-$HOME/.ssh/hermes_mikhailov}"

ssh -i "$SSH_KEY" -o BatchMode=yes "${TARGET_USER}@${TARGET_HOST}" 'bash -s' << 'REMOTE_SCRIPT'
set -euo pipefail

cat > /usr/local/bin/pull-code-explorer-site.sh << 'EOF'
#!/usr/bin/env bash
set -euo pipefail

REPO_URL="https://github.com/vmikhailov/code-explorer.git"
RAW_BASE="https://raw.githubusercontent.com/vmikhailov/code-explorer"
TARGET_DIR="/var/www/code-explorer"
STATE_DIR="/var/lib/code-explorer-site"
STATE_FILE="${STATE_DIR}/last_sha"

mkdir -p "${STATE_DIR}" "${TARGET_DIR}"

REMOTE_SHA=$(git ls-remote "${REPO_URL}" refs/heads/site-dist | awk '{print $1}')
if [[ -z "${REMOTE_SHA}" ]]; then
  exit 0
fi

LAST_SHA=""
if [[ -f "${STATE_FILE}" ]]; then
  LAST_SHA=$(cat "${STATE_FILE}")
fi

if [[ "${REMOTE_SHA}" == "${LAST_SHA}" ]]; then
  exit 0
fi

echo "[code-explorer-site-pull] New site-dist commit detected: ${REMOTE_SHA} (previous: ${LAST_SHA:-none})"

TMP_TAR=$(mktemp /tmp/site-dist.XXXXXX.tar.gz)
trap 'rm -f "${TMP_TAR}"' EXIT

curl -fsSL "${RAW_BASE}/${REMOTE_SHA}/site-dist.tar.gz" -o "${TMP_TAR}"
tar -tzf "${TMP_TAR}" >/dev/null

rm -rf "${TARGET_DIR}/assets"
tar -xzf "${TMP_TAR}" -C "${TARGET_DIR}"
chown -R root:root "${TARGET_DIR}"
chmod -R u=rwX,go=rX "${TARGET_DIR}"

echo "${REMOTE_SHA}" > "${STATE_FILE}"
echo "[code-explorer-site-pull] Successfully deployed ${REMOTE_SHA} to ${TARGET_DIR}"
EOF

chmod +x /usr/local/bin/pull-code-explorer-site.sh

cat > /etc/systemd/system/code-explorer-site-pull.service << 'EOF'
[Unit]
Description=Pull latest CodeExplorer website bundle from GitHub (site-dist)
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
ExecStart=/usr/local/bin/pull-code-explorer-site.sh
EOF

cat > /etc/systemd/system/code-explorer-site-pull.timer << 'EOF'
[Unit]
Description=Poll GitHub site-dist branch every minute for CodeExplorer website updates

[Timer]
OnBootSec=30s
OnUnitActiveSec=60s
AccuracySec=5s
Persistent=true

[Install]
WantedBy=timers.target
EOF

systemctl daemon-reload
systemctl enable --now code-explorer-site-pull.timer
systemctl start code-explorer-site-pull.service
systemctl status code-explorer-site-pull.timer --no-pager
echo "Deployed site-dist SHA: $(cat /var/lib/code-explorer-site/last_sha)"
REMOTE_SCRIPT
