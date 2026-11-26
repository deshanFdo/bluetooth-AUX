"use client";

import React, { useEffect, useRef, useState } from "react";
import {
  BatteryCharging,
  Bluetooth,
  Cpu,
  Headphones,
  Layers,
  Mic,
  Pause,
  Play,
  SkipBack,
  SkipForward,
  Speaker,
  Volume2,
  Zap,
} from "lucide-react";

type IslandStage = "rest" | "pill" | "expanded";
type IslandTab = 0 | 1 | 2;

export default function DynamicIslandDemo() {
  const [stage, setStage] = useState<IslandStage>("pill");
  const [activeTab, setActiveTab] = useState<IslandTab>(0);
  const [route, setRoute] = useState<"aux" | "bt" | "speakers">("bt");
  const [mirroring, setMirroring] = useState(false);
  const [auxVol, setAuxVol] = useState(72);
  const [btVol, setBtVol] = useState(64);
  const [syncDelay, setSyncDelay] = useState(170);
  const [isPlaying, setIsPlaying] = useState(true);
  const [progress, setProgress] = useState(98); // seconds
  const duration = 214; // 3:34

  // Interactive click-and-drag carousel offset (mirrors the real WPF app)
  const dragStartX = useRef<number | null>(null);
  const [dragOffset, setDragOffset] = useState(0);

  useEffect(() => {
    if (!isPlaying) return;
    const id = setInterval(() => {
      setProgress((prev) => (prev + 1 >= duration ? 0 : prev + 1));
    }, 1000);
    return () => clearInterval(id);
  }, [isPlaying]);

  const formatTime = (sec: number) => {
    const m = Math.floor(sec / 60);
    const s = sec % 60;
    return `${m}:${s < 10 ? "0" : ""}${s}`;
  };

  const cycleTab = (delta: number) => {
    setActiveTab((prev) => (((prev + delta) % 3 + 3) % 3) as IslandTab);
  };

  const handlePointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    const target = e.target as HTMLElement;
    if (target.closest("button, input, label")) return;
    dragStartX.current = e.clientX;
    e.currentTarget.setPointerCapture(e.pointerId);
  };

  const handlePointerMove = (e: React.PointerEvent<HTMLDivElement>) => {
    if (dragStartX.current === null) return;
    const dx = e.clientX - dragStartX.current;
    setDragOffset(Math.max(-85, Math.min(85, dx * 0.78)));
  };

  const handlePointerUp = (e: React.PointerEvent<HTMLDivElement>) => {
    if (dragStartX.current === null) return;
    const dx = e.clientX - dragStartX.current;
    dragStartX.current = null;
    if (e.currentTarget.hasPointerCapture(e.pointerId)) {
      e.currentTarget.releasePointerCapture(e.pointerId);
    }
    if (Math.abs(dx) >= 28) {
      cycleTab(dx < 0 ? 1 : -1);
    } else if (stage === "pill" && Math.abs(dx) < 8) {
      setStage("expanded");
    }
    setDragOffset(0);
  };

  const accentHex =
    activeTab === 1
      ? "#22C55E"
      : activeTab === 2
      ? "#10B981"
      : mirroring
      ? "#06B6D4"
      : route === "aux"
      ? "#10B981"
      : route === "bt"
      ? "#3B82F6"
      : "#F59E0B";

  const activeRouteLabel = mirroring
    ? "AUX + JBL Tune 720BT"
    : route === "aux"
    ? "AUX Headphones"
    : route === "bt"
    ? "JBL Tune 720BT"
    : "Laptop Speakers";

  return (
    <div className="relative mx-auto w-full max-w-[440px]">
      {/* Ambient Radial Glow matching active Island/route */}
      <div
        className="pointer-events-none absolute -inset-6 rounded-full opacity-35 blur-3xl transition-colors duration-500"
        style={{ background: `radial-gradient(circle, ${accentHex}44 0%, transparent 70%)` }}
      />

      {/* Simulated Desktop Wallpaper Viewport */}
      <div className="relative overflow-hidden rounded-[26px] border border-white/10 bg-[#070a12]/90 p-4 pt-3 shadow-2xl">
        {/* Top Desktop Status Hint */}
        <div className="mb-3 flex items-center justify-between text-[11px] text-slate-400">
          <span>Interactive Preview • Hover, Click, or Drag Left/Right</span>
          <div className="flex gap-1.5">
            {(["rest", "pill", "expanded"] as IslandStage[]).map((s) => (
              <button
                key={s}
                onClick={() => setStage(s)}
                className={`rounded-md px-2 py-0.5 text-[10px] font-medium capitalize transition ${
                  stage === s
                    ? "bg-blue-600/80 text-white"
                    : "bg-white/5 text-slate-400 hover:bg-white/10 hover:text-white"
                }`}
              >
                {s === "rest" ? "Idle" : s === "pill" ? "Hover" : "Open"}
              </button>
            ))}
          </div>
        </div>

        {/* Dynamic Island Frame */}
        <div className="flex min-h-[348px] flex-col items-center justify-start pt-1">
          <div
            onMouseEnter={() => {
              if (stage === "rest") setStage("pill");
            }}
            onPointerDown={handlePointerDown}
            onPointerMove={handlePointerMove}
            onPointerUp={handlePointerUp}
            className="obsidian-glass relative select-none overflow-hidden transition-all duration-300 ease-out"
            style={{
              width: stage === "rest" ? 112 : stage === "pill" ? 284 : 348,
              borderRadius: stage === "rest" ? 4 : stage === "pill" ? 20 : 24,
              minHeight: stage === "rest" ? 6 : stage === "pill" ? 40 : 288,
              cursor: stage === "expanded" ? "grab" : "pointer",
              border:
                stage === "rest"
                  ? "1px solid transparent"
                  : `1px solid ${accentHex}55`,
              boxShadow:
                stage === "rest"
                  ? `0 0 14px ${accentHex}88`
                  : `inset 0 1px 0 rgba(255,255,255,0.24), 0 22px 48px -12px rgba(0,0,0,0.85)`,
              background:
                stage === "rest"
                  ? `linear-gradient(90deg, rgba(255,255,255,0.2), ${accentHex}, rgba(255,255,255,0.2))`
                  : undefined,
            }}
          >
            {/* STAGE 1: COMPACT HOVER PILL */}
            {stage === "pill" && (
              <div
                className="flex h-[40px] items-center justify-between px-3 transition-transform duration-150"
                style={{
                  transform: `translateX(${dragOffset}px)`,
                  opacity: Math.max(0.45, 1 - Math.abs(dragOffset) / 110),
                }}
              >
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    cycleTab(-1);
                  }}
                  className="flex h-6 w-5 items-center justify-center rounded text-slate-400 hover:bg-white/10 hover:text-white"
                  aria-label="Previous Island"
                >
                  ‹
                </button>

                {activeTab === 0 && (
                  <div className="flex flex-1 items-center justify-between px-1.5">
                    <div className="flex items-center gap-2 overflow-hidden">
                      <div
                        className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full border"
                        style={{
                          background: `${accentHex}26`,
                          borderColor: `${accentHex}66`,
                        }}
                      >
                        {route === "aux" ? (
                          <Headphones className="h-3.5 w-3.5 text-emerald-400" />
                        ) : route === "bt" ? (
                          <Bluetooth className="h-3.5 w-3.5 text-blue-400" />
                        ) : (
                          <Speaker className="h-3.5 w-3.5 text-amber-400" />
                        )}
                      </div>
                      <div className="min-w-0">
                        <p className="truncate text-[11.5px] font-semibold leading-tight text-slate-50">
                          {activeRouteLabel}
                        </p>
                        <p className="truncate text-[9.2px] leading-tight text-slate-400">
                          Mic ready • Drag to slide
                        </p>
                      </div>
                    </div>
                    <div className="flex h-3.5 items-end gap-[2.5px]">
                      <span className="animate-eq-1 h-full w-[3px] origin-bottom rounded-full bg-blue-400" />
                      <span className="animate-eq-2 h-full w-[3px] origin-bottom rounded-full bg-emerald-400" />
                      <span className="animate-eq-3 h-full w-[3px] origin-bottom rounded-full bg-purple-400" />
                    </div>
                  </div>
                )}

                {activeTab === 1 && (
                  <div className="flex flex-1 flex-col justify-center px-1.5">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2 overflow-hidden">
                        <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full border border-emerald-400/50 bg-emerald-500/20 text-[11px] font-bold text-emerald-400">
                          ♪
                        </div>
                        <div className="min-w-0">
                          <p className="truncate text-[11px] font-semibold leading-tight text-white">
                            Midnight City
                          </p>
                          <p className="truncate text-[9px] leading-tight text-slate-400">
                            M83 • Spotify
                          </p>
                        </div>
                      </div>
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          setIsPlaying((p) => !p);
                        }}
                        className="flex h-6 w-6 items-center justify-center rounded-full bg-white/15 text-white hover:bg-white/25"
                        aria-label="Toggle playback"
                      >
                        {isPlaying ? <Pause className="h-3 w-3" /> : <Play className="h-3 w-3" />}
                      </button>
                    </div>
                    <div className="mt-1 h-[2px] w-full overflow-hidden rounded-full bg-white/15">
                      <div
                        className="h-full rounded-full bg-emerald-400 transition-all duration-500"
                        style={{ width: `${(progress / duration) * 100}%` }}
                      />
                    </div>
                  </div>
                )}

                {activeTab === 2 && (
                  <div className="flex flex-1 items-center justify-center gap-3 px-1">
                    <div className="flex items-center gap-1.5">
                      <span className="flex h-6 w-6 items-center justify-center rounded-full border-2 border-emerald-400 text-[9px] font-bold text-emerald-300 tabular-nums">
                        ⚡
                      </span>
                      <span className="text-[11px] font-semibold text-white tabular-nums">92%</span>
                    </div>
                    <div className="flex items-center gap-1.5">
                      <span className="flex h-6 w-6 items-center justify-center rounded-full border-2 border-blue-400 text-[9px] font-bold text-blue-300">
                        <Headphones className="h-3 w-3" />
                      </span>
                      <span className="text-[11px] font-semibold text-blue-300 tabular-nums">90%</span>
                    </div>
                  </div>
                )}

                <div className="flex items-center gap-1">
                  <div className="flex gap-1">
                    {[0, 1, 2].map((idx) => (
                      <span
                        key={idx}
                        className={`h-1.5 w-1.5 rounded-full transition ${
                          activeTab === idx ? "bg-white" : "bg-white/30"
                        }`}
                      />
                    ))}
                  </div>
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      cycleTab(1);
                    }}
                    className="flex h-6 w-5 items-center justify-center rounded text-slate-400 hover:bg-white/10 hover:text-white"
                    aria-label="Next Island"
                  >
                    ›
                  </button>
                </div>
              </div>
            )}

            {/* STAGE 2: EXPANDED INTERACTIVE 3-ISLAND DECK */}
            {stage === "expanded" && (
              <div className="p-3">
                {/* Header */}
                <div className="mb-2 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <div
                      className="flex h-6 w-6 items-center justify-center rounded-full border"
                      style={{
                        background: `${accentHex}2a`,
                        borderColor: `${accentHex}66`,
                      }}
                    >
                      {activeTab === 0 ? (
                        <Headphones className="h-3 w-3 text-blue-400" />
                      ) : activeTab === 1 ? (
                        <Volume2 className="h-3 w-3 text-emerald-400" />
                      ) : (
                        <Zap className="h-3 w-3 text-emerald-400" />
                      )}
                    </div>
                    <div>
                      <p className="text-[12.5px] font-bold leading-tight text-white">
                        {activeTab === 0
                          ? "Audio Island"
                          : activeTab === 1
                          ? "Spotify"
                          : "Battery & System"}
                      </p>
                      <p className="text-[9.5px] text-slate-400">
                        {mirroring ? "Mirroring + Sync Delay" : activeRouteLabel}
                      </p>
                    </div>
                  </div>
                  <button
                    onClick={() => setStage("pill")}
                    className="rounded-lg bg-white/5 px-2 py-1 text-[10px] font-medium text-slate-300 hover:bg-white/15 hover:text-white"
                  >
                    ⌃ Minimize
                  </button>
                </div>

                {/* 3-Island Switcher Strip */}
                <div className="mb-2 grid grid-cols-3 gap-1 rounded-[10px] border border-white/10 bg-[#080b12]/70 p-0.5">
                  {[
                    { id: 0 as IslandTab, label: "Audio" },
                    { id: 1 as IslandTab, label: "Spotify" },
                    { id: 2 as IslandTab, label: "Battery" },
                  ].map((tab) => (
                    <button
                      key={tab.id}
                      onClick={() => setActiveTab(tab.id)}
                      className={`h-6 rounded-[7px] text-[10.5px] font-semibold transition ${
                        activeTab === tab.id
                          ? "bg-blue-600 text-white shadow-sm"
                          : "text-slate-400 hover:text-slate-200"
                      }`}
                    >
                      {tab.label}
                    </button>
                  ))}
                </div>

                {/* Draggable Carousel Content */}
                <div
                  className="transition-transform duration-150"
                  style={{
                    transform: `translateX(${dragOffset}px)`,
                    opacity: Math.max(0.42, 1 - Math.abs(dragOffset) / 145),
                  }}
                >
                  {activeTab === 0 && (
                    <div className="space-y-1.5">
                      {/* AUX Card */}
                      <div
                        className={`rounded-[11px] border p-2 transition ${
                          route === "aux"
                            ? "border-emerald-500/70 bg-[#101c28]/70"
                            : "border-white/10 bg-[#141c2b]/50"
                        }`}
                      >
                        <div className="flex items-center justify-between">
                          <div className="flex items-center gap-2">
                            <span className="flex h-7 w-7 items-center justify-center rounded-full border border-emerald-500/40 bg-emerald-500/20">
                              <Headphones className="h-3.5 w-3.5 text-emerald-400" />
                            </span>
                            <div>
                              <div className="flex items-center gap-1.5">
                                <span className="text-[11.5px] font-semibold text-white">AUX</span>
                                {route === "aux" && (
                                  <span className="rounded bg-emerald-950 px-1.5 py-0.5 text-[8px] font-bold text-emerald-400">
                                    ON
                                  </span>
                                )}
                              </div>
                              <p className="text-[9.8px] text-slate-300">Realtek HD Audio</p>
                            </div>
                          </div>
                          <button
                            onClick={() => setRoute("aux")}
                            aria-label="Switch to AUX"
                            className={`relative h-[22px] w-[40px] rounded-full border transition ${
                              route === "aux"
                                ? "border-blue-400 bg-blue-600"
                                : "border-slate-600 bg-slate-800"
                            }`}
                          >
                            <span
                              className={`absolute top-[2px] h-4 w-4 rounded-full bg-white transition-all ${
                                route === "aux" ? "left-[20px]" : "left-[2px]"
                              }`}
                            />
                          </button>
                        </div>
                        {(route === "aux" || mirroring) && (
                          <div className="mt-1.5 flex items-center gap-2">
                            <span className="text-[9.5px] text-slate-400">Vol</span>
                            <input
                              type="range"
                              min={0}
                              max={100}
                              value={auxVol}
                              onChange={(e) => setAuxVol(Number(e.target.value))}
                              className="h-1 flex-1 accent-emerald-400"
                            />
                            <span className="w-7 text-right text-[10px] text-slate-300 tabular-nums">
                              {auxVol}%
                            </span>
                          </div>
                        )}
                      </div>

                      {/* Bluetooth Card */}
                      <div
                        className={`rounded-[11px] border p-2 transition ${
                          route === "bt"
                            ? "border-blue-500/70 bg-[#101c28]/70"
                            : "border-white/10 bg-[#141c2b]/50"
                        }`}
                      >
                        <div className="flex items-center justify-between">
                          <div className="flex items-center gap-2">
                            <span className="flex h-7 w-7 items-center justify-center rounded-full border border-blue-500/40 bg-blue-500/20">
                              <Bluetooth className="h-3.5 w-3.5 text-blue-400" />
                            </span>
                            <div>
                              <div className="flex items-center gap-1.5">
                                <span className="text-[11.5px] font-semibold text-white">Bluetooth</span>
                                {route === "bt" && (
                                  <span className="rounded bg-blue-950 px-1.5 py-0.5 text-[8px] font-bold text-blue-400">
                                    ON
                                  </span>
                                )}
                              </div>
                              <p className="text-[9.8px] text-slate-300">JBL Tune 720BT • 90%</p>
                            </div>
                          </div>
                          <button
                            onClick={() => setRoute("bt")}
                            aria-label="Switch to Bluetooth"
                            className={`relative h-[22px] w-[40px] rounded-full border transition ${
                              route === "bt"
                                ? "border-blue-400 bg-blue-600"
                                : "border-slate-600 bg-slate-800"
                            }`}
                          >
                            <span
                              className={`absolute top-[2px] h-4 w-4 rounded-full bg-white transition-all ${
                                route === "bt" ? "left-[20px]" : "left-[2px]"
                              }`}
                            />
                          </button>
                        </div>
                        {(route === "bt" || mirroring) && (
                          <div className="mt-1.5 flex items-center gap-2">
                            <span className="text-[9.5px] text-slate-400">Vol</span>
                            <input
                              type="range"
                              min={0}
                              max={100}
                              value={btVol}
                              onChange={(e) => setBtVol(Number(e.target.value))}
                              className="h-1 flex-1 accent-blue-400"
                            />
                            <span className="w-7 text-right text-[10px] text-slate-300 tabular-nums">
                              {btVol}%
                            </span>
                          </div>
                        )}
                      </div>

                      {/* Multi-Output Mirror Card */}
                      <div className="rounded-[11px] border border-white/10 bg-[#121b2d]/60 p-2">
                        <div className="flex items-center justify-between">
                          <div className="flex items-center gap-1.5">
                            <Layers className="h-3 w-3 text-cyan-400" />
                            <span className="text-[11px] font-semibold text-white">
                              Multi-Output Mirror
                            </span>
                            {mirroring && (
                              <span className="rounded bg-cyan-950 px-1.5 py-0.5 text-[8px] font-bold text-cyan-400">
                                MIRROR
                              </span>
                            )}
                          </div>
                          <button
                            onClick={() => setMirroring((m) => !m)}
                            className={`rounded-md border px-2 py-0.5 text-[9.5px] font-semibold transition ${
                              mirroring
                                ? "border-cyan-400/50 bg-cyan-500/20 text-cyan-300"
                                : "border-white/15 bg-white/5 text-slate-300 hover:bg-white/10"
                            }`}
                          >
                            {mirroring ? "Single Mode" : "Sync AUX + BT"}
                          </button>
                        </div>
                        {mirroring && (
                          <div className="mt-1.5 flex items-center gap-2">
                            <span className="text-[9.5px] text-cyan-300">Delay</span>
                            <input
                              type="range"
                              min={0}
                              max={350}
                              value={syncDelay}
                              onChange={(e) => setSyncDelay(Number(e.target.value))}
                              className="h-1 flex-1 accent-cyan-400"
                            />
                            <span className="w-11 text-right text-[9.5px] text-slate-200 tabular-nums">
                              {syncDelay} ms
                            </span>
                          </div>
                        )}
                      </div>

                      <div className="flex items-center justify-between rounded-[9px] border border-white/10 bg-[#090d16]/70 px-2.5 py-1.5 text-[9.5px] text-blue-300">
                        <span>Drag card left/right to slide islands</span>
                        <span className="flex items-center gap-1 text-slate-400">
                          <Mic className="h-3 w-3" /> Mics ready
                        </span>
                      </div>
                    </div>
                  )}

                  {activeTab === 1 && (
                    <div className="space-y-2">
                      <div className="rounded-[12px] border border-white/10 bg-[#0d131f]/80 p-3">
                        <div className="mb-2.5 flex items-center gap-3">
                          <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-[9px] border border-emerald-400/40 bg-gradient-to-br from-emerald-500/35 to-blue-600/30 text-xl font-bold text-emerald-300">
                            ♪
                          </div>
                          <div className="min-w-0 flex-1">
                            <div className="mb-0.5 flex items-center gap-1.5">
                              <span className="rounded bg-emerald-950 px-1.5 py-0.5 text-[8.5px] font-bold text-emerald-400">
                                Spotify
                              </span>
                              <span className="text-[9.5px] text-slate-400">
                                {isPlaying ? "Now Playing" : "Paused"}
                              </span>
                            </div>
                            <p className="truncate text-[13px] font-bold text-white">Midnight City</p>
                            <p className="truncate text-[10.5px] text-slate-300">M83 • Hurry Up, We&apos;re Dreaming</p>
                          </div>
                        </div>

                        <div className="mb-2.5">
                          <div className="h-1 w-full overflow-hidden rounded-full bg-white/15">
                            <div
                              className="h-full rounded-full bg-gradient-to-r from-emerald-500 to-emerald-400 transition-all duration-500"
                              style={{ width: `${(progress / duration) * 100}%` }}
                            />
                          </div>
                          <div className="mt-1 flex justify-between text-[9.2px] text-slate-400 tabular-nums">
                            <span>{formatTime(progress)}</span>
                            <span>{formatTime(duration)}</span>
                          </div>
                        </div>

                        <div className="flex items-center justify-center gap-3">
                          <button
                            onClick={() => setProgress(0)}
                            className="flex h-8 w-8 items-center justify-center rounded-full border border-white/15 bg-white/5 text-slate-200 hover:bg-white/15"
                            aria-label="Previous"
                          >
                            <SkipBack className="h-3.5 w-3.5" />
                          </button>
                          <button
                            onClick={() => setIsPlaying((p) => !p)}
                            className="flex h-10 w-10 items-center justify-center rounded-full bg-white text-slate-950 shadow hover:scale-105"
                            aria-label="Play or pause"
                          >
                            {isPlaying ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}
                          </button>
                          <button
                            onClick={() => setProgress(32)}
                            className="flex h-8 w-8 items-center justify-center rounded-full border border-white/15 bg-white/5 text-slate-200 hover:bg-white/15"
                            aria-label="Next"
                          >
                            <SkipForward className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      </div>

                      <div className="flex items-center justify-between rounded-[11px] border border-white/10 bg-[#0b101b]/70 px-3 py-2">
                        <div>
                          <p className="text-[9px] text-slate-400">Listening On</p>
                          <p className="text-[11px] font-semibold text-white">{activeRouteLabel}</p>
                        </div>
                        <button
                          onClick={() => setRoute((r) => (r === "aux" ? "bt" : "aux"))}
                          className="rounded-lg border border-white/15 bg-white/10 px-2.5 py-1 text-[10px] font-medium text-white hover:bg-white/20"
                        >
                          Swap ⇄
                        </button>
                      </div>
                    </div>
                  )}

                  {activeTab === 2 && (
                    <div className="space-y-2">
                      <div className="rounded-[12px] border border-white/10 bg-[#0d131f]/80 p-3">
                        <p className="mb-2 text-[10.5px] font-semibold text-slate-300">
                          Devices &amp; Power
                        </p>
                        <div className="flex items-center justify-around">
                          <div className="flex flex-col items-center">
                            <div className="flex h-14 w-14 flex-col items-center justify-center rounded-full border-[4px] border-emerald-400 bg-emerald-500/10">
                              <BatteryCharging className="h-3 w-3 text-emerald-400" />
                              <span className="text-[11px] font-bold text-white tabular-nums">92%</span>
                            </div>
                            <span className="mt-1 text-[10px] font-semibold text-white">Laptop</span>
                            <span className="text-[9px] text-slate-400">Charging</span>
                          </div>
                          <div className="flex flex-col items-center">
                            <div className="flex h-14 w-14 flex-col items-center justify-center rounded-full border-[4px] border-blue-400 bg-blue-500/10">
                              <Headphones className="h-3 w-3 text-blue-400" />
                              <span className="text-[11px] font-bold text-white tabular-nums">90%</span>
                            </div>
                            <span className="mt-1 text-[10px] font-semibold text-white">JBL Tune</span>
                            <span className="text-[9px] text-blue-300">Bluetooth</span>
                          </div>
                        </div>
                      </div>

                      <div className="flex items-center justify-between rounded-[12px] border border-white/10 bg-[#0e1422]/80 px-3 py-2">
                        <div className="flex gap-3">
                          <div className="flex flex-col items-center">
                            <div className="flex h-12 w-12 flex-col items-center justify-center rounded-full border-[3.5px] border-purple-400 bg-purple-500/10">
                              <Cpu className="h-2.5 w-2.5 text-purple-300" />
                              <span className="text-[10px] font-bold text-white tabular-nums">41%</span>
                            </div>
                            <span className="mt-0.5 text-[9px] text-slate-300">RAM</span>
                          </div>
                          <div className="flex flex-col items-center">
                            <div className="flex h-12 w-12 flex-col items-center justify-center rounded-full border-[3.5px] border-cyan-400 bg-cyan-500/10">
                              <Zap className="h-2.5 w-2.5 text-cyan-300" />
                              <span className="text-[10px] font-bold text-white tabular-nums">0%</span>
                            </div>
                            <span className="mt-0.5 text-[9px] text-slate-300">App CPU</span>
                          </div>
                        </div>
                        <div className="text-right">
                          <p className="text-base font-bold text-white tabular-nums">12:08 PM</p>
                          <p className="text-[9.5px] text-slate-400">Windows 11</p>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
