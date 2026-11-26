import React from "react";
import Link from "next/link";
import { ArrowDownToLine } from "lucide-react";
import DynamicIslandDemo from "@/components/DynamicIslandDemo";
import CopyShaButton from "@/components/CopyShaButton";

const INSTALLER_URL =
  "https://0hwpieezbnk7w7cc.public.blob.vercel-storage.com/v1.0.0/AudioSwitch-Setup.exe";
const PORTABLE_URL =
  "https://0hwpieezbnk7w7cc.public.blob.vercel-storage.com/v1.0.0/AudioSwitch.exe";
const GITHUB_URL = "https://github.com/deshanFdo/bluetooth-AUX";
const SHA256_SETUP =
  "9CE219E878D6BE8DEE72C0E611B48E7325B38E1D89D0DFAF60E906DE1B0FFAB9";

const FAQS = [
  {
    q: "How do I play audio on Bluetooth headphones and wired AUX at the same time in Windows 11 without delay?",
    a: "Audio Switch includes Multi-Output Mirroring backed by user-mode WASAPI loopback capture and a configurable 0–350 ms FIFO sync delay slider (170 ms default), delaying wired AUX output slightly so it lines up with wireless Bluetooth latency.",
  },
  {
    q: "Does Audio Switch require installing .NET Runtime or VB-Cable?",
    a: "No. AudioSwitch-Setup.exe (74.4 MB) bundles the .NET 8 desktop runtime and native WPF libraries into a single self-contained file and uses Windows Core Audio COM directly without virtual audio drivers.",
  },
  {
    q: "How much CPU does it use when minimized or gaming?",
    a: "Practically zero (~0.047s CPU over 6 seconds measured at rest). Peak meters, telemetry polling, and spring loops stop completely once the pill rests at the top edge.",
  },
  {
    q: "Can I see Bluetooth headphone battery percentage and control Spotify from the pill?",
    a: "Yes. Click and drag horizontally on the pill or expanded island to slide between Audio routing, Spotify/Chrome/Edge playback controls with live SMTC progress tracking, and circular Laptop + Bluetooth battery rings.",
  },
];

const JSON_LD = {
  "@context": "https://schema.org",
  "@graph": [
    {
      "@type": "SoftwareApplication",
      name: "Audio Switch",
      applicationCategory: "UtilitiesApplication",
      operatingSystem: "Windows 10, Windows 11 (x64)",
      softwareVersion: "1.0.0",
      fileSize: "74.4MB",
      downloadUrl: INSTALLER_URL,
      url: "https://audio-switch.vercel.app",
      sameAs: [GITHUB_URL],
      description:
        "iOS-style top-of-screen Dynamic Island for Windows 10/11 to switch AUX headphones, Bluetooth headsets, speakers, and microphones, mirror multi-output audio with FIFO sync delay, control Spotify/browser media, and monitor Bluetooth headset battery rings at ~0% idle CPU.",
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
        "Instant AUX wired headphone, Bluetooth headset, speaker, and microphone switching via Core Audio COM",
        "Simultaneous multi-output WASAPI mirroring with 0-350ms sync delay compensation",
        "Native Windows SMTC Spotify and browser media controls with interpolated progress bar",
        "Laptop and Bluetooth headset battery percentage rings",
        "System tray mode with ~0% idle CPU and Windows Settings uninstaller",
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
        dangerouslySetInnerHTML={{ __html: JSON.stringify(JSON_LD) }}
      />
      {/* Minimal Top Bar */}
      <header className="border-b border-white/[0.07]">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-2.5">
            <span className="h-2 w-2 rounded-full bg-emerald-400" />
            <span className="text-sm font-semibold tracking-tight text-white">Audio Switch</span>
            <span className="font-mono text-[11px] text-neutral-500">v1.0.0</span>
          </div>

          <div className="flex items-center gap-6 text-xs text-neutral-400">
            <a href="#how-it-works" className="hidden transition hover:text-white sm:inline">
              How it works
            </a>
            <a href="#install" className="hidden transition hover:text-white sm:inline">
              Install
            </a>
            <a
              href={GITHUB_URL}
              target="_blank"
              rel="noreferrer"
              className="hidden transition hover:text-white sm:inline"
            >
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
          <p className="font-mono text-xs tracking-wide text-neutral-400">
            WINDOWS 10 / 11 (x64) · SELF-CONTAINED · ~0% IDLE CPU
          </p>

          <h1 className="mt-4 text-4xl font-semibold tracking-[-0.035em] text-white sm:text-5xl md:text-[3.5rem] md:leading-[1.06]">
            The top-edge Dynamic Island for Windows audio.
          </h1>

          <p className="mx-auto mt-5 max-w-xl text-[15.5px] leading-relaxed text-neutral-400">
            Keep a barely-there indicator at the top of your monitor. Hover to expand a compact
            pill, click to switch AUX, Bluetooth headsets, speakers, and microphones — or slide
            into Spotify and battery rings.
          </p>

          <div className="mt-8 flex flex-col items-stretch justify-center gap-3 sm:flex-row sm:flex-wrap sm:items-center">
            <a
              href={INSTALLER_URL}
              download="AudioSwitch-Setup.exe"
              className="inline-flex items-center justify-center gap-2 rounded-full bg-white px-6 py-3 text-sm font-semibold text-black transition hover:bg-neutral-200"
            >
              <ArrowDownToLine className="h-4 w-4" />
              Download Installer
              <span className="font-mono text-xs font-normal text-neutral-600">74.4 MB</span>
            </a>

            <a
              href={PORTABLE_URL}
              download="AudioSwitch.exe"
              className="inline-flex items-center justify-center rounded-full border border-white/15 px-5 py-3 text-sm font-medium text-neutral-200 transition hover:border-white/30 hover:text-white"
            >
              Portable .exe
            </a>
          </div>

          <p className="mt-3 text-xs text-neutral-500">
            Runtime bundled — no extra .NET download needed. Includes Windows Apps uninstaller.
          </p>
        </section>

        {/* Live Desktop Stage */}
        <section className="mx-auto mt-12 max-w-3xl">
          <DynamicIslandDemo />
        </section>

        {/* Clean 3-Row Technical Overview (No generic card grid) */}
        <section id="how-it-works" className="mt-24 border-t border-white/[0.08] pt-14">
          <div className="grid gap-10 md:grid-cols-3">
            <div>
              <p className="font-mono text-[11px] text-neutral-500">01 / CORE AUDIO + MIRROR</p>
              <h2 className="mt-2 text-base font-semibold text-white">
                Instant switch &amp; sync delay
              </h2>
              <p className="mt-2 text-sm leading-relaxed text-neutral-400">
                Uses Windows Core Audio COM (<code className="text-neutral-300">IPolicyConfig</code>
                ) for sub-millisecond output &amp; mic switching, plus a user-mode WASAPI loopback
                buffer with 0–350 ms delay compensation to align Bluetooth and wired speakers.
              </p>
            </div>

            <div>
              <p className="font-mono text-[11px] text-neutral-500">02 / SLIDABLE ISLANDS</p>
              <h2 className="mt-2 text-base font-semibold text-white">
                Audio, Spotify, and Power rings
              </h2>
              <p className="mt-2 text-sm leading-relaxed text-neutral-400">
                Click and drag left or right across the pill or deck to slide between output
                controls, live Spotify / Chromium playback with SMTC timeline tracking, and laptop +
                Bluetooth battery gauges.
              </p>
            </div>

            <div>
              <p className="font-mono text-[11px] text-neutral-500">03 / SYSTEM TRAY &amp; IDLE</p>
              <h2 className="mt-2 text-base font-semibold text-white">
                Parks at ~0% CPU when collapsed
              </h2>
              <p className="mt-2 text-sm leading-relaxed text-neutral-400">
                Audio peak samplers, progress timers, and spring loops shut down completely once the
                pill returns to rest. Runs quietly from Windows Hidden Icons without taskbar clutter.
              </p>
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
              <h2 className="text-lg font-semibold text-white">Install &amp; SmartScreen</h2>
              <ol className="mt-4 space-y-3 text-sm text-neutral-300">
                <li>
                  <span className="font-mono text-xs text-neutral-500">1. </span>
                  Download{" "}
                  <a
                    href={INSTALLER_URL}
                    download="AudioSwitch-Setup.exe"
                    className="break-words underline decoration-white/30 underline-offset-4 hover:text-white"
                  >
                    AudioSwitch-Setup.exe
                  </a>
                  .
                </li>
                <li>
                  <span className="font-mono text-xs text-neutral-500">2. </span>
                  If Windows SmartScreen warns on an unsigned indie executable, choose{" "}
                  <strong className="font-medium text-white">More info → Run anyway</strong>.
                </li>
                <li>
                  <span className="font-mono text-xs text-neutral-500">3. </span>
                  Pick Start Menu / Desktop shortcuts and click{" "}
                  <strong className="font-medium text-white">Install Audio Switch</strong>. Uninstall
                  anytime from Windows <strong className="font-medium text-white">Installed Apps</strong>.
                </li>
              </ol>
            </div>

            <div className="flex min-w-0 flex-col justify-between gap-4 border-t border-white/[0.07] pt-6 md:border-l md:border-t-0 md:pl-8 md:pt-0">
              <dl className="grid grid-cols-1 gap-y-3 text-xs sm:grid-cols-[132px_minmax(0,1fr)] sm:gap-x-4">
                <dt className="text-neutral-500">Install directory</dt>
                <dd className="min-w-0 break-all font-mono text-neutral-200">
                  %LocalAppData%\Programs\AudioSwitch
                </dd>
                <dt className="text-neutral-500">Architecture</dt>
                <dd className="min-w-0 text-neutral-200">Windows 10 / 11 · x64</dd>
                <dt className="text-neutral-500">Measured idle CPU</dt>
                <dd className="min-w-0 font-mono text-neutral-200">0.047s / 6s resting</dd>
                <dt className="text-neutral-500">License / Cost</dt>
                <dd className="min-w-0 text-neutral-200">Free</dd>
              </dl>

              <div className="min-w-0 rounded-xl border border-white/[0.08] bg-black/50 p-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="font-mono text-[10px] uppercase tracking-wider text-neutral-500">
                    SHA-256 (AudioSwitch-Setup.exe)
                  </span>
                  <CopyShaButton value={SHA256_SETUP} />
                </div>
                <p className="mt-1.5 break-all font-mono text-[11px] text-neutral-300">
                  {SHA256_SETUP}
                </p>
              </div>
            </div>
          </div>
        </section>
        {/* Concise FAQ (AEO / GEO Structured Answers) */}
        <section id="faq" className="mt-16 border-t border-white/[0.08] pt-12">
          <h2 className="text-base font-semibold text-white">Frequently asked questions</h2>
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
        <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-center gap-x-4 gap-y-2 px-6">
          <span>Audio Switch v1.0.0 · Deshan Fernando</span>
          <Link href="/about" className="hover:text-neutral-300">
            About
          </Link>
          <Link href="/privacy" className="hover:text-neutral-300">
            Privacy
          </Link>
          <Link href="/contact" className="hover:text-neutral-300">
            Contact
          </Link>
          <a href={GITHUB_URL} target="_blank" rel="noreferrer" className="hover:text-neutral-300">
            GitHub
          </a>
          <a href="/llms.txt" className="hover:text-neutral-300">
            llms.txt
          </a>
        </div>
      </footer>
    </div>
  );
}
