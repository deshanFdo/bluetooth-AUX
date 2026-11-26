import React from "react";
import Link from "next/link";
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Privacy Policy — Audio Switch",
  description:
    "Audio Switch runs 100% locally on your Windows device with zero telemetry, zero analytics SDKs, and zero network accounts.",
  alternates: { canonical: "/privacy" },
};

export default function PrivacyPage() {
  return (
    <main className="mx-auto min-h-screen max-w-3xl bg-[#050507] px-6 py-16 text-[#f4f4f6]">
      <Link href="/" className="font-mono text-xs text-neutral-400 hover:text-white">
        ← Audio Switch
      </Link>
      <h1 className="mt-6 text-3xl font-semibold tracking-tight text-white">Privacy Policy</h1>
      <p className="mt-4 text-sm leading-relaxed text-neutral-300">
        Audio Switch operates strictly offline on your local Windows machine. The desktop app does
        not contain telemetry SDKs, analytics trackers, crash-upload beacons, ads, or user accounts,
        and it never records or transmits microphone input, system audio streams, or media playback
        history over the network.
      </p>
      <p className="mt-4 text-sm leading-relaxed text-neutral-400">
        User customization preferences (such as which islands appear, preferred sync delay, and
        auto-mute behavior) are saved locally in plain JSON at{" "}
        <code className="break-all text-neutral-200">%LocalAppData%\AudioSwitch\settings.json</code>
        . Audio mirroring reads local loopback PCM buffers purely in memory to route them to your
        checked secondary playback endpoint and discards frames immediately afterward.
      </p>
      <p className="mt-4 text-sm leading-relaxed text-neutral-400">
        This website (<code className="text-neutral-200">audio-switch.vercel.app</code>) serves
        static documentation and direct download links hosted on Vercel without tracking cookies.
        You can audit the full source code anytime on{" "}
        <a
          href="https://github.com/deshanFdo/bluetooth-AUX"
          target="_blank"
          rel="noreferrer"
          className="text-white underline decoration-white/30 underline-offset-4"
        >
          GitHub
        </a>
        .
      </p>
    </main>
  );
}
