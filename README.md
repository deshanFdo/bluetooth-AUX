# Audio Switch — Dynamic Island Audio & Media Control for Windows

**Live Website & Download:** https://audio-switch.vercel.app  
**Direct Windows Installer (`v1.2.1`, 74.4 MB, Self-Contained):** https://audio-switch.vercel.app/download  
**Portable Executable (`v1.2.1`, 74.4 MB):** https://audio-switch.vercel.app/portable

Audio Switch is a lightweight Windows 10 / 11 desktop utility that eliminates having to manually unplug your 3.5mm AUX cable just to listen through an already-connected Bluetooth headset. It lives quietly in the Windows System Tray (**Hidden Icons**) and reveals an iOS-inspired **Dynamic Island** at the top center of your active monitor.

---

## Features

- **3-Stage Spring-Animated Dynamic Island**
  - **Rest (`~0%` CPU):** Near-invisible top-edge glow bar (`120×14 px` hit target) that stops all audio peak meters and timers.
  - **Hover (`282×40 px` rounded capsule):** Live route indicator, equalizer bars, mini battery rings, or Spotify track progress.
  - **Click / Slide (`348 px` compact deck):** Click and drag horizontally anywhere inside the pill or deck to slide smoothly between **Audio**, **Spotify**, and **Battery & System**.
- **Instant Output & Microphone Routing**
  - Sub-millisecond playback and capture switching between **3.5mm AUX Wired Headphones**, **Bluetooth Headsets**, and **Laptop Speakers** using native Win32 Core Audio COM (`IPolicyConfig`) — zero kernel drivers.
  - Optional auto-mute for inactive devices and per-device volume/gain sliders.
- **Per-App Split Audio (`Apps` tab)** — Play songs through wired AUX while Discord uses your Bluetooth headset and microphone simultaneously via Windows `AudioPolicyConfig`
- **Multi-Output Mirroring with Sync Delay (`0–350 ms`)**
  - Play audio simultaneously on wired AUX headphones/speakers and Bluetooth headsets via a user-mode WASAPI loopback capture & render pipeline with an adjustable `170 ms` default FIFO delay compensator.
- **Spotify & YouTube Music Auto-Launch & Hidden Tray Mode**
  - Switch seamlessly between **Spotify** and **YouTube Music** with 1 tap (auto-pauses the other player), auto-launch on **Play (`▶`)** even when closed, and keep windows off the taskbar in Hidden Tray icons (`^`). Protected local settings (`settings.dat` via Windows DPAPI) and clean zero-leftover uninstaller.
- **Connected-Only Bluetooth Battery Rings & Telemetry**
  - Shows circular gauges exclusively for actively connected Bluetooth headsets (`DEVPKEY_Device_IsConnected`) alongside laptop battery, RAM, CPU, and clock.
- **Persistent Top-Edge Capsule (`~0%` Idle CPU)**
  - Event-driven `HWND_TOPMOST` recovery (`SetWinEventHook`), `Win+D` minimize guard, display/DPI recentering, and crisp contrast rim across light and dark windows.
- **Built-In Self-Contained Setup Wizard & Clean Uninstaller**
  - Bundles the `.NET 8` desktop runtime and native WPF libraries into a single `74.4 MB` executable (`AudioSwitch-Setup.exe`) with Start Menu/Desktop shortcuts, Windows startup toggle, and clean **Windows Settings → Installed Apps** removal (`--uninstall`).

---

## Architecture

| Path | Responsibility |
| --- | --- |
| [`src/AudioSwitch/AudioCore/`](src/AudioSwitch/AudioCore) | Core Audio COM (`IMMDeviceEnumerator`, `IPolicyConfig`), WASAPI loopback mirror + FIFO delay, SMTC media timeline client, PnP Bluetooth battery reader, JSON settings, per-user installer |
| [`src/AudioSwitch/Controls/`](src/AudioSwitch/Controls) | Reusable vector `CircularGauge` user control |
| [`src/AudioSwitch/MainWindow.xaml`](src/AudioSwitch/MainWindow.xaml) | 3-stage Dynamic Island UI, concentric Apple HIG radii, System Tray host |
| [`src/AudioSwitch/SetupWindow.xaml`](src/AudioSwitch/SetupWindow.xaml) | Dark-glass Windows Setup Wizard & uninstaller flow |
| [`web/`](web) | Next.js 15 landing page + interactive Dynamic Island browser simulator deployed on Vercel |

---

## Build from Source

Requires **.NET 8 SDK (`net8.0-windows10.0.19041.0`)** on Windows:

```powershell
dotnet publish src/AudioSwitch/AudioSwitch.csproj -c Release -r win-x64 -o dist
```

Diagnostics check (lists detected endpoints, COM interface probe, and Bluetooth battery keys):

```powershell
.\dist\AudioSwitch.exe --diag
```

### Installer SHA-256 (`v1.2.1`)

```text
F077F5406ED8D4C70C6252C294844F4C483185B62D7C5650BBAB55EA65661944  AudioSwitch-Setup.exe
```
