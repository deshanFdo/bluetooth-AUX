#!/usr/bin/env bash
set -euo pipefail

echo "Uninstalling WaveDock cleanly..."

pkill -u "${USER}" -x "WaveDock" >/dev/null 2>&1 || true
pkill -u "${USER}" -x "AudioSwitch" >/dev/null 2>&1 || true
sleep 0.3

if [[ -x "${HOME}/.local/share/WaveDock/WaveDock" ]]; then
  "${HOME}/.local/share/WaveDock/WaveDock" --uninstall >/dev/null 2>&1 || true
elif [[ -x "${HOME}/.local/share/AudioSwitch/AudioSwitch" ]]; then
  "${HOME}/.local/share/AudioSwitch/AudioSwitch" --uninstall >/dev/null 2>&1 || true
fi

chmod -R u+w "${HOME}/.local/share/WaveDock" >/dev/null 2>&1 || true
chmod -R u+w "${HOME}/.config/WaveDock" >/dev/null 2>&1 || true
chmod -R u+w "${HOME}/.local/share/AudioSwitch" >/dev/null 2>&1 || true
chmod -R u+w "${HOME}/.config/AudioSwitch" >/dev/null 2>&1 || true
rm -rf "${HOME}/.local/share/WaveDock" "${HOME}/.config/WaveDock"
rm -rf "${HOME}/.local/share/AudioSwitch" "${HOME}/.config/AudioSwitch"
rm -f "${HOME}/.local/share/applications/wavedock.desktop" "${HOME}/.config/autostart/wavedock.desktop"
rm -f "${HOME}/.local/share/applications/audioswitch.desktop" "${HOME}/.config/autostart/audioswitch.desktop"
rm -f "${HOME}/.local/bin/wavedock" "${HOME}/.local/bin/audioswitch"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "${HOME}/.local/share/applications" >/dev/null 2>&1 || true
fi

echo "WaveDock and all local settings/files have been removed."
