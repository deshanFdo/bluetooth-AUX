import type { MetadataRoute } from "next";

export default function sitemap(): MetadataRoute.Sitemap {
  const now = new Date();
  return [
    {
      url: "https://audio-switch.vercel.app",
      lastModified: now,
      changeFrequency: "weekly",
      priority: 1,
    },
    {
      url: "https://audio-switch.vercel.app/llms.txt",
      lastModified: now,
      changeFrequency: "weekly",
      priority: 0.8,
    },
    {
      url: "https://audio-switch.vercel.app/llms-full.txt",
      lastModified: now,
      changeFrequency: "weekly",
      priority: 0.7,
    },
  ];
}
