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
  "/download-deb",
  "/download-linux",
  "/a8f4d9c2e1b7403892c5f1a6e3d8b402.txt",
]);

const HOME_MARKDOWN = `# WaveDock — Dynamic Island for Windows 10/11 & Ubuntu / Debian Linux (v1.3.0)

WaveDock (formerly Audio Switch) lets you switch between wired AUX, Bluetooth headsets, speakers, and mics from a top-edge Dynamic Island without unplugging cables — plus per-app Dual Split audio, Spotify/YT Music switching, and connected battery rings.

- **Homepage:** https://audio-switch.vercel.app
- **Windows Installer (\`v1.3.0\`, 74.4 MB, Self-Contained):** https://audio-switch.vercel.app/download
- **Windows Portable EXE (\`v1.3.0\`, 74.4 MB):** https://audio-switch.vercel.app/portable
- **Ubuntu / Debian \`.deb\` (\`v1.3.0\`, 36.3 MB):** https://audio-switch.vercel.app/download-deb
- **Linux \`.tar.gz\` (\`v1.3.0\`, 36.3 MB):** https://audio-switch.vercel.app/download-linux
- **Source Code (GitHub):** https://github.com/deshanFdo/bluetooth-AUX
- **Machine-Readable Summary:** https://audio-switch.vercel.app/llms.txt

## Highlights
- **01 / Zero-Unplug Switching & Split Audio:** Switches default outputs & mics via Core Audio COM (\`IPolicyConfig\`) or Linux PipeWire (\`pactl\`), splits apps cleanly (Music -> AUX, Discord -> BT), and mirrors audio with 0–350 ms delay.
- **02 / Spotify & YouTube Music + Hidden Tray:** 1-tap switching with pause handoff, auto-launch on Play, Windows Hidden Icons, and Linux MPRIS2.
- **03 / Hardened Local Security & Clean Uninstall:** DPAPI / AES-256-GCM encrypted \`settings.dat\` + \`install.sig\`, System32 P/Invoke search lock, CurrentUser-only pipe ACL, \`ReadOnly\` / \`0500\` binary protection, and zero-leftover uninstall.
`;

function markdownForPath(pathname: string): string {
  if (pathname === "/about") {
    return `# About WaveDock\n\nWaveDock (formerly Audio Switch) is a free, open-source Windows 10/11 (.NET 8 WPF) & Ubuntu/Debian Linux (.NET 8 Avalonia + PipeWire) Dynamic Island desktop utility built by Deshan Fernando.\n\n- Source: https://github.com/deshanFdo/bluetooth-AUX\n- Windows: https://audio-switch.vercel.app/download\n- Ubuntu / Debian: https://audio-switch.vercel.app/download-deb\n- Technical reference: https://audio-switch.vercel.app/llms-full.txt\n`;
  }
  if (pathname === "/privacy") {
    return `# Privacy & Security — WaveDock\n\nWaveDock runs 100% locally offline with zero analytics or telemetry. Settings and install seals are encrypted via Windows DPAPI (\`%LocalAppData%\\WaveDock\\settings.dat\`) or Linux AES-256-GCM (\`~/.config/WaveDock/settings.dat\`).\n\n- Homepage: https://audio-switch.vercel.app\n- Source audit: https://github.com/deshanFdo/bluetooth-AUX\n`;
  }
  if (pathname === "/contact") {
    return `# Contact & Support — WaveDock\n\n- GitHub Issues & Source: https://github.com/deshanFdo/bluetooth-AUX/issues\n- Maintainer: Deshan Fernando\n- Email: deshanfernando67@gmail.com\n- Diagnostics: WaveDock.exe --diag (Windows) / WaveDock --diag (Linux)\n`;
  }
  return HOME_MARKDOWN;
}

function sanitizePathForText(pathname: string): string {
  return pathname.replace(/[^a-zA-Z0-9/_.\-]/g, "").slice(0, 120) || "/";
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
          "X-Content-Type-Options": "nosniff",
          "X-Frame-Options": "DENY",
          Vary: "Accept",
        },
      });
    }

    const safePath = sanitizePathForText(pathname);
    return new NextResponse(
      `# 404 Not Found\n\nNo resource exists at \`${safePath}\` on audio-switch.vercel.app.\n\n- Homepage: https://audio-switch.vercel.app\n- Sitemap: https://audio-switch.vercel.app/sitemap.xml\n- Agent documentation: https://audio-switch.vercel.app/llms.txt\n`,
      {
        status: 404,
        headers: {
          "Content-Type": "text/markdown; charset=utf-8",
          "X-Content-Type-Options": "nosniff",
          "X-Frame-Options": "DENY",
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
