import type { MetadataRoute } from "next";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: [
      {
        userAgent: "*",
        allow: "/",
      },
      {
        userAgent: [
          "Googlebot",
          "Bingbot",
          "OAI-SearchBot",
          "GPTBot",
          "ChatGPT-User",
          "Google-Extended",
          "PerplexityBot",
          "ClaudeBot",
          "Anthropic-AI",
          "Applebot-Extended",
        ],
        allow: "/",
      },
    ],
    sitemap: "https://audio-switch.vercel.app/sitemap.xml",
    host: "https://audio-switch.vercel.app",
  };
}
