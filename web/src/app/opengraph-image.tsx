import { ImageResponse } from "next/og";

export const runtime = "edge";
export const alt = "Audio Switch — Top-edge Dynamic Island for Windows & Linux audio";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default function OpenGraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          backgroundColor: "#050507",
          color: "#f4f4f6",
          fontFamily: "system-ui, sans-serif",
          padding: "64px",
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            width: 480,
            height: 68,
            borderRadius: 34,
            backgroundColor: "#0d1017",
            border: "1px solid rgba(255,255,255,0.18)",
            padding: "0 26px",
            marginBottom: 48,
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
            <div
              style={{
                width: 14,
                height: 14,
                borderRadius: 7,
                backgroundColor: "#34d399",
              }}
            />
            <span style={{ fontSize: 22, fontWeight: 600 }}>Audio Switch</span>
          </div>
          <span style={{ fontSize: 16, color: "#9ca3af" }}>AUX · BT · Spotify · YT Music</span>
        </div>

        <div
          style={{
            fontSize: 60,
            fontWeight: 700,
            letterSpacing: "-0.03em",
            textAlign: "center",
            lineHeight: 1.08,
            maxWidth: 960,
          }}
        >
          The top-edge Dynamic Island for Windows &amp; Linux audio.
        </div>

        <div
          style={{
            marginTop: 24,
            fontSize: 23,
            color: "#9ca3af",
            textAlign: "center",
          }}
        >
          Split music onto AUX + Discord on Bluetooth · Encrypted config · Windows 10/11 &amp; Ubuntu
        </div>
      </div>
    ),
    { ...size }
  );
}
