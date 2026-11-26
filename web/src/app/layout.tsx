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
  title: "Audio Switch — Dynamic Island Audio Control for Windows",
  description:
    "Switch AUX, Bluetooth headsets, speakers, and mics from a small top-of-screen Dynamic Island. Mirror devices with sync delay, control Spotify, and monitor battery rings at ~0% idle CPU.",
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
