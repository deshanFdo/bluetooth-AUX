"use client";

import React, { useState } from "react";
import { ArrowDownToLine, Check, Loader2 } from "lucide-react";

export default function DownloadButton({
  href,
  filename,
  label,
  sizeBadge,
}: {
  href: string;
  filename: string;
  label: string;
  sizeBadge: string;
}) {
  const [state, setState] = useState<"idle" | "starting" | "started">("idle");

  const handleClick = () => {
    setState("starting");
    setTimeout(() => setState("started"), 700);
    setTimeout(() => setState("idle"), 4000);
  };

  return (
    <a
      href={href}
      download={filename}
      onClick={handleClick}
      className="inline-flex items-center justify-center gap-2 rounded-full bg-white px-6 py-3 text-sm font-semibold text-black transition hover:bg-neutral-200"
    >
      {state === "idle" && <ArrowDownToLine className="h-4 w-4" />}
      {state === "starting" && <Loader2 className="h-4 w-4 animate-spin text-blue-600" />}
      {state === "started" && <Check className="h-4 w-4 text-emerald-600" />}
      <span>
        {state === "starting"
          ? "Starting download..."
          : state === "started"
          ? "Downloading..."
          : label}
      </span>
      <span className="font-mono text-xs font-normal text-neutral-600">{sizeBadge}</span>
    </a>
  );
}
