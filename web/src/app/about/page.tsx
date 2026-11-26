import React from "react";
import Link from "next/link";
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "About — Audio Switch",
  description:
    "Architecture, design principles, and background behind Audio Switch, an open-source Windows 10/11 Dynamic Island audio router built with .NET 8, Core Audio COM, and WASAPI.",
  alternates: { canonical: "/about" },
};

export default function AboutPage() {
  return (
    <main className="mx-auto min-h-screen max-w-3xl bg-[#050507] px-6 py-16 text-[#f4f4f6]">
      <Link href="/" className="font-mono text-xs text-neutral-400 hover:text-white">
        ← Audio Switch
      </Link>
      <h1 className="mt-6 text-3xl font-semibold tracking-tight text-white">About Audio Switch</h1>
      <p className="mt-4 text-sm leading-relaxed text-neutral-300">
        Audio Switch was created by Deshan Fernando to solve a daily annoyance on Windows laptops:
        switching rapidly between wired 3.5mm AUX headphones, Bluetooth headsets, laptop speakers,
        and microphones without digging through Windows Sound Settings or leaving bloated tray
        utilities burning CPU in the background.
      </p>
      <p className="mt-4 text-sm leading-relaxed text-neutral-400">
        Built in native C# on .NET 8 WPF, Audio Switch interfaces directly with Win32 Core Audio COM
        interfaces (<code className="text-neutral-200">IMMDeviceEnumerator</code> and{" "}
        <code className="text-neutral-200">IPolicyConfig</code>) for sub-millisecond default
        device switching, pairs a user-mode WASAPI loopback buffer with adjustable 0–350 ms FIFO
        delay for simultaneous AUX + Bluetooth listening, reads Bluetooth headset battery metrics
        via Windows PnP HID/HFP keys, and binds to Windows SMTC sessions for Spotify and browser
        playback control.
      </p>
      <p className="mt-4 text-sm leading-relaxed text-neutral-400">
        At rest, background peak meters, media progress timers, and spring loops shut down
        completely — resulting in ~0% idle CPU usage (`0.047s` CPU over 6 seconds measured). Full
        source code is published openly on{" "}
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
