"use client";

import React, { useState } from "react";
import { Check, Copy } from "lucide-react";

export default function CopyShaButton({ value }: { value: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 1800);
    } catch {
      // Ignore clipboard failures
    }
  };

  return (
    <button
      onClick={handleCopy}
      className="inline-flex items-center gap-1.5 rounded-md border border-white/10 bg-white/[0.04] px-2.5 py-1 text-[11px] font-medium text-neutral-300 transition hover:bg-white/10 hover:text-white"
    >
      {copied ? (
        <>
          <Check className="h-3 w-3 text-emerald-400" />
          Copied
        </>
      ) : (
        <>
          <Copy className="h-3 w-3" />
          Copy SHA-256
        </>
      )}
    </button>
  );
}
