import React from "react";
import { ArrowDownToLine } from "lucide-react";
import DynamicIslandDemo from "@/components/DynamicIslandDemo";
import CopyShaButton from "@/components/CopyShaButton";

const INSTALLER_URL =
  "https://0hwpieezbnk7w7cc.public.blob.vercel-storage.com/v1.0.0/AudioSwitch-Setup.exe";
const PORTABLE_URL =
  "https://0hwpieezbnk7w7cc.public.blob.vercel-storage.com/v1.0.0/AudioSwitch.exe";
const SHA256_SETUP =
  "9CE219E878D6BE8DEE72C0E611B48E7325B38E1D89D0DFAF60E906DE1B0FFAB9";

export default function HomePage() {
  return (
    <div className="min-h-screen overflow-x-hidden bg-[#050507] text-[#f4f4f6]">
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
      </main>

      <footer className="border-t border-white/[0.07] py-6 text-center text-xs text-neutral-500">
        Audio Switch v1.0.0 · Built by Deshan Fernando
      </footer>
    </div>
  );
}
