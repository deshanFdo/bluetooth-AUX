import React from "react";
import Link from "next/link";
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "About — WaveDock",
  description:
    "Architecture, security design, and cross-platform implementation behind WaveDock (formerly Audio Switch) for Windows 10/11 (.NET 8 WPF + Core Audio COM) and Ubuntu/Debian Linux (.NET 8 Avalonia + PipeWire).",
  alternates: { canonical: "/about" },
};

export default function AboutPage() {
  return (
    <main className="mx-auto min-h-screen max-w-3xl bg-[#050507] px-6 py-16 text-[#f4f4f6]">
      <Link href="/" className="font-mono text-xs text-neutral-400 hover:text-white">
        ← WaveDock
      </Link>
      <h1 className="mt-6 text-3xl font-semibold tracking-tight text-white">About WaveDock</h1>
      <p className="mt-4 text-sm leading-relaxed text-neutral-300">
        WaveDock (formerly Audio Switch) was created by Deshan Fernando to solve a daily annoyance
        across Windows and Linux laptops: switching rapidly between wired 3.5mm AUX headphones,
        Bluetooth headsets, laptop speakers, and microphones — or splitting music onto AUX while
        voice calls stay on Bluetooth — without digging through system settings.
      </p>
      <p className="mt-4 text-sm leading-relaxed text-neutral-400">
        On <strong className="font-medium text-neutral-200">Windows 10 / 11</strong> (.NET 8 WPF),
        WaveDock uses Win32 Core Audio COM (<code className="text-neutral-200">IMMDeviceEnumerator</code>,{" "}
        <code className="text-neutral-200">IPolicyConfig</code>, and <code className="text-neutral-200">AudioPolicyConfig</code>),
        user-mode WASAPI loopback with a 0–350 ms FIFO delay compensator, Windows SMTC + Hidden Tray
        hosting for Spotify &amp; YouTube Music, and PnP{" "}
        <code className="text-neutral-200">DEVPKEY_Device_ContainerId</code> Bluetooth battery lookup.
      </p>
      <p className="mt-4 text-sm leading-relaxed text-neutral-400">
        On <strong className="font-medium text-neutral-200">Ubuntu / Debian Linux</strong> (.NET 8
        Avalonia), it drives PipeWire / PulseAudio (<code className="text-neutral-200">pactl</code>,{" "}
        <code className="text-neutral-200">module-loopback</code>, and <code className="text-neutral-200">sink-inputs</code>),
        controls Spotify &amp; YouTube Music over <code className="text-neutral-200">MPRIS2</code>,
        and reads connected-only headset batteries via <code className="text-neutral-200">org.bluez.Battery1</code>{" "}
        and UPower.
      </p>
      <p className="mt-4 text-sm leading-relaxed text-neutral-400">
        Settings (<code className="text-neutral-200">settings.dat</code>) and installed binary seals
        (<code className="text-neutral-200">install.sig</code>) are protected with Windows DPAPI or
        Linux AES-256-GCM + read-only permissions, and everything removes cleanly via Installed Apps,{" "}
        <code className="text-neutral-200">sudo apt purge audioswitch</code>, or{" "}
        <code className="text-neutral-200">--uninstall</code>. Full source is published openly on{" "}
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
