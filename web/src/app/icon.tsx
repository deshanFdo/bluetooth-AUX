import { ImageResponse } from "next/og";

export const runtime = "edge";
export const size = { width: 32, height: 32 };
export const contentType = "image/png";

export default function Icon() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          borderRadius: 9,
          backgroundColor: "#070a12",
          border: "1px solid rgba(96, 165, 250, 0.45)",
        }}
      >
        <div
          style={{
            width: 18,
            height: 10,
            borderRadius: 5,
            backgroundColor: "#111827",
            border: "1px solid rgba(255,255,255,0.22)",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "0 3px",
          }}
        >
          <div style={{ width: 4, height: 4, borderRadius: 2, backgroundColor: "#34d399" }} />
          <div style={{ width: 4, height: 4, borderRadius: 2, backgroundColor: "#60a5fa" }} />
        </div>
      </div>
    ),
    { ...size }
  );
}
