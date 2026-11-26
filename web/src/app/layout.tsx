import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Audio Switch — iOS Dynamic Island Audio & Media Control for Windows 11",
  description:
    "Unobtrusive top-of-screen Dynamic Island for Windows 10/11. Switch AUX, Bluetooth, Speakers & Microphones, mirror multi-output audio with sync delay, control Spotify, and monitor Bluetooth headset battery rings at ~0% idle CPU.",
  openGraph: {
    title: "Audio Switch — Dynamic Island for Windows 11",
    description:
      "Instant AUX, Bluetooth & Multi-Output audio switcher, live Spotify island, and Bluetooth battery monitor at ~0% resting CPU.",
    type: "website",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="dark">
      <body className="antialiased selection:bg-blue-500/30 selection:text-white">
        {children}
      </body>
    </html>
  );
}
