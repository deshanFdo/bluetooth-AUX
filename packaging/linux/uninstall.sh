#!/usr/bin/env bash
set -euo pipefail

echo "Uninstalling Audio Switch cleanly..."

pkill -u "${USER}" -x "AudioSwitch" >/dev/null 2>&1 || true
sleep 0.3

if [[ -x "${HOME}/.local/share/AudioSwitch/AudioSwitch" ]]; then
  "${HOME}/.local/share/AudioSwitch/AudioSwitch" --uninstall >/dev/null 2>&1 || true
fi

chmod -R u+w "${HOME}/.local/share/AudioSwitch" >/dev/null 2>&1 || true
chmod -R u+w "${HOME}/.config/AudioSwitch" >/dev/null 2>&1 || true
rm -rf "${HOME}/.local/share/AudioSwitch"
rm -rf "${HOME}/.config/AudioSwitch"
rm -f "${HOME}/.local/share/applications/audioswitch.desktop"
rm -f "${HOME}/.config/autostart/audioswitch.desktop"
rm -f "${HOME}/.local/bin/audioswitch"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "${HOME}/.local/share/applications" >/dev/null 2>&1 || true
fi

echo "Audio Switch and all local settings/files have been removed."
