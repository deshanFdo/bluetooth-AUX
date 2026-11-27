#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BINARY_SRC="${SCRIPT_DIR}/WaveDock"

if [[ ! -f "${BINARY_SRC}" ]]; then
  echo "Error: WaveDock binary not found next to install.sh (${BINARY_SRC})." >&2
  exit 1
fi

chmod +x "${BINARY_SRC}"

echo "Installing WaveDock v1.3.0 for user '${USER}'..."
"${BINARY_SRC}" --install

echo ""
echo "WaveDock installed cleanly."
echo "  Binary:    ${HOME}/.local/share/WaveDock/WaveDock"
echo "  Settings:  ${HOME}/.config/WaveDock/settings.dat (AES-256-GCM encrypted)"
echo "  Uninstall: ${HOME}/.local/share/WaveDock/WaveDock --uninstall (or run ./uninstall.sh)"
echo ""
echo "Launching WaveDock in the background..."
nohup "${HOME}/.local/share/WaveDock/WaveDock" >/dev/null 2>&1 &
disown || true
