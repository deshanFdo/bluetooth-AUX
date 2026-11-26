import { NextRequest, NextResponse } from "next/server";

const KNOWN_HTML_PATHS = new Set(["/", "/about", "/contact", "/privacy"]);
const PUBLIC_PASSTHROUGH_PATHS = new Set([
  "/robots.txt",
  "/sitemap.xml",
  "/llms.txt",
  "/llms-full.txt",
  "/opengraph-image",
  "/download",
  "/portable",
  "/a8f4d9c2e1b7403892c5f1a6e3d8b402.txt",
]);

const HOME_MARKDOWN = `# Audio Switch — Dynamic Island for Windows 10 / 11

No longer need to unplug your 3.5mm AUX cable from your computer just to listen through an already-connected Bluetooth headset — switch between wired AUX, Bluetooth headsets, speakers, and microphones right from a compact top-of-screen Dynamic Island at ~0% idle CPU.

- **Homepage:** https://audio-switch.vercel.app
- **Download Windows Installer (74.4 MB, Self-Contained):** https://audio-switch.vercel.app/download
- **Download Portable EXE (74.4 MB):** https://audio-switch.vercel.app/portable
- **Source Code (GitHub):** https://github.com/deshanFdo/bluetooth-AUX
- **Machine-Readable Summary:** https://audio-switch.vercel.app/llms.txt

## Highlights
- **01 / Core Audio + Multi-Output Sync Delay:** Switches default Windows output and mic endpoints via native Core Audio COM (\`IPolicyConfig\`) in <1ms, and mirrors audio across AUX + Bluetooth simultaneously with a 0–350 ms WASAPI FIFO latency compensator.
- **02 / Slidable 3-Page Island:** Hover the top-edge glow bar to expand a compact capsule, or click and drag horizontally between **Audio**, **Spotify**, and **Battery & System**.
- **03 / ~0% Idle CPU & System Tray Mode:** Stops all audio peak samplers and telemetry timers when resting collapsed; integrates with Windows Settings → Installed Apps for clean uninstallation.
`;

function markdownForPath(pathname: string): string {
  if (pathname === "/about") {
    return `# About Audio Switch\n\nAudio Switch is a free, open-source Windows 10/11 desktop utility built by Deshan Fernando in C# (.NET 8 WPF + Win32 Core Audio + WASAPI) paired with a Next.js landing page.\n\n- Source: https://github.com/deshanFdo/bluetooth-AUX\n- Installer: https://audio-switch.vercel.app/download\n- Technical reference: https://audio-switch.vercel.app/llms-full.txt\n`;
  }
  if (pathname === "/privacy") {
    return `# Privacy Policy — Audio Switch\n\nAudio Switch runs 100% locally on your Windows device. It uses zero analytics SDKs, zero telemetry beacons, and zero cloud accounts. Settings are stored strictly in \`%LocalAppData%\\AudioSwitch\\settings.json\`.\n\n- Homepage: https://audio-switch.vercel.app\n- Source audit: https://github.com/deshanFdo/bluetooth-AUX\n`;
  }
  if (pathname === "/contact") {
    return `# Contact & Support — Audio Switch\n\n- GitHub Issues & Source: https://github.com/deshanFdo/bluetooth-AUX/issues\n- Maintainer: Deshan Fernando\n- Email: deshanfernando67@gmail.com\n- Homepage: https://audio-switch.vercel.app\n`;
  }
  return HOME_MARKDOWN;
}

export function middleware(req: NextRequest) {
  const { pathname } = req.nextUrl;
  const accept = (req.headers.get("accept") || "").toLowerCase();

  const mdIdx = accept.indexOf("text/markdown");
  const htmlIdx = accept.indexOf("text/html");
  const wantsMarkdown = mdIdx >= 0 && (htmlIdx < 0 || mdIdx < htmlIdx);

  if (wantsMarkdown && !PUBLIC_PASSTHROUGH_PATHS.has(pathname)) {
    if (KNOWN_HTML_PATHS.has(pathname)) {
      return new NextResponse(markdownForPath(pathname), {
        status: 200,
        headers: {
          "Content-Type": "text/markdown; charset=utf-8",
          Vary: "Accept",
        },
      });
    }

    return new NextResponse(
      `# 404 Not Found\n\nNo resource exists at \`${pathname}\` on audio-switch.vercel.app.\n\n- Homepage: https://audio-switch.vercel.app\n- Sitemap: https://audio-switch.vercel.app/sitemap.xml\n- Agent documentation: https://audio-switch.vercel.app/llms.txt\n`,
      {
        status: 404,
        headers: {
          "Content-Type": "text/markdown; charset=utf-8",
          Vary: "Accept",
        },
      }
    );
  }

  const res = NextResponse.next();
  res.headers.set("Vary", "Accept");
  return res;
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
