import { type Page, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

// Shared e2e helpers (KDM-029).

/**
 * Stub the API so screens render deterministically without a live backend.
 * By default the visitor is signed OUT (so the auth screen shows).
 */
export async function stubApi(page: Page, opts: { google?: boolean; setupRequired?: boolean } = {}) {
  await page.route("**/api/me", (r) => r.fulfill({ status: 401, body: "{}", contentType: "application/json" }));
  await page.route("**/api/config", (r) =>
    r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ google_configured: opts.google ?? true }) }),
  );
  await page.route("**/api/setup", (r) =>
    r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ setup_required: opts.setupRequired ?? false }) }),
  );
}

/** Fail the test on any critical/serious axe violation (acceptance criterion 1).
 * `include` narrows the scan to one region (e.g. a drawer) so a feature spec asserts its own
 * surface without re-reporting pre-existing shell issues. */
export async function assertNoSeriousA11yViolations(page: Page, opts: { include?: string } = {}) {
  let builder = new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"]);
  if (opts.include) builder = builder.include(opts.include);
  const results = await builder.analyze();
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
