"use client";

import React, { useEffect, useRef, useState } from "react";
import {
  Bluetooth,
  Headphones,
  Pause,
  Play,
  SkipBack,
  SkipForward,
  Speaker,
  Volume2,
} from "lucide-react";

type Stage = "rest" | "pill" | "expanded";
type Page = 0 | 1 | 2;
type AudioSubTab = "outputs" | "mics" | "apps";

export default function DynamicIslandDemo() {
  const [stage, setStage] = useState<Stage>("pill");
  const [page, setPage] = useState<Page>(0);
  const [audioTab, setAudioTab] = useState<AudioSubTab>("outputs");
  const [route, setRoute] = useState<"aux" | "bt" | "speakers">("bt");
  const [micRoute, setMicRoute] = useState<"bt" | "laptop" | "aux">("bt");
  const [spotifyOut, setSpotifyOut] = useState<"default" | "aux" | "bt">("aux");
  const [discordOut, setDiscordOut] = useState<"default" | "aux" | "bt">("bt");
  const [spotifyInTray, setSpotifyInTray] = useState(true);
  const [mirror, setMirror] = useState(false);
  const [vol, setVol] = useState(68);
  const [delayMs, setDelayMs] = useState(170);
  const [playing, setPlaying] = useState(true);
  const [sec, setSec] = useState(84);
  const totalSec = 214;

  const dragStart = useRef<number | null>(null);
  const [dragX, setDragX] = useState(0);

  useEffect(() => {
    if (!playing) return;
    const t = setInterval(() => {
      setSec((s) => (s + 1 >= totalSec ? 0 : s + 1));
    }, 1000);
    return () => clearInterval(t);
  }, [playing]);

  const fmt = (s: number) => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;

  const shiftPage = (d: number) => {
    setPage((p) => (((p + d) % 3 + 3) % 3) as Page);
  };

  const onDown = (e: React.PointerEvent<HTMLDivElement>) => {
    if ((e.target as HTMLElement).closest("button, input, select")) return;
    dragStart.current = e.clientX;
    e.currentTarget.setPointerCapture(e.pointerId);
  };

  const onMove = (e: React.PointerEvent<HTMLDivElement>) => {
    if (dragStart.current === null) return;
    const dx = e.clientX - dragStart.current;
    setDragX(Math.max(-80, Math.min(80, dx * 0.78)));
  };

  const onUp = (e: React.PointerEvent<HTMLDivElement>) => {
    if (dragStart.current === null) return;
    const dx = e.clientX - dragStart.current;
    dragStart.current = null;
    if (e.currentTarget.hasPointerCapture(e.pointerId)) {
      e.currentTarget.releasePointerCapture(e.pointerId);
    }
    if (Math.abs(dx) >= 26) {
      shiftPage(dx < 0 ? 1 : -1);
    } else if (stage === "pill" && Math.abs(dx) < 6) {
      setStage("expanded");
    }
    setDragX(0);
  };

  const routeName = mirror
    ? "AUX + JBL Tune 720BT"
    : route === "aux"
    ? "AUX Headphones"
    : route === "bt"
    ? "JBL Tune 720BT"
    : "Laptop Speakers";

  const accentColor =
    page === 1
      ? "#22c55e"
      : page === 2
      ? "#10b981"
      : mirror
      ? "#22d3ee"
      : route === "aux"
      ? "#34d399"
      : route === "bt"
      ? "#60a5fa"
      : "#fbbf24";

  return (
    <div className="w-full">
      {/* Desktop Display Frame */}
      <div className="relative mx-auto overflow-hidden rounded-2xl border border-white/[0.11] bg-[#0b0d12] shadow-[0_32px_90px_-20px_rgba(0,0,0,0.95)]">
        {/* Top subtle wallpaper backdrop mimicking a clean Windows 11 desktop */}
        <div
          className="relative flex min-h-[390px] flex-col justify-between px-3 pb-3 pt-2 sm:px-4"
          style={{
            background:
              "radial-gradient(120% 90% at 50% 0%, rgba(30, 41, 59, 0.38) 0%, rgba(11, 13, 18, 0.98) 72%)",
          }}
        >
          {/* Floating Dynamic Island anchored top-center */}
          <div className="flex w-full flex-col items-center">
            <div
              onMouseEnter={() => {
                if (stage === "rest") setStage("pill");
              }}
              onPointerDown={onDown}
              onPointerMove={onMove}
              onPointerUp={onUp}
              className="island-obsidian relative max-w-full select-none overflow-hidden transition-all duration-300 ease-out"
              style={{
                width:
                  stage === "rest"
                    ? "104px"
                    : stage === "pill"
                    ? "min(100%, 282px)"
                    : "min(100%, 348px)",
                minHeight: stage === "rest" ? 4 : stage === "pill" ? 40 : 262,
                borderRadius: stage === "rest" ? 2 : stage === "pill" ? 20 : 24,
                marginTop: stage === "rest" ? 0 : 4,
                cursor: stage === "expanded" ? "grab" : "pointer",
                border:
                  stage === "rest"
                    ? "0px solid transparent"
                    : "1px solid rgba(255,255,255,0.14)",
                boxShadow:
                  stage === "rest"
                    ? `0 0 10px ${accentColor}99`
                    : "inset 0 1px 0 rgba(255,255,255,0.18), 0 24px 48px -12px rgba(0,0,0,0.85)",
                background:
                  stage === "rest"
                    ? `linear-gradient(90deg, rgba(255,255,255,0.18), ${accentColor}, rgba(255,255,255,0.18))`
                    : undefined,
              }}
            >
              {/* COMPACT HOVER PILL */}
              {stage === "pill" && (
                <div
                  className="flex h-[40px] items-center justify-between px-2.5 transition-transform duration-150"
                  style={{
                    transform: `translateX(${dragX}px)`,
                    opacity: Math.max(0.45, 1 - Math.abs(dragX) / 110),
                  }}
                >
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      shiftPage(-1);
                    }}
                    className="flex h-6 w-5 items-center justify-center rounded text-neutral-400 hover:bg-white/10 hover:text-white"
                    aria-label="Previous"
                  >
                    ‹
                  </button>

                  {page === 0 && (
                    <div className="flex flex-1 items-center justify-between px-1.5">
                      <div className="flex items-center gap-2 overflow-hidden">
                        <span
                          className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full border"
                          style={{
                            borderColor: `${accentColor}55`,
                            backgroundColor: `${accentColor}20`,
                          }}
                        >
                          {route === "aux" ? (
                            <Headphones className="h-3 w-3 text-emerald-400" />
                          ) : route === "bt" ? (
                            <Bluetooth className="h-3 w-3 text-blue-400" />
                          ) : (
                            <Speaker className="h-3 w-3 text-amber-400" />
                          )}
                        </span>
                        <div className="min-w-0">
                          <p className="truncate text-[11px] font-semibold leading-tight text-white">
                            {routeName}
                          </p>
                          <p className="truncate text-[9px] leading-tight text-neutral-400">
                            Mic active
                          </p>
                        </div>
                      </div>
                      <div className="flex h-3.5 items-end gap-[2.5px]">
                        <span className="eq-1 h-full w-[2.5px] origin-bottom rounded-full bg-blue-400" />
                        <span className="eq-2 h-full w-[2.5px] origin-bottom rounded-full bg-emerald-400" />
                        <span className="eq-3 h-full w-[2.5px] origin-bottom rounded-full bg-purple-400" />
                      </div>
                    </div>
                  )}

                  {page === 1 && (
                    <div className="flex flex-1 flex-col justify-center px-1.5">
                      <div className="flex items-center justify-between">
                        <div className="min-w-0">
                          <p className="truncate text-[11px] font-semibold leading-tight text-white">
                            Teardrop
                          </p>
                          <p className="truncate text-[9px] leading-tight text-neutral-400">
                            Massive Attack · Spotify
                          </p>
                        </div>
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            setPlaying((p) => !p);
                          }}
                          className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-white/15 text-white hover:bg-white/25"
                        >
                          {playing ? (
                            <Pause className="h-3 w-3" />
                          ) : (
                            <Play className="h-3 w-3" />
                          )}
                        </button>
                      </div>
                      <div className="mt-1 h-[2px] w-full overflow-hidden rounded-full bg-white/15">
                        <div
                          className="h-full bg-emerald-400 transition-all duration-500"
                          style={{ width: `${(sec / totalSec) * 100}%` }}
                        />
                      </div>
                    </div>
                  )}

                  {page === 2 && (
                    <div className="flex flex-1 items-center justify-center gap-3">
                      <span className="text-[11px] font-semibold text-emerald-300 tabular-nums">
                        Laptop 92%
                      </span>
                      <span className="h-3 w-px bg-white/15" />
                      <span className="text-[11px] font-semibold text-blue-300 tabular-nums">
                        Headset 90%
                      </span>
                    </div>
                  )}

                  <div className="flex items-center gap-1">
                    <span className="flex gap-1">
                      {[0, 1, 2].map((i) => (
                        <span
                          key={i}
                          className={`h-1 w-1 rounded-full ${
                            page === i ? "bg-white" : "bg-white/30"
                          }`}
                        />
                      ))}
                    </span>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        shiftPage(1);
                      }}
                      className="flex h-6 w-5 items-center justify-center rounded text-neutral-400 hover:bg-white/10 hover:text-white"
                      aria-label="Next"
                    >
                      ›
                    </button>
                  </div>
                </div>
              )}

              {/* EXPANDED COMPACT 3-ISLAND DECK */}
              {stage === "expanded" && (
                <div className="p-3">
                  <div className="mb-2 flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <span
                        className="flex h-6 w-6 items-center justify-center rounded-full border"
                        style={{
                          borderColor: `${accentColor}55`,
                          backgroundColor: `${accentColor}22`,
                        }}
                      >
                        <Volume2 className="h-3 w-3 text-white" />
                      </span>
                      <div>
                        <p className="text-[12px] font-semibold leading-tight text-white">
                          {page === 0
                            ? "Audio Island"
                            : page === 1
                            ? "Spotify"
                            : "Battery & System"}
                        </p>
                        <p className="text-[9.5px] text-neutral-400">{routeName}</p>
                      </div>
                    </div>
                    <button
                      onClick={() => setStage("pill")}
                      className="rounded-md bg-white/[0.06] px-2 py-0.5 text-[10px] text-neutral-300 hover:bg-white/15 hover:text-white"
                    >
                      ⌃
                    </button>
                  </div>

                  {/* Page Strip */}
                  <div className="mb-2 grid grid-cols-3 gap-1 rounded-[9px] border border-white/10 bg-black/40 p-0.5">
                    {[
                      { id: 0 as Page, name: "Audio" },
                      { id: 1 as Page, name: "Spotify" },
                      { id: 2 as Page, name: "Battery" },
                    ].map((t) => (
                      <button
                        key={t.id}
                        onClick={() => setPage(t.id)}
                        className={`h-6 rounded-[7px] text-[10.5px] font-medium transition ${
                          page === t.id
                            ? "bg-white/15 text-white"
                            : "text-neutral-400 hover:text-neutral-200"
                        }`}
                      >
                        {t.name}
                      </button>
                    ))}
                  </div>

                  <div
                    className="transition-transform duration-150"
                    style={{
                      transform: `translateX(${dragX}px)`,
                      opacity: Math.max(0.42, 1 - Math.abs(dragX) / 140),
                    }}
                  >
                    {page === 0 && (
                      <div className="space-y-1.5">
                        {/* Outputs | Microphones | Apps sub-tabs */}
                        <div className="grid grid-cols-3 gap-0.5 rounded-[8px] border border-white/10 bg-black/40 p-0.5">
                          {(
                            [
                              { id: "outputs", label: "Outputs" },
                              { id: "mics", label: "Microphones" },
                              { id: "apps", label: "Apps" },
                            ] as { id: AudioSubTab; label: string }[]
                          ).map((st) => (
                            <button
                              key={st.id}
                              onClick={() => setAudioTab(st.id)}
                              className={`h-5 rounded-[6px] text-[10px] font-medium transition ${
                                audioTab === st.id
                                  ? "bg-blue-600 text-white"
                                  : "text-neutral-400 hover:text-white"
                              }`}
                            >
                              {st.label}
                            </button>
                          ))}
                        </div>

                        {audioTab === "outputs" && (
                          <>
                            {[
                              { key: "aux" as const, title: "AUX", sub: "Realtek HD Audio", col: "#10b981" },
                              { key: "bt" as const, title: "Bluetooth", sub: "JBL Tune 720BT • 90%", col: "#3b82f6" },
                              { key: "speakers" as const, title: "Speakers", sub: "Laptop Array", col: "#f59e0b" },
                            ].map((dev) => {
                              const on = route === dev.key;
                              return (
                                <div
                                  key={dev.key}
                                  className="rounded-[10px] border px-2.5 py-1.5 transition"
                                  style={{
                                    borderColor: on ? `${dev.col}88` : "rgba(255,255,255,0.08)",
                                    background: on
                                      ? "rgba(255,255,255,0.05)"
                                      : "rgba(255,255,255,0.02)",
                                  }}
                                >
                                  <div className="flex items-center justify-between">
                                    <div>
                                      <div className="flex items-center gap-1.5">
                                        <span className="text-[11.5px] font-semibold text-white">
                                          {dev.title}
                                        </span>
                                        {on && (
                                          <span className="text-[8.5px] font-bold uppercase tracking-wider text-emerald-400">
                                            Active
                                          </span>
                                        )}
                                      </div>
                                      <p className="text-[9.5px] text-neutral-400">{dev.sub}</p>
                                    </div>
                                    <button
                                      onClick={() => setRoute(dev.key)}
                                      className={`relative h-[20px] w-[36px] rounded-full border transition ${
                                        on
                                          ? "border-blue-400 bg-blue-600"
                                          : "border-neutral-600 bg-neutral-800"
                                      }`}
                                      aria-label={`Switch to ${dev.title}`}
                                    >
                                      <span
                                        className={`absolute top-[2px] h-3.5 w-3.5 rounded-full bg-white transition-all ${
                                          on ? "left-[18px]" : "left-[2px]"
                                        }`}
                                      />
                                    </button>
                                  </div>
                                  {(on || mirror) && (
                                    <div className="mt-1 flex items-center gap-2">
                                      <span className="text-[9px] text-neutral-400">Vol</span>
                                      <input
                                        type="range"
                                        min={0}
                                        max={100}
                                        value={vol}
                                        onChange={(e) => setVol(Number(e.target.value))}
                                        className="h-1 flex-1 accent-blue-400"
                                      />
                                      <span className="w-7 text-right text-[9.5px] text-neutral-300 tabular-nums">
                                        {vol}%
                                      </span>
                                    </div>
                                  )}
                                </div>
                              );
                            })}

                            <div className="rounded-[10px] border border-white/10 bg-white/[0.02] px-2.5 py-1.5">
                              <div className="flex items-center justify-between">
                                <span className="text-[11px] font-medium text-neutral-200">
                                  Multi-Output Mirror
                                </span>
                                <button
                                  onClick={() => setMirror((m) => !m)}
                                  className={`rounded px-2 py-0.5 text-[9.5px] font-semibold transition ${
                                    mirror
                                      ? "bg-cyan-500/20 text-cyan-300"
                                      : "bg-white/10 text-neutral-300 hover:bg-white/15"
                                  }`}
                                >
                                  {mirror ? "Single Mode" : "Sync AUX + BT"}
                                </button>
                              </div>
                              {mirror && (
                                <div className="mt-1 flex items-center gap-2">
                                  <span className="text-[9px] text-cyan-300">Delay</span>
                                  <input
                                    type="range"
                                    min={0}
                                    max={350}
                                    value={delayMs}
                                    onChange={(e) => setDelayMs(Number(e.target.value))}
                                    className="h-1 flex-1 accent-cyan-400"
                                  />
                                  <span className="w-10 text-right text-[9.5px] text-neutral-300 tabular-nums">
                                    {delayMs} ms
                                  </span>
                                </div>
                              )}
                            </div>
                          </>
                        )}

                        {audioTab === "mics" && (
                          <div className="space-y-1.5">
                            {[
                              { key: "bt" as const, title: "Bluetooth Mic", sub: "JBL Tune 720BT Hands-Free" },
                              { key: "laptop" as const, title: "Laptop Mic", sub: "Realtek Microphone Array" },
                              { key: "aux" as const, title: "AUX Jack Mic", sub: "Wired 3.5mm Headset" },
                            ].map((m) => {
                              const on = micRoute === m.key;
                              return (
                                <div
                                  key={m.key}
                                  className="flex items-center justify-between rounded-[10px] border px-2.5 py-1.5"
                                  style={{
                                    borderColor: on ? "#a855f788" : "rgba(255,255,255,0.08)",
                                    background: on ? "rgba(255,255,255,0.05)" : "rgba(255,255,255,0.02)",
                                  }}
                                >
                                  <div>
                                    <p className="text-[11px] font-semibold text-white">{m.title}</p>
                                    <p className="text-[9.5px] text-neutral-400">{m.sub}</p>
                                  </div>
                                  <button
                                    onClick={() => setMicRoute(m.key)}
                                    className={`relative h-[20px] w-[36px] rounded-full border transition ${
                                      on ? "border-purple-400 bg-purple-600" : "border-neutral-600 bg-neutral-800"
                                    }`}
                                    aria-label={`Use ${m.title}`}
                                  >
                                    <span
                                      className={`absolute top-[2px] h-3.5 w-3.5 rounded-full bg-white transition-all ${
                                        on ? "left-[18px]" : "left-[2px]"
                                      }`}
                                    />
                                  </button>
                                </div>
                              );
                            })}
                          </div>
                        )}

                        {audioTab === "apps" && (
                          <div className="space-y-1.5">
                            <div className="rounded-[10px] border border-white/10 bg-white/[0.03] px-2.5 py-2">
                              <div className="flex items-center justify-between gap-2">
                                <span className="text-[10.5px] font-semibold text-white">
                                  Dual Split
                                </span>
                                <button
                                  onClick={() => {
                                    setSpotifyOut("default");
                                    setDiscordOut("default");
                                  }}
                                  className="rounded border border-white/10 bg-white/5 px-2 py-0.5 text-[9px] text-neutral-300 hover:bg-white/10"
                                >
                                  Reset
                                </button>
                              </div>
                              <button
                                onClick={() => {
                                  setSpotifyOut("aux");
                                  setDiscordOut("bt");
                                }}
                                className="mt-1.5 w-full rounded-[7px] border border-cyan-400/30 bg-cyan-500/10 py-1 text-[10px] font-medium text-cyan-200 hover:bg-cyan-500/20"
                              >
                                Split: Music → AUX + Calls → Bluetooth
                              </button>
                            </div>

                            <div className="flex items-center justify-between gap-2 rounded-[10px] border border-white/10 bg-white/[0.02] px-2.5 py-1.5">
                              <div className="min-w-0">
                                <p className="text-[11px] font-semibold text-white">Spotify</p>
                                <p className="truncate text-[9px] text-emerald-400">
                                  Playing → {spotifyOut === "aux" ? "AUX (3.5mm)" : spotifyOut === "bt" ? "BT • JBL" : "Default"}
                                </p>
                              </div>
                              <select
                                value={spotifyOut}
                                onChange={(e) => setSpotifyOut(e.target.value as "default" | "aux" | "bt")}
                                className="rounded-md border border-white/15 bg-[#101624] px-2 py-1 text-[10px] text-white"
                              >
                                <option value="default">Default</option>
                                <option value="aux">AUX (3.5mm)</option>
                                <option value="bt">BT • JBL Tune</option>
                              </select>
                            </div>

                            <div className="flex items-center justify-between gap-2 rounded-[10px] border border-white/10 bg-white/[0.02] px-2.5 py-1.5">
                              <div className="min-w-0">
                                <p className="text-[11px] font-semibold text-white">Discord</p>
                                <p className="truncate text-[9px] text-blue-300">
                                  Call + Mic → {discordOut === "bt" ? "BT • JBL" : discordOut === "aux" ? "AUX (3.5mm)" : "Default"}
                                </p>
                              </div>
                              <select
                                value={discordOut}
                                onChange={(e) => setDiscordOut(e.target.value as "default" | "aux" | "bt")}
                                className="rounded-md border border-white/15 bg-[#101624] px-2 py-1 text-[10px] text-white"
                              >
                                <option value="default">Default</option>
                                <option value="bt">BT • JBL Tune</option>
                                <option value="aux">AUX (3.5mm)</option>
                              </select>
                            </div>
                          </div>
                        )}
                      </div>
                    )}

                    {page === 1 && (
                      <div className="space-y-2">
                        <div className="rounded-[12px] border border-white/10 bg-white/[0.03] p-3">
                          <div className="mb-2.5 flex items-center gap-3">
                            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg border border-white/15 bg-neutral-900 text-base text-emerald-400">
                              ♪
                            </div>
                            <div className="min-w-0 flex-1">
                              <div className="flex items-center justify-between gap-2">
                                <p className="text-[9.5px] font-medium text-emerald-400">
                                  Spotify · {playing ? "Now Playing" : "Paused"}
                                </p>
                                <button
                                  onClick={() => setSpotifyInTray((v) => !v)}
                                  className="rounded border border-white/10 bg-white/5 px-1.5 py-0.5 text-[9px] text-neutral-300 hover:bg-white/10"
                                >
                                  {spotifyInTray ? "Show" : "Hide"}
                                </button>
                              </div>
                              <p className="truncate text-[13px] font-semibold text-white">
                                Teardrop
                              </p>
                              <p className="truncate text-[10.5px] text-neutral-400">
                                Massive Attack · {spotifyInTray ? "Hidden Tray" : "Taskbar"}
                              </p>
                            </div>
                          </div>
                          <div className="h-1 w-full overflow-hidden rounded-full bg-white/15">
                            <div
                              className="h-full bg-emerald-400 transition-all duration-500"
                              style={{ width: `${(sec / totalSec) * 100}%` }}
                            />
                          </div>
                          <div className="mt-1 flex justify-between text-[9px] text-neutral-400 tabular-nums">
                            <span>{fmt(sec)}</span>
                            <span>{fmt(totalSec)}</span>
                          </div>
                          <div className="mt-2 flex items-center justify-center gap-3">
                            <button
                              onClick={() => setSec(0)}
                              className="flex h-8 w-8 items-center justify-center rounded-full border border-white/15 bg-white/5 text-white hover:bg-white/15"
                            >
                              <SkipBack className="h-3.5 w-3.5" />
                            </button>
                            <button
                              onClick={() => setPlaying((p) => !p)}
                              className="flex h-9 w-9 items-center justify-center rounded-full bg-white text-black"
                            >
                              {playing ? (
                                <Pause className="h-4 w-4" />
                              ) : (
                                <Play className="h-4 w-4" />
                              )}
                            </button>
                            <button
                              onClick={() => setSec(140)}
                              className="flex h-8 w-8 items-center justify-center rounded-full border border-white/15 bg-white/5 text-white hover:bg-white/15"
                            >
                              <SkipForward className="h-3.5 w-3.5" />
                            </button>
                          </div>
                        </div>
                      </div>
                    )}

                    {page === 2 && (
                      <div className="space-y-2">
                        <div className="flex items-center justify-around rounded-[12px] border border-white/10 bg-white/[0.03] py-4">
                          {[
                            { label: "Laptop", pct: 92, col: "#34d399" },
                            { label: "JBL Tune", pct: 90, col: "#60a5fa" },
                            { label: "CPU App", pct: 0, col: "#22d3ee" },
                          ].map((g) => (
                            <div key={g.label} className="flex flex-col items-center">
                              <div
                                className="flex h-12 w-12 items-center justify-center rounded-full border-[3.5px] text-[11px] font-bold text-white tabular-nums"
                                style={{ borderColor: g.col }}
                              >
                                {g.pct}%
                              </div>
                              <span className="mt-1.5 text-[10px] text-neutral-300">
                                {g.label}
                              </span>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Bottom Desktop Hint & Stage Switcher */}
          <div className="flex flex-wrap items-center justify-between gap-2 border-t border-white/[0.07] pt-2.5 text-[11px] text-neutral-400">
            <span>
              {stage === "rest"
                ? "Resting at 0% idle CPU — hover top edge"
                : stage === "pill"
                ? "Drag left/right to slide · Click to open"
                : "Drag any card left/right to slide islands"}
            </span>
            <div className="flex items-center gap-1 rounded-lg border border-white/10 bg-black/50 p-0.5">
              {(["rest", "pill", "expanded"] as Stage[]).map((s) => (
                <button
                  key={s}
                  onClick={() => setStage(s)}
                  className={`rounded-md px-2.5 py-0.5 text-[10.5px] font-medium transition ${
                    stage === s
                      ? "bg-white text-black"
                      : "text-neutral-400 hover:text-white"
                  }`}
                >
                  {s === "rest" ? "Idle" : s === "pill" ? "Hover" : "Open"}
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
