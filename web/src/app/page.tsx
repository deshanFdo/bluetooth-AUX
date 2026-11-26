import React from "react";
import {
  BatteryCharging,
  Bluetooth,
  CheckCircle2,
  Cpu,
  Download,
  Github,
  Headphones,
  Layers,
  MonitorSmartphone,
  Music2,
  ShieldCheck,
  Sparkles,
  Trash2,
} from "lucide-react";
import DynamicIslandDemo from "@/components/DynamicIslandDemo";

const INSTALLER_URL =
  "https://github.com/deshanFdo/bluetooth-AUX/releases/latest/download/AudioSwitch-Setup.exe";
const PORTABLE_URL =
  "https://github.com/deshanFdo/bluetooth-AUX/releases/latest/download/AudioSwitch.exe";
const GITHUB_URL = "https://github.com/deshanFdo/bluetooth-AUX";
const SHA256_SETUP =
  "C19C3FC444342B322B0D83F3DD0D100070339AD54EB709986B218EB766BFA133";

const FEATURES = [
  {
    icon: Headphones,
    accent: "text-emerald-400 bg-emerald-500/15 border-emerald-500/30",
    title: "Instant AUX, Bluetooth & Mic Switching",
    desc: "Switch default Windows playback and capture endpoints in <1ms using native Core Audio COM — zero driver installs.",
  },
  {
    icon: Layers,
    accent: "text-cyan-400 bg-cyan-500/15 border-cyan-500/30",
    title: "Multi-Output Mirroring + Sync Delay",
    desc: "Play on wired AUX headphones and Bluetooth headsets simultaneously with an adjustable 0–350 ms FIFO latency compensator.",
  },
  {
    icon: Music2,
    accent: "text-green-400 bg-green-500/15 border-green-500/30",
    title: "Live Spotify & Media Island",
    desc: "Shows album artwork, transport controls, and a real-time interpolated SMTC progress bar across Spotify, YouTube, and Edge.",
  },
  {
    icon: BatteryCharging,
    accent: "text-blue-400 bg-blue-500/15 border-blue-500/30",
    title: "iOS Battery & System Rings",
    desc: "Circular progress gauges for laptop charge, Bluetooth headset battery levels via Windows PnP keys, RAM, and CPU telemetry.",
  },
  {
    icon: MonitorSmartphone,
    accent: "text-purple-400 bg-purple-500/15 border-purple-500/30",
    title: "Multi-Monitor Drag & Smart Snap",
    desc: "Drag the Island to any monitor, or slide horizontally inside the card to swap views. Clicking away springs smoothly back to top-center.",
  },
  {
    icon: Cpu,
    accent: "text-amber-400 bg-amber-500/15 border-amber-500/30",
    title: "0% Idle CPU & System Tray Native",
    desc: "Stops all background audio meters and timers when collapsed. Hides cleanly in Windows System Tray with 0 taskbars cluttered.",
  },
];

export default function HomePage() {
  return (
    <div className="relative min-h-screen overflow-x-hidden bg-[#04060b] text-slate-100">
      {/* Background Mesh Glow */}
      <div className="pointer-events-none fixed inset-0 overflow-hidden">
        <div className="absolute -top-48 left-1/2 h-[420px] w-[720px] -translate-x-1/2 rounded-full bg-blue-600/15 blur-[140px]" />
        <div className="absolute top-64 left-1/4 h-[300px] w-[400px] rounded-full bg-emerald-500/10 blur-[120px]" />
      </div>

      {/* Header Navbar */}
      <header className="sticky top-0 z-40 border-b border-white/10 bg-[#04060b]/80 backdrop-blur-xl">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-3.5">
          <div className="flex items-center gap-2.5">
            <span className="flex h-8 w-8 items-center justify-center rounded-full border border-blue-400/40 bg-blue-500/20">
              <Headphones className="h-4 w-4 text-blue-400" />
            </span>
            <span className="text-sm font-bold tracking-tight text-white">Audio Switch</span>
            <span className="rounded-full border border-white/10 bg-white/5 px-2 py-0.5 text-[10px] font-medium text-slate-300">
              v1.0.0
            </span>
          </div>

          <nav className="hidden items-center gap-7 text-xs font-medium text-slate-300 md:flex">
            <a href="#features" className="transition hover:text-white">
              Features
            </a>
            <a href="#install" className="transition hover:text-white">
              Install Guide
            </a>
            <a
              href={GITHUB_URL}
              target="_blank"
              rel="noreferrer"
              className="flex items-center gap-1.5 transition hover:text-white"
            >
              <Github className="h-3.5 w-3.5" />
              GitHub
            </a>
          </nav>

          <a
            href={INSTALLER_URL}
            className="flex items-center gap-1.5 rounded-full bg-white px-4 py-1.5 text-xs font-semibold text-slate-950 shadow transition hover:scale-[1.03] hover:bg-slate-100"
          >
            <Download className="h-3.5 w-3.5" />
            Download
          </a>
        </div>
      </header>

      <main className="relative z-10 mx-auto max-w-6xl px-6 pb-24 pt-12 md:pt-16">
        {/* HERO SECTION */}
        <section className="grid items-center gap-12 lg:grid-cols-12">
          <div className="space-y-6 lg:col-span-6">
            <div className="inline-flex items-center gap-2 rounded-full border border-blue-400/30 bg-blue-500/10 px-3 py-1 text-xs font-medium text-blue-300">
              <Sparkles className="h-3.5 w-3.5" />
              Windows 10 / 11 (x64) • Standalone • ~0% Idle CPU
            </div>

            <h1 className="text-4xl font-extrabold tracking-tight text-white sm:text-5xl lg:text-[3.35rem] lg:leading-[1.08]">
              iOS Dynamic Island for{" "}
              <span className="bg-gradient-to-r from-blue-400 via-cyan-300 to-emerald-400 bg-clip-text text-transparent">
                Windows Audio.
              </span>
            </h1>

            <p className="max-w-xl text-base leading-relaxed text-slate-300">
              Swap effortlessly between 3.5mm AUX headphones, Bluetooth headsets, laptop speakers,
              and microphones from an unobtrusive top-of-screen pill. Mirror audio with live
              sync delay compensation, control Spotify, and track Bluetooth battery rings — with
              zero taskbars cluttered.
            </p>

            {/* Primary Download CTA */}
            <div className="flex flex-wrap items-center gap-3 pt-1">
              <a
                href={INSTALLER_URL}
                className="group flex items-center gap-2.5 rounded-2xl bg-gradient-to-r from-blue-600 to-emerald-600 px-6 py-3.5 text-sm font-semibold text-white shadow-lg shadow-blue-600/25 transition hover:scale-[1.02] hover:brightness-110"
              >
                <Download className="h-4 w-4 transition group-hover:translate-y-0.5" />
                <span>Download Installer (.exe)</span>
                <span className="rounded-md bg-black/25 px-2 py-0.5 text-[11px] font-medium text-blue-100">
                  70.9 MB
                </span>
              </a>

              <a
                href={PORTABLE_URL}
                className="flex items-center gap-2 rounded-2xl border border-white/15 bg-white/5 px-4 py-3.5 text-sm font-medium text-slate-200 transition hover:bg-white/10 hover:text-white"
              >
                Portable EXE
              </a>

              <a
                href={GITHUB_URL}
                target="_blank"
                rel="noreferrer"
                className="flex items-center gap-2 rounded-2xl border border-white/10 bg-transparent px-4 py-3.5 text-sm font-medium text-slate-400 transition hover:border-white/20 hover:text-white"
              >
                <Github className="h-4 w-4" />
                Source
              </a>
            </div>

            <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-xs text-slate-400">
              <span className="flex items-center gap-1.5">
                <CheckCircle2 className="h-3.5 w-3.5 text-emerald-400" />
                Runtime bundled (no .NET install needed)
              </span>
              <span className="flex items-center gap-1.5">
                <Bluetooth className="h-3.5 w-3.5 text-blue-400" />
                Includes Windows Apps Uninstaller
              </span>
            </div>
          </div>

          <div className="lg:col-span-6">
            <DynamicIslandDemo />
          </div>
        </section>

        {/* METRICS STRIP */}
        <section className="mt-16 grid grid-cols-2 gap-3 rounded-2xl border border-white/10 bg-white/[0.02] p-5 backdrop-blur md:grid-cols-4">
          {[
            { value: "~0%", label: "Steady Idle CPU", sub: "All meters park at rest" },
            { value: "0–350 ms", label: "Sync Delay FIFO", sub: "Align BT & wired speakers" },
            { value: "3 Islands", label: "Slide Gesture", sub: "Audio • Spotify • Battery" },
            { value: "100%", label: "Self-Contained", sub: "Zero runtime prerequisites" },
          ].map((stat) => (
            <div key={stat.label} className="px-2 py-1 text-center md:text-left">
              <p className="text-2xl font-extrabold text-white tabular-nums">{stat.value}</p>
              <p className="text-xs font-semibold text-slate-200">{stat.label}</p>
              <p className="text-[11px] text-slate-400">{stat.sub}</p>
            </div>
          ))}
        </section>

        {/* FEATURES BENTO GRID */}
        <section id="features" className="mt-20">
          <div className="mb-10 max-w-2xl">
            <h2 className="text-2xl font-bold tracking-tight text-white sm:text-3xl">
              Built for Apple HIG smoothness and potato-laptop speed.
            </h2>
            <p className="mt-2 text-sm text-slate-400">
              Concentric corner geometry, live spring physics, direct Win32 Core Audio routing, and
              event-driven Windows SMTC playback controls.
            </p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {FEATURES.map((feat) => {
              const Icon = feat.icon;
              return (
                <div
                  key={feat.title}
                  className="rounded-2xl border border-white/10 bg-[#0b101b]/70 p-5 transition hover:border-white/20 hover:bg-[#0e1524]"
                >
                  <div
                    className={`mb-4 inline-flex h-10 w-10 items-center justify-center rounded-xl border ${feat.accent}`}
                  >
                    <Icon className="h-5 w-5" />
                  </div>
                  <h3 className="text-base font-semibold text-white">{feat.title}</h3>
                  <p className="mt-1.5 text-xs leading-relaxed text-slate-400">{feat.desc}</p>
                </div>
              );
            })}
          </div>
        </section>

        {/* INSTALL & UNINSTALL GUIDE */}
        <section id="install" className="mt-20 grid gap-6 lg:grid-cols-12">
          <div className="rounded-2xl border border-white/10 bg-[#0b101b]/75 p-6 lg:col-span-7">
            <h2 className="text-xl font-bold text-white">Install on any Windows 10/11 PC</h2>
            <p className="mt-1 text-xs text-slate-400">
              Per-user install in <code className="text-slate-200">%LocalAppData%\Programs\AudioSwitch</code> — no admin password or separate runtime required.
            </p>

            <ol className="mt-5 space-y-4 text-sm">
              <li className="flex gap-3">
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-blue-600/30 text-xs font-bold text-blue-300">
                  1
                </span>
                <div>
                  <p className="font-semibold text-white">Download AudioSwitch-Setup.exe</p>
                  <p className="text-xs text-slate-400">
                    Click Download above. Everything needed (.NET 8 + WPF desktop runtime) is
                    packed inside one signed-ready single file.
                  </p>
                </div>
              </li>
              <li className="flex gap-3">
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-blue-600/30 text-xs font-bold text-blue-300">
                  2
                </span>
                <div>
                  <p className="font-semibold text-white">Run the dark-glass Setup Wizard</p>
                  <p className="text-xs text-slate-400">
                    Choose Start Menu / Desktop shortcuts and Startup auto-launch, then click{" "}
                    <strong className="text-slate-200">Install Audio Switch</strong>.
                  </p>
                </div>
              </li>
              <li className="flex gap-3">
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-emerald-600/30 text-xs font-bold text-emerald-300">
                  3
                </span>
                <div>
                  <p className="font-semibold text-white">Hover top-center or check System Tray</p>
                  <p className="text-xs text-slate-400">
                    Hover the faint glow at the top of your screen to reveal the rounded pill, or
                    click &amp; drag left/right to slide between Audio, Spotify, and Battery.
                  </p>
                </div>
              </li>
            </ol>
          </div>

          <div className="flex flex-col justify-between gap-4 rounded-2xl border border-white/10 bg-[#0b101b]/75 p-6 lg:col-span-5">
            <div className="space-y-4">
              <div className="flex items-start gap-3">
                <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-amber-400" />
                <div>
                  <h3 className="text-sm font-semibold text-white">Windows SmartScreen Note</h3>
                  <p className="mt-1 text-xs leading-relaxed text-slate-400">
                    Because this open-source app is unsigned, Windows Defender SmartScreen may show{" "}
                    <em>&ldquo;Windows protected your PC&rdquo;</em> on first launch. Click{" "}
                    <strong className="text-slate-200">More info → Run anyway</strong>, or inspect
                    the full C# source code on GitHub.
                  </p>
                </div>
              </div>

              <div className="flex items-start gap-3">
                <Trash2 className="mt-0.5 h-5 w-5 shrink-0 text-slate-400" />
                <div>
                  <h3 className="text-sm font-semibold text-white">Clean Windows Uninstall</h3>
                  <p className="mt-1 text-xs leading-relaxed text-slate-400">
                    Registers cleanly in{" "}
                    <strong className="text-slate-200">
                      Windows Settings → Apps → Installed Apps
                    </strong>{" "}
                    so removing it deletes shortcuts, startup entries, and app files cleanly.
                  </p>
                </div>
              </div>
            </div>

            <div className="rounded-xl border border-white/10 bg-black/40 p-3">
              <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">
                Installer SHA-256 (v1.0.0)
              </p>
              <p className="mt-1 break-all font-mono text-[10.5px] text-emerald-300">
                {SHA256_SETUP}
              </p>
            </div>
          </div>
        </section>
      </main>

      <footer className="border-t border-white/10 py-8 text-center text-xs text-slate-500">
        <p>
          Audio Switch v1.0.0 • Open-Source Windows Dynamic Island Utility •{" "}
          <a
            href={GITHUB_URL}
            target="_blank"
            rel="noreferrer"
            className="text-slate-300 underline-offset-4 hover:underline"
          >
            github.com/deshanFdo/bluetooth-AUX
          </a>
        </p>
      </footer>
    </div>
  );
}
