import { type Page, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

// Shared e2e helpers (KDM-029).

/**
 * Stub the API so screens render deterministically without a live backend.
 * By default the visitor is signed OUT (so the auth screen shows).
 */
export async function stubApi(page: Page, opts: { pending?: string; demo?: boolean } = {}) {
  await page.route("**/api/me", (r) => r.fulfill({ status: 401, body: "{}", contentType: "application/json" }));
  await page.route("**/api/config", (r) =>
    r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ demo_enabled: opts.demo ?? true }) }),
  );
  await page.route("**/api/signup/pending", (r) =>
    opts.pending
      ? r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ email: opts.pending }) })
      : r.fulfill({ status: 401, body: "{}", contentType: "application/json" }),
  );
}

/** Fail the test on any critical/serious axe violation (acceptance criterion 1). */
export async function assertNoSeriousA11yViolations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"]).analyze();
  const blocking = results.violations.filter((v) => v.impact === "critical" || v.impact === "serious");
  const summary = blocking.map((v) => `${v.id} (${v.impact}): ${v.nodes.length} node(s)`).join("\n");
  expect(blocking, `Critical/serious axe violations:\n${summary}`).toEqual([]);
}

/** WCAG 2.2 reflow (1.4.10): no horizontal scrolling at 320 CSS px. */
export async function assertNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => {
    const el = document.documentElement;
    // Allow a 1px rounding slack.
    return el.scrollWidth - el.clientWidth;
  });
  expect(overflow, "content overflows horizontally at this viewport").toBeLessThanOrEqual(1);
}
