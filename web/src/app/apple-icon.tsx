import { ImageResponse } from "next/og";

export const runtime = "edge";
export const size = { width: 180, height: 180 };
export const contentType = "image/png";

export default function AppleIcon() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          borderRadius: 42,
          backgroundColor: "#05070d",
          border: "4px solid rgba(96, 165, 250, 0.35)",
        }}
      >
        <div
          style={{
            width: 120,
            height: 56,
            borderRadius: 28,
            backgroundColor: "#0f172a",
            border: "2px solid rgba(255,255,255,0.24)",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "0 18px",
          }}
        >
          <div style={{ width: 20, height: 20, borderRadius: 10, backgroundColor: "#34d399" }} />
          <div style={{ width: 20, height: 20, borderRadius: 10, backgroundColor: "#60a5fa" }} />
        </div>
      </div>
    ),
    { ...size }
  );
}
