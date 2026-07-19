import { defineConfig, devices } from "@playwright/test";

// End-to-end accessibility + responsive baseline (KDM-029). Playwright builds the app
// and serves the production bundle with `vite preview`, then runs axe + keyboard +
// reflow smoke tests across desktop/tablet/mobile viewports. No live backend is
// required — the specs stub /api/* so screens render deterministically.
const PORT = 4173;

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["github"], ["list"]] : [["list"]],
  use: {
    baseURL: `http://localhost:${PORT}`,
    trace: "on-first-retry",
  },
  // Desktop / tablet / mobile VIEWPORTS all on Chromium — the matrix exercises
  // responsive breakpoints, not multiple rendering engines, so CI downloads one browser.
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"], viewport: { width: 1280, height: 800 } } },
    { name: "tablet", use: { ...devices["Desktop Chrome"], viewport: { width: 768, height: 1024 } } },
    { name: "mobile", use: { ...devices["Desktop Chrome"], viewport: { width: 390, height: 844 } } },
  ],
  webServer: {
    command: "npm run build && npm run preview -- --port " + PORT + " --strictPort",
    url: `http://localhost:${PORT}`,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
