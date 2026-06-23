import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// Build output goes straight into web/static, which FastAPI serves (index.html at /, hashed
// assets at /assets). `npm run dev` proxies the API to the Python server on :8080.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    outDir: "../static",
    emptyOutDir: true,
    assetsDir: "assets",
  },
  server: {
    port: 5173,
    proxy: { "/api": "http://localhost:8080" },
  },
});
