import { test, expect } from "@playwright/test";
import { assertNoSeriousA11yViolations } from "./support";

// .NET auth contract e2e (KDM-030): local login → policy-driven TOTP → session
// re-probe, all cookie-based with no token in storage. The .NET endpoints are stubbed
// so the flow is deterministic without a live backend.

test.describe("Local sign-in + TOTP", () => {
  test("email/password then TOTP re-probes the session, cookie-only", async ({ page }) => {

    const meCalls: string[] = [];
    await page.route("**/api/me", (r) => {
      meCalls.push(r.request().url());
      return r.fulfill({ status: 401, contentType: "application/json", body: "{}" });
    });
    await page.route("**/api/config", (r) => r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ demo_enabled: false }) }));
    await page.route("**/api/auth/login", (r) => r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ totp_required: true }) }));
    let totpBody = "";
    await page.route("**/api/auth/2fa", async (r) => {
      totpBody = r.request().postData() ?? "";
      return r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ ok: true }) });
    });

    await page.goto("/giris");

    // Step 1 — credentials. The form is accessible and keyboard-operable.
    const email = page.getByRole("textbox", { name: "E-posta" });
    const password = page.getByLabel("Parola");
    await expect(email).toBeVisible();
    await assertNoSeriousA11yViolations(page);

    await email.fill("av@example.com");
    await password.fill("dogru-parola");
    await page.getByRole("button", { name: "Giriş yap" }).click();

    // Step 2 — TOTP challenge appears.
    const code = page.getByRole("textbox", { name: "Doğrulama kodu" });
    await expect(code).toBeVisible();
    await assertNoSeriousA11yViolations(page);

    // Verifying the code re-probes /api/me (signalAuthChanged → checkSession).
    await code.fill("123456");
    await page.getByRole("button", { name: "Doğrula" }).click();

    // The 2FA code was sent, and the app re-probed the session (signalAuthChanged).
    await expect.poll(() => meCalls.length).toBeGreaterThanOrEqual(2);
    expect(totpBody).toContain("123456");

    // No token was ever written to storage (cookie-only session).
    const stored = await page.evaluate(() => ({
      ls: JSON.stringify(window.localStorage),
      keys: Object.keys(window.localStorage),
    }));
    expect(stored.ls).not.toMatch(/token|jwt|bearer/i);
  });

  test("switching to the recovery-code field keeps the form accessible", async ({ page }) => {
    await page.route("**/api/me", (r) => r.fulfill({ status: 401, body: "{}", contentType: "application/json" }));
    await page.route("**/api/config", (r) => r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ demo_enabled: false }) }));
    await page.route("**/api/auth/login", (r) => r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ totp_required: true }) }));

    await page.goto("/giris");
    await page.getByRole("textbox", { name: "E-posta" }).fill("av@example.com");
    await page.getByLabel("Parola").fill("p");
    await page.getByRole("button", { name: "Giriş yap" }).click();

    await expect(page.getByRole("textbox", { name: "Doğrulama kodu" })).toBeVisible();
    await page.getByRole("button", { name: "Kurtarma kodu kullan" }).click();
    await expect(page.getByRole("textbox", { name: "Kurtarma kodu" })).toBeVisible();
    await assertNoSeriousA11yViolations(page);
  });

  test("a wrong password surfaces an accessible error", async ({ page }) => {
    await page.route("**/api/me", (r) => r.fulfill({ status: 401, body: "{}", contentType: "application/json" }));
    await page.route("**/api/config", (r) => r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ demo_enabled: false }) }));
    await page.route("**/api/auth/login", (r) =>
      r.fulfill({ status: 401, contentType: "application/json", body: JSON.stringify({ detail: "E-posta veya parola hatalı" }) }),
    );

    await page.goto("/giris");
    await page.getByRole("textbox", { name: "E-posta" }).fill("av@example.com");
    await page.getByLabel("Parola").fill("yanlis");
    await page.getByRole("button", { name: "Giriş yap" }).click();

    await expect(page.getByRole("alert")).toHaveText(/hatalı/);
  });
});
