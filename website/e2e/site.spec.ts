import { test, expect, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

// Public-website baseline (KDM-032): routes/links resolve, CSP is present and strict,
// no external assets load, layout reflows at 320px/200% zoom, and axe finds no
// critical/serious violations.

// /demo artık uygulamanın kayıt sayfasına anlık meta-refresh ile yönlenir; sayfa-düzeyi
// denetimler gerçek sayfalara uygulanır, /demo ayrı bir yönlendirme testiyle doğrulanır.
const PAGES = ["/", "/guvenlik/", "/kvkk/", "/gizlilik/", "/kosullar/"];

async function assertNoSeriousA11y(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"]).analyze();
  const blocking = results.violations.filter((v) => v.impact === "critical" || v.impact === "serious");
  const summary = blocking.map((v) => `${v.id} (${v.impact}): ${v.nodes.length}`).join("\n");
  expect(blocking, `axe violations:\n${summary}`).toEqual([]);
}

for (const path of PAGES) {
  test.describe(`page ${path}`, () => {
    test("passes axe (no critical/serious)", async ({ page }) => {
      await page.goto(path);
      await assertNoSeriousA11y(page);
    });

    test("ships a strict CSP meta with no unsafe-inline scripts", async ({ page }) => {
      await page.goto(path);
      const csp = await page.locator('meta[http-equiv="Content-Security-Policy"]').getAttribute("content");
      expect(csp).toBeTruthy();
      expect(csp).toContain("default-src 'self'");
      expect(csp).toContain("object-src 'none'");
      expect(csp).toContain("frame-ancestors 'none'");
      expect(csp).toContain("script-src 'self'");
      // script-src must not permit inline scripts.
      expect(csp).not.toMatch(/script-src[^;]*'unsafe-inline'/);
    });

    test("loads no external (cross-origin) assets", async ({ page, baseURL }) => {
      const external: string[] = [];
      page.on("request", (req) => {
        const url = req.url();
        if (!url.startsWith(baseURL!) && !url.startsWith("data:")) external.push(url);
      });
      await page.goto(path, { waitUntil: "networkidle" });
      expect(external, `external requests:\n${external.join("\n")}`).toEqual([]);
    });

    test("no horizontal overflow at 320px", async ({ page }) => {
      await page.setViewportSize({ width: 320, height: 720 });
      await page.goto(path);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow).toBeLessThanOrEqual(1);
    });

    test("no horizontal overflow at 320px under 200% zoom", async ({ page }) => {
      await page.setViewportSize({ width: 640, height: 900 });
      await page.goto(path);
      await page.evaluate(() => document.documentElement.style.setProperty("zoom", "2"));
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow).toBeLessThanOrEqual(1);
    });
  });
}

test.describe("redirects", () => {
  test("/demo forwards old links to the install section", async ({ page }) => {
    // Navigasyonsuz doğrula (anlık meta refresh sayfayı hemen terk eder).
    const res = await page.request.get("/demo");
    expect(res.status()).toBeLessThan(400);
    const html = await res.text();
    expect(html).toContain('http-equiv="refresh"');
    expect(html).toContain("/#kurulum");
  });
});

test.describe("routes & links", () => {
  test("every internal link resolves (no broken routes)", async ({ page, baseURL }) => {
    const checked = new Set<string>();
    for (const path of PAGES) {
      await page.goto(path);
      const hrefs = await page.$$eval("a[href]", (as) => as.map((a) => (a as HTMLAnchorElement).getAttribute("href") ?? ""));
      for (const href of hrefs) {
        if (!href || href.startsWith("#") || href.startsWith("mailto:") || href.startsWith("tel:")) continue;
        // Only crawl internal, same-origin page links.
        const url = new URL(href, baseURL);
        if (url.origin !== new URL(baseURL!).origin) continue;
        const key = url.pathname;
        if (checked.has(key)) continue;
        checked.add(key);
        const res = await page.request.get(url.toString());
        expect(res.status(), `broken link: ${url.pathname}`).toBeLessThan(400);
      }
    }
    expect(checked.size).toBeGreaterThan(0);
  });
});

test("screenshot evidence of the landing page", async ({ page }, testInfo) => {
  await page.goto("/");
  await testInfo.attach("landing", { body: await page.screenshot({ fullPage: true }), contentType: "image/png" });
});
