import type { Metadata } from "next";
import { Inter, JetBrains_Mono } from "next/font/google";
import "./globals.css";

const inter = Inter({
  subsets: ["latin"],
  variable: "--font-sans",
  display: "swap",
});

const mono = JetBrains_Mono({
  subsets: ["latin"],
  variable: "--font-mono",
  display: "swap",
});

export const metadata: Metadata = {
  metadataBase: new URL("https://audio-switch.vercel.app"),
  title: "Audio Switch — Dynamic Island Audio Switcher for Windows & Ubuntu Linux",
  description:
    "Free lightweight Dynamic Island for Windows 10/11 & Ubuntu/Debian Linux. Stop unplugging your 3.5mm AUX cable just to listen through an already-connected Bluetooth headset — switch outputs & mics, split apps onto AUX vs Discord, control Spotify & YouTube Music, and monitor headset battery at ~0% idle CPU.",
  keywords: [
    "switch AUX and Bluetooth audio without unplugging Windows",
    "Ubuntu PipeWire Dynamic Island audio switcher",
    "Windows 11 Dynamic Island",
    "Ubuntu Bluetooth AUX audio switcher",
    "play audio on Bluetooth and wired headphones at same time",
    "Bluetooth audio sync delay Windows 11 Linux",
    "Spotify YouTube Music Dynamic Island",
    "Bluetooth headphone battery monitor",
    "Audio Switch Windows Linux",
  ],
  authors: [{ name: "Deshan Fernando" }],
  creator: "Deshan Fernando",
  alternates: {
    canonical: "/",
  },
  openGraph: {
    type: "website",
    url: "https://audio-switch.vercel.app",
    siteName: "Audio Switch",
    title: "Audio Switch — Dynamic Island for Windows & Linux Audio, Split Apps & Spotify",
    description:
      "Top-of-screen iOS-style Dynamic Island for Windows 10/11 & Ubuntu/Debian Linux. Instant AUX/Bluetooth routing, per-app split audio, Spotify/YT Music switcher, and headset battery rings.",
  },
  twitter: {
    card: "summary_large_image",
    title: "Audio Switch — Dynamic Island for Windows & Ubuntu Linux Audio",
    description:
      "Switch AUX, Bluetooth & microphones from a top-edge Dynamic Island, split music onto AUX while Discord uses Bluetooth, and monitor headset battery rings.",
  },
  robots: {
    index: true,
    follow: true,
    googleBot: {
      index: true,
      follow: true,
      "max-video-preview": -1,
      "max-image-preview": "large",
      "max-snippet": -1,
    },
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className={`${inter.variable} ${mono.variable} dark`}>
      <body className="bg-[#050507] font-sans text-[#f4f4f6] antialiased selection:bg-white/20 selection:text-white">
        {children}
      </body>
    </html>
  );
}
