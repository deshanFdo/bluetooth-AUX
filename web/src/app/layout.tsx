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
  title: "Audio Switch — Dynamic Island Audio Switcher & Multi-Output for Windows 11",
  description:
    "Free lightweight Dynamic Island for Windows 10/11. Switch AUX headphones, Bluetooth headsets, speakers & mics in <1ms, play Bluetooth + wired audio together with sync delay, control Spotify, and monitor headset battery at ~0% idle CPU.",
  keywords: [
    "Windows 11 Dynamic Island",
    "switch AUX and Bluetooth audio Windows 11",
    "play audio on Bluetooth and wired headphones at same time Windows",
    "Bluetooth audio sync delay Windows 11",
    "Windows audio output switcher tray app",
    "Bluetooth headphone battery monitor Windows 11",
    "Spotify Dynamic Island Windows",
    "Audio Switch Windows",
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
    title: "Audio Switch — Dynamic Island for Windows Audio, Sync Delay & Spotify",
    description:
      "Top-of-screen iOS-style Dynamic Island for Windows 10/11. Instant AUX/Bluetooth routing, multi-output sync delay, live SMTC Spotify bar, and headset battery rings at ~0% idle CPU.",
  },
  twitter: {
    card: "summary_large_image",
    title: "Audio Switch — Dynamic Island for Windows 11 Audio",
    description:
      "Switch AUX, Bluetooth & microphones from a top-edge Dynamic Island, sync multi-output audio delay, and monitor battery rings at ~0% idle CPU.",
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
