import React from "react";
import Link from "next/link";
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Privacy & Security — WaveDock",
  description:
    "WaveDock runs 100% locally on Windows and Ubuntu/Debian Linux with encrypted local settings, zero telemetry, and clean removal.",
  alternates: { canonical: "/privacy" },
};

export default function PrivacyPage() {
  return (
    <main className="mx-auto min-h-screen max-w-3xl bg-[#050507] px-6 py-16 text-[#f4f4f6]">
      <Link href="/" className="font-mono text-xs text-neutral-400 hover:text-white">
        ← WaveDock
      </Link>
      <h1 className="mt-6 text-3xl font-semibold tracking-tight text-white">Privacy &amp; Local Encryption</h1>
      <p className="mt-4 text-sm leading-relaxed text-neutral-300">
        WaveDock operates strictly offline on your local Windows or Ubuntu / Debian Linux
        machine. The desktop app does not contain telemetry SDKs, analytics trackers, crash-upload
        beacons, ads, or user accounts, and it never records or transmits microphone input, system
        audio streams, or media playback history over the network.
      </p>
      <p className="mt-4 text-sm leading-relaxed text-neutral-400">
        User preferences and install verification seals are encrypted locally and unreadable as
        plaintext: on Windows,{" "}
        <code className="break-all text-neutral-200">%LocalAppData%\AudioSwitch\settings.dat</code>{" "}
        and <code className="text-neutral-200">install.sig</code> use{" "}
        <strong className="font-medium text-neutral-200">Windows DPAPI</strong> (
        <code className="text-neutral-200">CryptProtectData</code>, CurrentUser scope) with{" "}
        <code className="text-neutral-200">ReadOnly</code> protection on the installed binary; on
        Linux, <code className="break-all text-neutral-200">~/.config/AudioSwitch/settings.dat</code>{" "}
        and <code className="text-neutral-200">install.sig</code> use{" "}
        <strong className="font-medium text-neutral-200">AES-256-GCM</strong> authenticated
        encryption with an owner-only (<code className="text-neutral-200">0600</code>) key and{" "}
        <code className="text-neutral-200">0500</code> / <code className="text-neutral-200">root:root</code>{" "}
        binary permissions. Audio mirroring buffers PCM frames strictly in RAM and discards them immediately.
      </p>
      <p className="mt-4 text-sm leading-relaxed text-neutral-400">
        Desktop processes apply defense-in-depth hardening: Windows enforces{" "}
        <code className="text-neutral-200">DefaultDllImportSearchPaths(System32)</code> against DLL
        preloading, restricts single-instance wake IPC pipes via{" "}
        <code className="text-neutral-200">PipeSecurity</code> strictly to the current Windows user,
        and validates cleanup paths before execution. Linux uses positional{" "}
        <code className="text-neutral-200">ArgumentList</code> execution with zero shell string
        interpolation and regex-validated <code className="text-neutral-200">MPRIS2</code> D-Bus targets.
      </p>
      <p className="mt-4 text-sm leading-relaxed text-neutral-400">
        Because executables themselves must remain unencrypted on disk for Windows/Linux OS loaders
        to execute without Authenticode/PGP signing, every release publishes{" "}
        <strong className="font-medium text-neutral-200">SHA-256 checksums</strong> on the homepage
        and GitHub so you can verify downloaded installers before running them. Uninstalling via
        Windows Installed Apps, <code className="text-neutral-200">sudo apt purge audioswitch</code>,
        or <code className="text-neutral-200">--uninstall</code> deletes all application and config files.
      </p>
      <p className="mt-4 text-sm leading-relaxed text-neutral-400">
        This website (<code className="text-neutral-200">audio-switch.vercel.app</code>) serves
        static documentation and direct download links over HTTPS with HSTS preload, strict{" "}
        <code className="text-neutral-200">Content-Security-Policy</code>,{" "}
        <code className="text-neutral-200">X-Frame-Options: DENY</code>,{" "}
        <code className="text-neutral-200">X-Content-Type-Options: nosniff</code>, and COOP/CORP
        headers without tracking cookies. Full source code is auditable anytime on{" "}
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
