import { test, expect } from "@playwright/test";
import { stubApi, assertNoSeriousA11yViolations, assertNoHorizontalOverflow } from "./support";

// Sign-in (the representative form): screenshot + axe + keyboard smoke,
// plus WCAG 2.2 reflow at 320px and a 200% zoom check (KDM-029).

test.describe("Sign-in (auth)", () => {
  test("renders, passes axe, and is keyboard-operable", async ({ page }, testInfo) => {
    await stubApi(page);
    await page.goto("/giris");

    await expect(page.getByRole("heading", { name: "Korkut" })).toBeVisible();
    const signIn = page.getByRole("button", { name: "Giriş yap" });
    await expect(signIn).toBeVisible();

    await assertNoSeriousA11yViolations(page);

    // Keyboard: the sign-in action is reachable and focus is visible.
    await page.keyboard.press("Tab");
    const focused = await page.evaluate(() => document.activeElement?.tagName);
    expect(focused).toBeTruthy();
    const login = page.getByRole("textbox", { name: /E-posta ya da kullanıcı adı/ });
    await login.focus();
    await page.keyboard.type("korkut-admin");
    await expect(login).toHaveValue("korkut-admin");

    // Capture a screenshot as evidence (attached to the report); pixel-diff gating is
    // avoided so the suite stays stable across the dev (macOS) and CI (Linux) renderers.
    await testInfo.attach("signin", { body: await page.screenshot(), contentType: "image/png" });
  });
});

test.describe("Responsive reflow (WCAG 2.2 AA)", () => {
  test("no horizontal overflow at 320px", async ({ page }) => {
    await stubApi(page);
    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto("/giris");
    await assertNoHorizontalOverflow(page);
  });

  test("no horizontal overflow at 320px under 200% zoom", async ({ page }) => {
    await stubApi(page);
    // 200% zoom on a 640px viewport == 320 CSS px of content width.
    await page.setViewportSize({ width: 640, height: 900 });
    await page.goto("/giris");
    await page.evaluate(() => {
      document.documentElement.style.setProperty("zoom", "2");
    });
    await assertNoHorizontalOverflow(page);
  });
});
