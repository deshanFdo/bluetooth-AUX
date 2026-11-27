# Audio Switch — Dynamic Island Audio & Media Control for Windows & Linux

**Live Website & Download:** https://audio-switch.vercel.app  
**Windows Installer (`v1.2.1`, 74.4 MB, Self-Contained):** https://audio-switch.vercel.app/download  
**Windows Portable EXE (`v1.2.1`, 74.4 MB):** https://audio-switch.vercel.app/portable  
**Ubuntu / Debian `.deb` (`v1.3.0`, 36.3 MB, Self-Contained):** https://audio-switch.vercel.app/download-deb  
**Linux Generic `.tar.gz` (`v1.3.0`, 36.3 MB):** https://audio-switch.vercel.app/download-linux

Audio Switch is a lightweight desktop utility for **Windows 10/11** and **Ubuntu / Debian Linux (PipeWire & PulseAudio)** that eliminates having to manually unplug your 3.5mm AUX cable just to listen through an already-connected Bluetooth headset. It reveals an iOS-inspired **Dynamic Island** at the top center of your active monitor.

---

## Features

- **3-Stage Spring-Animated Dynamic Island**
  - **Rest (`~0%` CPU):** Near-invisible top-edge glow bar (`120×14 px` hit target) that stops background timers on Windows.
  - **Hover (`282×40 px` rounded capsule):** Live route indicator, equalizer bars, mini battery rings, or active track progress.
  - **Click / Slide (`348 px` compact deck):** Click and drag horizontally anywhere inside the pill or deck to slide smoothly between **Audio**, **Media (Spotify / YT Music)**, and **Battery & System**.
- **Instant Output & Microphone Routing**
  - Sub-millisecond playback and capture switching between **3.5mm AUX Wired Headphones**, **Bluetooth Headsets**, and **Laptop Speakers** (`IPolicyConfig` Core Audio COM on Windows; PipeWire/PulseAudio `pactl` on Linux) — zero kernel drivers.
  - Optional auto-mute for inactive devices and per-device volume/gain sliders.
- **Per-App Split Audio (`Apps` tab)** — Play songs through wired AUX while Discord uses your Bluetooth headset and microphone simultaneously (`AudioPolicyConfig` on Windows; `sink-inputs` / `source-outputs` routing on Linux).
- **Multi-Output Mirroring with Sync Delay (`0–350 ms`)**
  - Play audio simultaneously on wired AUX headphones/speakers and Bluetooth headsets via user-mode WASAPI loopback + FIFO delay (`170 ms` default) on Windows or `module-loopback` (`latency_msec`) on Linux.
- **Spotify & YouTube Music Switcher, Auto-Launch & Hidden Tray**
  - Switch seamlessly between **Spotify** and **YouTube Music** with 1 tap (auto-pauses the other player), auto-launch on **Play (`▶`)** even when closed, and keep windows tucked off the taskbar on Windows (**Hidden Icons `^`**) with `MPRIS2` control on Linux.
- **Encrypted Settings & Zero-Leftover Uninstaller**
  - Local settings are encrypted with Windows **DPAPI** (`CryptProtectData`) or Linux **AES-256-GCM** (`~/.config/AudioSwitch/settings.dat`). Uninstall cleanly via Windows **Installed Apps**, `sudo apt purge audioswitch`, or `AudioSwitch --uninstall`.
- **Connected-Only Bluetooth Battery Rings & Telemetry**
  - Shows circular gauges exclusively for actively connected Bluetooth headsets (`DEVPKEY_Device_IsConnected` + ContainerId matching on Windows; `org.bluez.Battery1` + UPower on Linux) alongside laptop battery, RAM, CPU, and clock.
- **Persistent Top-Edge Capsule (`~0%` Idle CPU)**
  - Windows: event-driven `HWND_TOPMOST` recovery (`SetWinEventHook`), `Win+D` minimize guard, display/DPI recentering. Linux: X11 dockable overlay + GNOME Wayland capsule and tray icon.

---

## Architecture

| Path | Responsibility |
| --- | --- |
| [`src/AudioSwitch/`](src/AudioSwitch) | Windows WPF `.NET 8` desktop app (Core Audio COM, WASAPI loopback mirror, SMTC, PnP Bluetooth battery, DPAPI settings, Setup Wizard) |
| [`src/AudioSwitch.Linux/`](src/AudioSwitch.Linux) | Linux `.NET 8` + Avalonia UI desktop app (PipeWire/PulseAudio `pactl`, `MPRIS2` via `gdbus`, `BlueZ Battery1`/UPower, AES-256-GCM settings, tray host) |
| [`packaging/linux/`](packaging/linux) | `install.sh`, `uninstall.sh`, and `build_packages.py` generating `.deb` and `.tar.gz` archives |
| [`web/`](web) | Next.js 15 landing page + interactive Dynamic Island browser simulator deployed on Vercel |

---

## Build from Source

### Windows (`x64`)

```powershell
dotnet publish src/AudioSwitch/AudioSwitch.csproj -c Release -r win-x64 -o dist
.\dist\AudioSwitch.exe --diag
```

### Ubuntu / Debian Linux (`linux-x64`)

```bash
dotnet publish src/AudioSwitch.Linux/AudioSwitch.Linux.csproj -c Release -r linux-x64 -o dist-linux
python3 packaging/linux/build_packages.py

# Install .deb or standalone tarball:
sudo apt install ./dist-linux/audioswitch_1.3.0_amd64.deb
```

### Artifact SHA-256 Checksums

```text
F077F5406ED8D4C70C6252C294844F4C483185B62D7C5650BBAB55EA65661944  AudioSwitch-Setup.exe (Windows v1.2.1)
91C8F14043A782821870CAA813CC041973EEBB8E2D4CDDDAC65E6CDB70FBB0B6  audioswitch_1.3.0_amd64.deb (Linux v1.3.0)
6523EA7E218126127B91B5B8C423BFF0335D331025B8564D5D3D1B0DC1725DE5  AudioSwitch-linux-x64.tar.gz (Linux v1.3.0)
```
