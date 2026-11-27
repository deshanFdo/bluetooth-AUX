#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BINARY_SRC="${SCRIPT_DIR}/AudioSwitch"

if [[ ! -f "${BINARY_SRC}" ]]; then
  echo "Error: AudioSwitch binary not found next to install.sh (${BINARY_SRC})." >&2
  exit 1
fi

chmod +x "${BINARY_SRC}"

echo "Installing Audio Switch v1.3.0 for user '${USER}'..."
"${BINARY_SRC}" --install

echo ""
echo "Audio Switch installed cleanly."
echo "  Binary:    ${HOME}/.local/share/AudioSwitch/AudioSwitch"
echo "  Settings:  ${HOME}/.config/AudioSwitch/settings.dat (AES-256-GCM encrypted)"
echo "  Uninstall: ${HOME}/.local/share/AudioSwitch/AudioSwitch --uninstall (or run ./uninstall.sh)"
echo ""
echo "Launching Audio Switch in the background..."
nohup "${HOME}/.local/share/AudioSwitch/AudioSwitch" >/dev/null 2>&1 &
disown || true
