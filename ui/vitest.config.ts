import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// Unit + component test config (KDM-029). jsdom environment; axe runs in-process
// against rendered components so critical/serious a11y violations fail the build.
export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    include: ["src/**/*.test.{ts,tsx}"],
    css: false,
  },
});
