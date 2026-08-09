import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// Build output goes into ./dist (this repo is standalone since the split from the backend repo).
// Deploy step copies dist/ into the backend's static dir (or serve it from any static host).
// `npm run dev` proxies /api and /admin/api to whatever backend you're running locally — default
// is the Python server on :8080; set VITE_DEV_API_TARGET in your own gitignored .env to point
// elsewhere (e.g. a natively-run Korkut.Api on :5129).
// e2e-only test harness pages (mount a single feature in isolation) are built ONLY
// when BUILD_HARNESS=1, so they never ship in the production bundle.
const input = process.env.BUILD_HARNESS
  ? { main: "index.html", documents: "documents-harness.html" }
  : undefined;

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const apiTarget = env.VITE_DEV_API_TARGET || "http://localhost:8080";

  return {
    plugins: [react(), tailwindcss()],
    build: {
      outDir: "dist",
      emptyOutDir: true,
      assetsDir: "assets",
      rollupOptions: input ? { input } : undefined,
    },
    server: {
      port: 5173,
      proxy: {
        "/api": apiTarget,
        "/admin/api": apiTarget,
      },
    },
  };
});
