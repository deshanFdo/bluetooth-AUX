import React from "react";
import Link from "next/link";
import {
  ArrowDownToLine,
  BatteryCharging,
  Bluetooth,
  CheckCircle2,
  Cpu,
  EyeOff,
  Folder,
  Github,
  Headphones,
  HelpCircle,
  Info,
  Laptop,
  Layers,
  Mail,
  Mic,
  Monitor,
  Music2,
  Package,
  Radio,
  Shield,
  ShieldAlert,
  Sliders,
  Sparkles,
  Split,
  Tag,
  Terminal,
} from "lucide-react";
import DynamicIslandDemo from "@/components/DynamicIslandDemo";
import CopyShaButton from "@/components/CopyShaButton";
import DownloadButton from "@/components/DownloadButton";

const INSTALLER_URL =
  "https://github.com/deshanFdo/bluetooth-AUX/releases/download/v1.3.0/AudioSwitch-Setup.exe";
const PORTABLE_URL =
  "https://github.com/deshanFdo/bluetooth-AUX/releases/download/v1.3.0/AudioSwitch.exe";
const DEB_URL =
  "https://github.com/deshanFdo/bluetooth-AUX/releases/download/v1.3.0/audioswitch_1.3.0_amd64.deb";
const TAR_LINUX_URL =
  "https://github.com/deshanFdo/bluetooth-AUX/releases/download/v1.3.0/AudioSwitch-linux-x64.tar.gz";
const GITHUB_URL = "https://github.com/deshanFdo/bluetooth-AUX";
const SHA256_SETUP =
  "8BF943E92676DB8FACDAB45DA71C6A94CDBED496BE8D57A8F8661754A9B21B29";
const SHA256_DEB =
  "260A067D84F4954ABCE53A7D1295EDBA7453ECF888D344DB6EA7D30B67C138A8";
const SHA256_TAR_LINUX =
  "8324A04F556901AB0437F3298C35C5939EBB5B97C2B922746C4387CD3B64804F";

const FAQS = [
  {
    q: "How do I listen through an already-connected Bluetooth headset without unplugging my AUX cable?",
    a: "Hover the top edge of your screen, click the Audio Switch pill, and toggle Bluetooth on. It switches your default output in <1ms on Windows (<5ms on PipeWire) and can mute the idle 3.5mm jack automatically — so you never have to unplug your AUX cable again.",
  },
  {
    q: "Can I play songs on AUX while using my Bluetooth headset and mic for a Discord call?",
    a: "Yes. Open the Apps tab inside Audio Island and click Dual Split (Music → AUX + Calls → Bluetooth), or pick endpoints per app. Windows uses AudioPolicyConfig across child PIDs; Ubuntu/Debian Linux moves live PipeWire/PulseAudio sink-inputs and source-outputs via pactl.",
  },
  {
    q: "What happens if I click Play when Spotify or YouTube Music is closed, and can I switch between them?",
    a: "Yes. Pick Spotify or YT Music in the Media card (it auto-pauses the other player). Clicking Play launches Spotify or YouTube Music automatically even when closed, and on Windows hides both windows off your taskbar into Hidden Icons (^).",
  },
  {
    q: "Are installed files and settings encrypted so nobody can change them after downloading?",
    a: "Settings (settings.dat) and installed binary seals (install.sig) are encrypted via Windows DPAPI or Linux AES-256-GCM, and installed binaries are locked ReadOnly (Windows) / 0500 or root:root .deb (Linux) with startup SHA-256 verification. Because unsigned binaries must remain unencrypted for OS execution, verify your download with the SHA-256 hashes below.",
  },
  {
    q: "How does Ubuntu / Debian Linux support work, and what desktop sessions are supported?",
    a: "Audio Switch Linux v1.3.0 uses Avalonia (.NET 8 self-contained linux-x64), PipeWire/PulseAudio (pactl + module-loopback), MPRIS2 over D-Bus (gdbus), BlueZ org.bluez.Battery1 / UPower, and AES-256-GCM encrypted settings. X11 docks cleanly at the top edge; GNOME Wayland shows the top-center capsule and full tray icon.",
  },
  {
    q: "How do I play the same audio on Bluetooth headphones and wired AUX together without echo?",
    a: "Use Multi-Output Mirror under Outputs. Windows captures via user-mode WASAPI loopback with a 0–350 ms FIFO buffer; Linux spawns module-loopback with configurable latency_msec (170 ms default) so wired AUX aligns with wireless Bluetooth latency.",
  },
];

const JSON_LD = {
  "@context": "https://schema.org",
  "@graph": [
    {
      "@type": "SoftwareApplication",
      name: "Audio Switch",
      applicationCategory: "UtilitiesApplication",
      operatingSystem: "Windows 10, Windows 11, Ubuntu / Debian Linux (x64)",
      softwareVersion: "1.3.0",
      fileSize: "36.3MB (Linux) / 74.4MB (Windows)",
      downloadUrl: INSTALLER_URL,
      url: "https://audio-switch.vercel.app",
      sameAs: [GITHUB_URL],
      description:
        "iOS-style top-of-screen Dynamic Island for Windows 10/11 and Ubuntu/Debian Linux: switch AUX headphones, Bluetooth headsets, speakers, and microphones without unplugging cables, split Spotify/browser music onto AUX while Discord calls use Bluetooth, switch seamlessly between Spotify and YouTube Music, mirror multi-output audio with sync delay, and monitor connected Bluetooth battery rings at ~0% idle CPU.",
      author: {
        "@type": "Person",
        name: "Deshan Fernando",
      },
      offers: {
        "@type": "Offer",
        price: "0",
        priceCurrency: "USD",
      },
      featureList: [
        "Instant AUX wired headphone, Bluetooth headset, speaker, and microphone switching (Windows Core Audio COM & Linux PipeWire/PulseAudio)",
        "Per-app split audio routing (play music on AUX while Discord uses your Bluetooth headset and mic)",
        "One-tap Spotify and YouTube Music switching with automatic pause handoff, auto-launch, and Windows hidden tray / Linux MPRIS2",
        "Simultaneous multi-output mirroring with 0-350ms sync delay compensation",
        "Connected laptop and Bluetooth headset battery rings (Windows PnP & Linux BlueZ Battery1)",
        "Encrypted local settings (Windows DPAPI & Linux AES-256-GCM) and zero-leftover uninstaller",
      ],
    },
    {
      "@type": "FAQPage",
      mainEntity: FAQS.map((item) => ({
        "@type": "Question",
        name: item.q,
        acceptedAnswer: {
          "@type": "Answer",
          text: item.a,
        },
      })),
    },
  ],
};

export default function HomePage() {
  return (
    <div className="min-h-screen overflow-x-hidden bg-[#050507] text-[#f4f4f6]">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: JSON.stringify(JSON_LD).replace(/</g, "\\u003c"),
        }}
      />
      {/* Minimal Top Bar */}
      <header className="border-b border-white/[0.07]">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-2.5">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg border border-white/15 bg-white/[0.04]">
              <Headphones className="h-3.5 w-3.5 text-emerald-400" />
            </span>
            <span className="text-sm font-semibold tracking-tight text-white">Audio Switch</span>
            <span className="rounded border border-white/10 bg-white/[0.03] px-1.5 py-0.5 font-mono text-[10.5px] text-neutral-400">
              v1.3.0
            </span>
          </div>

          <div className="flex items-center gap-5 text-xs text-neutral-400">
            <a
              href="#how-it-works"
              className="hidden items-center gap-1.5 transition hover:text-white sm:inline-flex"
            >
              <Sliders className="h-3.5 w-3.5" />
              Capabilities
            </a>
            <a
              href="#install"
              className="hidden items-center gap-1.5 transition hover:text-white sm:inline-flex"
            >
              <Package className="h-3.5 w-3.5" />
              Install
            </a>
            <a
              href={GITHUB_URL}
              target="_blank"
              rel="noreferrer"
              className="hidden items-center gap-1.5 transition hover:text-white sm:inline-flex"
            >
              <Github className="h-3.5 w-3.5" />
              GitHub
            </a>
            <a
              href={INSTALLER_URL}
              download="AudioSwitch-Setup.exe"
              className="inline-flex items-center gap-1.5 rounded-full bg-white px-3.5 py-1.5 text-xs font-semibold text-black transition hover:bg-neutral-200"
            >
              <ArrowDownToLine className="h-3.5 w-3.5" />
              Download
            </a>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-6 pb-24 pt-14 md:pt-20">
        {/* Centered Editorial Hero */}
        <section className="mx-auto max-w-2xl text-center">
          <p className="inline-flex items-center gap-2 font-mono text-xs tracking-wide text-neutral-400">
            <Laptop className="h-3.5 w-3.5 text-neutral-500" />
            WINDOWS 10/11 &amp; UBUNTU / DEBIAN LINUX (x64) · SELF-CONTAINED
          </p>

          <h1 className="mt-4 text-4xl font-semibold tracking-[-0.035em] text-white sm:text-5xl md:text-[3.5rem] md:leading-[1.06]">
            The top-edge Dynamic Island for Windows &amp; Linux audio.
          </h1>

          <p className="mx-auto mt-5 max-w-xl text-[15.5px] leading-relaxed text-neutral-400">
            No longer need to unplug your 3.5mm AUX cable just to listen through a Bluetooth
            headset that&apos;s already connected — switch outputs &amp; mics instantly, split
            music onto AUX while Discord uses your headset, or click Play to launch Spotify or
            YouTube Music on Windows and Ubuntu/Debian Linux.
          </p>

          <div className="mt-8 flex flex-col items-stretch justify-center gap-3 sm:flex-row sm:flex-wrap sm:items-center">
            <DownloadButton
              href={INSTALLER_URL}
              filename="AudioSwitch-Setup.exe"
              label="Windows Installer"
              sizeBadge="74.4 MB"
            />

            <a
              href={DEB_URL}
              download="audioswitch_1.3.0_amd64.deb"
              className="inline-flex items-center justify-center gap-2 rounded-full border border-emerald-400/35 bg-emerald-400/10 px-5 py-3 text-sm font-medium text-emerald-200 transition hover:border-emerald-400/60 hover:bg-emerald-400/20 hover:text-white"
            >
              <Terminal className="h-4 w-4 text-emerald-400" />
              Ubuntu / Debian .deb
              <span className="font-mono text-xs text-emerald-300/80">36.3 MB</span>
            </a>

            <a
              href={PORTABLE_URL}
              download="AudioSwitch.exe"
              className="inline-flex items-center justify-center gap-2 rounded-full border border-white/15 px-4 py-3 text-sm font-medium text-neutral-200 transition hover:border-white/30 hover:text-white"
            >
              <Package className="h-4 w-4 text-neutral-400" />
              Portable .exe
            </a>

            <a
              href={TAR_LINUX_URL}
              download="AudioSwitch-linux-x64.tar.gz"
              className="inline-flex items-center justify-center gap-2 rounded-full border border-white/15 px-4 py-3 text-sm font-medium text-neutral-200 transition hover:border-white/30 hover:text-white"
            >
              <Package className="h-4 w-4 text-neutral-400" />
              Linux .tar.gz
            </a>
          </div>

          <p className="mt-3 inline-flex items-center gap-1.5 text-xs text-neutral-500">
            <CheckCircle2 className="h-3.5 w-3.5 text-emerald-400/80" />
            Runtime bundled (.NET 8) — no extra dependencies. Clean one-click &amp; apt purge removal.
          </p>
        </section>

        {/* Live Desktop Stage */}
        <section className="mx-auto mt-12 max-w-3xl">
          <DynamicIslandDemo />
        </section>

        {/* Clean 6-Capability Engineering Grid */}
        <section id="how-it-works" className="mt-24 border-t border-white/[0.08] pt-14">
          <div className="grid gap-x-10 gap-y-10 sm:grid-cols-2 lg:grid-cols-3">
            <div>
              <div className="flex items-center gap-2 font-mono text-[11px] text-neutral-500">
                <Headphones className="h-3.5 w-3.5 text-emerald-400" />
                <Bluetooth className="h-3.5 w-3.5 text-blue-400" />
                <span>01 / ZERO-UNPLUG ROUTING</span>
              </div>
              <h2 className="mt-2.5 text-base font-semibold text-white">
                Instant AUX, BT &amp; Mic switching
              </h2>
              <p className="mt-2 text-sm leading-relaxed text-neutral-400">
                Uses native Windows Core Audio COM (<code className="text-neutral-300">IPolicyConfig</code>)
                or Linux PipeWire / PulseAudio (<code className="text-neutral-300">pactl</code>) for
                instant output and mic switching — so you never pull your 3.5mm plug again.
              </p>
            </div>

            <div>
              <div className="flex items-center gap-2 font-mono text-[11px] text-neutral-500">
                <Split className="h-3.5 w-3.5 text-cyan-400" />
                <Mic className="h-3.5 w-3.5 text-purple-400" />
                <span>02 / PER-APP SPLIT AUDIO</span>
              </div>
              <h2 className="mt-2.5 text-base font-semibold text-white">
                Songs on AUX, Discord on headset
              </h2>
              <p className="mt-2 text-sm leading-relaxed text-neutral-400">
                Inside the <strong className="font-medium text-neutral-200">Apps</strong> tab, one
                click splits Spotify or browsers to wired AUX while routing Discord, Teams, or Zoom
                voice and mic to Bluetooth (<code className="text-neutral-300">AudioPolicyConfig</code> / PipeWire).
              </p>
            </div>

            <div>
              <div className="flex items-center gap-2 font-mono text-[11px] text-neutral-500">
                <Radio className="h-3.5 w-3.5 text-cyan-400" />
                <span>03 / MULTI-OUTPUT MIRROR</span>
              </div>
              <h2 className="mt-2.5 text-base font-semibold text-white">
                Play AUX + Bluetooth in sync
              </h2>
              <p className="mt-2 text-sm leading-relaxed text-neutral-400">
                Mirrors primary audio via user-mode WASAPI loopback or PipeWire{" "}
                <code className="text-neutral-300">module-loopback</code> with a{" "}
                <code className="text-neutral-300">0–350 ms</code> delay slider (170 ms default)
                to match Bluetooth latency.
              </p>
            </div>

            <div>
              <div className="flex items-center gap-2 font-mono text-[11px] text-neutral-500">
                <Music2 className="h-3.5 w-3.5 text-green-400" />
                <EyeOff className="h-3.5 w-3.5 text-emerald-400" />
                <span>04 / SPOTIFY &amp; YT MUSIC TRAY</span>
              </div>
              <h2 className="mt-2.5 text-base font-semibold text-white">
                Auto-launch, play &amp; switch cleanly
              </h2>
              <p className="mt-2 text-sm leading-relaxed text-neutral-400">
                Switch seamlessly between Spotify and YouTube Music with automatic pause handoff,
                click Play to launch even when closed, and use Windows Hidden Icons or Linux MPRIS2.
              </p>
            </div>

            <div>
              <div className="flex items-center gap-2 font-mono text-[11px] text-neutral-500">
                <BatteryCharging className="h-3.5 w-3.5 text-emerald-400" />
                <span>05 / CONNECTED BATTERIES</span>
              </div>
              <h2 className="mt-2.5 text-base font-semibold text-white">
                Live headset &amp; system rings
              </h2>
              <p className="mt-2 text-sm leading-relaxed text-neutral-400">
                Drag across the capsule to inspect laptop charge, RAM, CPU, clock, and connected-only
                Bluetooth headset battery levels via PnP or{" "}
                <code className="text-neutral-300">org.bluez.Battery1</code>.
              </p>
            </div>

            <div>
              <div className="flex items-center gap-2 font-mono text-[11px] text-neutral-500">
                <Shield className="h-3.5 w-3.5 text-blue-400" />
                <span>06 / HARDENED &amp; ~0% IDLE</span>
              </div>
              <h2 className="mt-2.5 text-base font-semibold text-white">
                Encrypted, locked &amp; clean removal
              </h2>
              <p className="mt-2 text-sm leading-relaxed text-neutral-400">
                DPAPI / AES-256-GCM encrypted <code className="text-neutral-300">settings.dat</code> &amp;{" "}
                <code className="text-neutral-300">install.sig</code>, System32 DLL lock, same-user IPC
                ACLs, and zero-leftover removal (<code className="text-neutral-300">--uninstall</code> /{" "}
                <code className="text-neutral-300">apt purge</code>).
              </p>
            </div>
          </div>

          {/* Hardened Security Posture Strip */}
          <div className="mt-10 rounded-xl border border-emerald-500/20 bg-emerald-500/[0.04] p-4 sm:p-5">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="flex items-center gap-2 font-mono text-xs font-semibold tracking-wide text-emerald-300">
                <Shield className="h-4 w-4 text-emerald-400" />
                DEFENSE-IN-DEPTH SECURITY ARCHITECTURE
              </p>
              <Link
                href="/privacy"
                className="font-mono text-[11px] text-emerald-300/80 underline decoration-emerald-400/30 underline-offset-4 hover:text-white"
              >
                Full security &amp; privacy spec →
              </Link>
            </div>
            <div className="mt-3 grid gap-3 text-xs text-neutral-300 sm:grid-cols-2 lg:grid-cols-4">
              <div className="rounded-lg border border-white/[0.06] bg-black/30 p-3">
                <p className="font-medium text-white">Offline &amp; Zero Telemetry</p>
                <p className="mt-1 text-neutral-400">
                  Zero outbound sockets, analytics, update daemons, or audio/mic disk capture.
                </p>
              </div>
              <div className="rounded-lg border border-white/[0.06] bg-black/30 p-3">
                <p className="font-medium text-white">Binary &amp; IPC Hardening</p>
                <p className="mt-1 text-neutral-400">
                  <code className="text-neutral-300">System32</code> P/Invoke lock prevents DLL
                  hijacking; NamedPipe ACL &amp; zero shell interpolation prevent local elevation.
                </p>
              </div>
              <div className="rounded-lg border border-white/[0.06] bg-black/30 p-3">
                <p className="font-medium text-white">Encrypted Config &amp; Seal</p>
                <p className="mt-1 text-neutral-400">
                  DPAPI &amp; AES-256-GCM protect <code className="text-neutral-300">settings.dat</code>{" "}
                  and <code className="text-neutral-300">install.sig</code>; binaries lock{" "}
                  <code className="text-neutral-300">ReadOnly</code> / <code className="text-neutral-300">0500</code>.
                </p>
              </div>
              <div className="rounded-lg border border-white/[0.06] bg-black/30 p-3">
                <p className="font-medium text-white">Hardened Web Headers</p>
                <p className="mt-1 text-neutral-400">
                  Enforces strict CSP, 2-year HSTS preload, <code className="text-neutral-300">nosniff</code>,{" "}
                  <code className="text-neutral-300">DENY</code> framing, and COOP/CORP isolation.
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* Specs & Install Sheet */}
        <section
          id="install"
          className="mt-20 overflow-hidden rounded-2xl border border-white/[0.09] bg-[#0a0c10] p-5 sm:p-8"
        >
          <div className="grid gap-8 md:grid-cols-2">
            <div className="min-w-0">
              <h2 className="flex items-center gap-2 text-lg font-semibold text-white">
                <Layers className="h-4 w-4 text-neutral-400" />
                Install on Windows &amp; Ubuntu / Debian
              </h2>
              <ol className="mt-4 space-y-3.5 text-sm text-neutral-300">
                <li className="flex items-start gap-2.5">
                  <ArrowDownToLine className="mt-0.5 h-4 w-4 shrink-0 text-blue-400" />
                  <span>
                    <strong className="font-medium text-white">Windows (v1.3.0):</strong> Download{" "}
                    <a
                      href={INSTALLER_URL}
                      download="AudioSwitch-Setup.exe"
                      className="break-words underline decoration-white/30 underline-offset-4 hover:text-white"
                    >
                      AudioSwitch-Setup.exe
                    </a>
                    , choose <strong className="font-medium text-white">More info → Run anyway</strong>{" "}
                    if SmartScreen prompts, and click Install.
                  </span>
                </li>
                <li className="flex items-start gap-2.5">
                  <Terminal className="mt-0.5 h-4 w-4 shrink-0 text-emerald-400" />
                  <span>
                    <strong className="font-medium text-white">Ubuntu / Debian Linux (v1.3.0):</strong>{" "}
                    Download{" "}
                    <a
                      href={DEB_URL}
                      download="audioswitch_1.3.0_amd64.deb"
                      className="break-words underline decoration-white/30 underline-offset-4 hover:text-white"
                    >
                      audioswitch_1.3.0_amd64.deb
                    </a>{" "}
                    and run{" "}
                    <code className="break-all rounded bg-white/[0.06] px-1.5 py-0.5 text-xs text-emerald-300">
                      sudo apt install ./audioswitch_1.3.0_amd64.deb
                    </code>
                    .
                  </span>
                </li>
                <li className="flex items-start gap-2.5">
                  <Sparkles className="mt-0.5 h-4 w-4 shrink-0 text-cyan-400" />
                  <span>
                    <strong className="font-medium text-white">Generic Linux Tarball:</strong> Extract{" "}
                    <a
                      href={TAR_LINUX_URL}
                      download="AudioSwitch-linux-x64.tar.gz"
                      className="break-words underline decoration-white/30 underline-offset-4 hover:text-white"
                    >
                      AudioSwitch-linux-x64.tar.gz
                    </a>{" "}
                    and run <code className="text-neutral-200">./install.sh</code> (no sudo).
                  </span>
                </li>
              </ol>
            </div>

            <div className="flex min-w-0 flex-col justify-between gap-4 border-t border-white/[0.07] pt-6 md:border-l md:border-t-0 md:pl-8 md:pt-0">
              <dl className="grid grid-cols-1 gap-y-3 text-xs sm:grid-cols-[148px_minmax(0,1fr)] sm:gap-x-4">
                <dt className="flex items-center gap-1.5 text-neutral-500">
                  <Folder className="h-3.5 w-3.5" />
                  Install paths
                </dt>
                <dd className="min-w-0 break-all font-mono text-neutral-200">
                  %LocalAppData%\Programs\AudioSwitch · /usr/lib/audioswitch
                </dd>
                <dt className="flex items-center gap-1.5 text-neutral-500">
                  <Monitor className="h-3.5 w-3.5" />
                  Architectures
                </dt>
                <dd className="min-w-0 text-neutral-200">
                  Windows 10/11 (x64) · Ubuntu / Debian (amd64)
                </dd>
                <dt className="flex items-center gap-1.5 text-neutral-500">
                  <ShieldAlert className="h-3.5 w-3.5" />
                  Clean uninstall
                </dt>
                <dd className="min-w-0 font-mono text-neutral-200">
                  Installed Apps · sudo apt purge audioswitch
                </dd>
                <dt className="flex items-center gap-1.5 text-neutral-500">
                  <Tag className="h-3.5 w-3.5" />
                  License / Cost
                </dt>
                <dd className="min-w-0 text-neutral-200">Free · Open Source</dd>
              </dl>

              <div className="space-y-2.5">
                <div className="min-w-0 rounded-xl border border-white/[0.08] bg-black/50 p-3">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="inline-flex items-center gap-1.5 font-mono text-[10px] uppercase tracking-wider text-neutral-500">
                      <Terminal className="h-3 w-3" />
                      SHA-256 (Windows v1.3.0 Setup &amp; Portable)
                    </span>
                    <CopyShaButton value={SHA256_SETUP} />
                  </div>
                  <p className="mt-1.5 break-all font-mono text-[11px] text-neutral-300">
                    {SHA256_SETUP}
                  </p>
                </div>

                <div className="min-w-0 rounded-xl border border-white/[0.08] bg-black/50 p-3">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="inline-flex items-center gap-1.5 font-mono text-[10px] uppercase tracking-wider text-neutral-500">
                      <Terminal className="h-3 w-3 text-emerald-400" />
                      SHA-256 (Ubuntu / Debian v1.3.0 .deb)
                    </span>
                    <CopyShaButton value={SHA256_DEB} />
                  </div>
                  <p className="mt-1.5 break-all font-mono text-[11px] text-neutral-300">
                    {SHA256_DEB}
                  </p>
                </div>

                <div className="min-w-0 rounded-xl border border-white/[0.08] bg-black/50 p-3">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="inline-flex items-center gap-1.5 font-mono text-[10px] uppercase tracking-wider text-neutral-500">
                      <Terminal className="h-3 w-3 text-cyan-400" />
                      SHA-256 (Linux v1.3.0 .tar.gz)
                    </span>
                    <CopyShaButton value={SHA256_TAR_LINUX} />
                  </div>
                  <p className="mt-1.5 break-all font-mono text-[11px] text-neutral-300">
                    {SHA256_TAR_LINUX}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* Concise FAQ */}
        <section id="faq" className="mt-16 border-t border-white/[0.08] pt-12">
          <h2 className="flex items-center gap-2 text-base font-semibold text-white">
            <HelpCircle className="h-4 w-4 text-neutral-400" />
            Frequently asked questions
          </h2>
          <div className="mt-6 grid gap-x-10 gap-y-6 md:grid-cols-2">
            {FAQS.map((item) => (
              <div key={item.q}>
                <h3 className="text-sm font-medium text-neutral-100">{item.q}</h3>
                <p className="mt-1.5 text-xs leading-relaxed text-neutral-400">{item.a}</p>
              </div>
            ))}
          </div>
        </section>
      </main>

      <footer className="border-t border-white/[0.07] py-6 text-center text-xs text-neutral-500">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-center gap-x-5 gap-y-2 px-6">
          <span>Audio Switch v1.3.0 · Windows &amp; Ubuntu/Debian Linux · Deshan Fernando</span>
          <Link href="/about" className="inline-flex items-center gap-1 hover:text-neutral-300">
            <Info className="h-3 w-3" />
            About
          </Link>
          <Link href="/privacy" className="inline-flex items-center gap-1 hover:text-neutral-300">
            <Shield className="h-3 w-3" />
            Privacy
          </Link>
          <Link href="/contact" className="inline-flex items-center gap-1 hover:text-neutral-300">
            <Mail className="h-3 w-3" />
            Contact
          </Link>
          <a
            href={GITHUB_URL}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1 hover:text-neutral-300"
          >
            <Github className="h-3 w-3" />
            GitHub
          </a>
        </div>
      </footer>
    </div>
  );
}
