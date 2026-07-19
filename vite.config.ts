import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// Build output goes into ./dist (this repo is standalone since the split from the backend repo).
// Deploy step copies dist/ into the backend's static dir (or serve it from any static host).
// `npm run dev` proxies /api to the Python server on :8080.
// e2e-only test harness pages (mount a single feature in isolation) are built ONLY
// when BUILD_HARNESS=1, so they never ship in the production bundle.
const input = process.env.BUILD_HARNESS
  ? { main: "index.html", documents: "documents-harness.html" }
  : undefined;

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    outDir: "dist",
    emptyOutDir: true,
    assetsDir: "assets",
    rollupOptions: input ? { input } : undefined,
  },
  server: {
    port: 5173,
    proxy: { "/api": "http://localhost:8080" },
  },
});
