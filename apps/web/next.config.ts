import type { NextConfig } from "next";
import { fileURLToPath } from "node:url";

const apiOrigin = (process.env.API_BASE_URL ?? "http://127.0.0.1:5080").replace(
  /\/$/,
  "",
);
const config: NextConfig = {
  output: "standalone",
  outputFileTracingRoot: fileURLToPath(new URL("../../", import.meta.url)),
  poweredByHeader: false,
  async rewrites() {
    return [
      { source: "/api/:path*", destination: `${apiOrigin}/api/:path*` },
      { source: "/health/live", destination: `${apiOrigin}/health/live` },
      { source: "/health/ready", destination: `${apiOrigin}/health/ready` },
    ];
  },
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "X-Frame-Options", value: "DENY" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
        ],
      },
    ];
  },
};
export default config;
