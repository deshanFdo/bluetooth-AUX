import React from "react";
import Link from "next/link";
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Contact & Support — WaveDock",
  description:
    "Contact WaveDock maintainer Deshan Fernando, report Windows or Linux audio bugs, or open feature requests on GitHub.",
  alternates: { canonical: "/contact" },
};

export default function ContactPage() {
  return (
    <main className="mx-auto min-h-screen max-w-3xl bg-[#050507] px-6 py-16 text-[#f4f4f6]">
      <Link href="/" className="font-mono text-xs text-neutral-400 hover:text-white">
        ← WaveDock
      </Link>
      <h1 className="mt-6 text-3xl font-semibold tracking-tight text-white">Contact &amp; Support</h1>
      <p className="mt-4 text-sm leading-relaxed text-neutral-300">
        WaveDock is maintained by Deshan Fernando. For bug reports, Bluetooth headset battery
        compatibility logs, multi-output sync delay feedback, or feature requests, reach out using
        any of the channels below.
      </p>
      <div className="mt-6 space-y-4 rounded-xl border border-white/10 bg-[#0a0c10] p-5 text-sm">
        <div>
          <p className="font-mono text-xs text-neutral-500">GitHub Issues &amp; Source</p>
          <a
            href="https://github.com/deshanFdo/bluetooth-AUX/issues"
            target="_blank"
            rel="noreferrer"
            className="text-white underline decoration-white/30 underline-offset-4"
          >
            github.com/deshanFdo/bluetooth-AUX/issues
          </a>
        </div>
        <div>
          <p className="font-mono text-xs text-neutral-500">Maintainer Email</p>
          <a
            href="mailto:deshanfernando67@gmail.com"
            className="text-white underline decoration-white/30 underline-offset-4"
          >
            deshanfernando67@gmail.com
          </a>
        </div>
        <div>
          <p className="font-mono text-xs text-neutral-500">Diagnostic Output Commands</p>
          <p className="mt-1 font-mono text-xs text-neutral-300">
            Windows: AudioSwitch.exe --diag · Linux: AudioSwitch --diag
          </p>
          <p className="mt-1 text-xs text-neutral-400">
            Include output from <code className="text-neutral-200">--diag</code> when reporting
            device detection or Bluetooth battery read issues.
          </p>
        </div>
      </div>
    </main>
  );
}
