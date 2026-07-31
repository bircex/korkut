import { test, expect, type Page } from "@playwright/test";
import { assertNoSeriousA11yViolations } from "./support";

// Drawer e2e (DRAWER_ROADMAP Faz 6): sourced bir sohbet açılır, kaynak chip'i İlgili Kararlar
// drawer'ını açar, kart drawer içi tam metne gider; editör drawer'ı ile tek-drawer kuralı
// korunur. Geniş ekranda drawer'lar push (complementary), dar ekranda overlay (dialog) açılır;
// aynı spec üç viewport projesinde de koşar ve beklenen rolü viewport'tan türetir.

const USER = {
  username: "av", email: "av@example.com", full_name: "Av. Test",
  bar_registry_no: null, role: "owner", office: "Test Bürosu",
};

const SOURCES = [
  {
    kind: "ictihat", source: "yargitay", id: "k-1",
    title: "Yargıtay Ceza Genel Kurulu 2015/89 E. 2018/140 K.",
    date: "2018-04-02", case_number: "2015/89", decision_number: "2018/140",
    court: "Yargıtay", chamber: "Ceza Genel Kurulu",
    url: "https://example.org/k1", snippet: "Hakaret suçunun unsurları değerlendirilmiştir.",
  },
  {
    kind: "ictihat", source: "yargitay", id: "k-2",
    title: "Yargıtay 15. Ceza Dairesi 2013/24590 E. 2013/18178 K.",
    date: "2013-11-20", snippet: "Sinkaflı sözlerle hakaret suç sayılmıştır.",
  },
  { kind: "mevzuat", source: "mevzuat", id: "5237", title: "Türk Ceza Kanunu m.125" },
];

const SESSION = {
  id: "s1", title: "içtihat testi", matter_id: null,
  created_by_name: "av", updated_at: "2026-07-31T10:00:00Z",
};

async function stubSignedInChat(page: Page) {
  // Drawer'ın giriş animasyonu (opacity fade) sırasında axe renkleri blend'li okuyup sahte
  // kontrast ihlali üretir; prefers-reduced-motion animasyonu kapatır (CSS buna saygılıdır).
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.route("**/api/me", (r) =>
    r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(USER) }));
  await page.route("**/api/config", (r) =>
    r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ demo_enabled: false }) }));
  await page.route("**/api/coverage", (r) =>
    r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total_decisions: 1000, by_court: [], decision_sources: [] }) }));
  await page.route("**/api/tasks**", (r) =>
    r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ items: [] }) }));
  await page.route("**/api/documents", (r) =>
    r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([]) }));
  await page.route("**/api/sessions?*", (r) =>
    r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ items: [SESSION], total: 1 }) }));
  await page.route("**/api/sessions/s1", (r) =>
    r.fulfill({
      status: 200, contentType: "application/json",
      body: JSON.stringify({
        ...SESSION,
        messages: [
          { role: "user", content: "hakaret emsalleri getir", sources: null },
          { role: "assistant", content: "Emsaller aşağıdadır.", sources: SOURCES },
        ],
      }),
    }));
  await page.route("**/api/sessions/s1/document", (r) =>
    r.fulfill({
      status: 200, contentType: "application/json",
      body: JSON.stringify({ id: "d1", title: "Taslak", content_md: "# Taslak", status: "draft", versions: [], pending: null }),
    }));
  await page.route("**/api/legal/documents**", (r) => {
    const url = new URL(r.request().url());
    return url.searchParams.get("id") === "k-1"
      ? r.fulfill({
          status: 200, contentType: "application/json",
          body: JSON.stringify({ source: "yargitay", id: "k-1", content_md: "# İçtihat Metni\n\nGerekçe paragrafı." }),
        })
      : r.fulfill({ status: 404, contentType: "application/json", body: JSON.stringify({ detail: "Belge getirilemedi." }) });
  });
}

// Kaynaklar drawer'ı 1024px altında, editör drawer'ı 1200px altında overlay'e düşer.
function drawerRole(page: Page, overlayBelow: number): "complementary" | "dialog" {
  return (page.viewportSize()?.width ?? 1280) >= overlayBelow ? "complementary" : "dialog";
}

test.describe("İlgili Kararlar drawer'ı", () => {
  test("chip → kart listesi → tam metin → hata fallback akışı", async ({ page }) => {
    await stubSignedInChat(page);
    await page.goto("/sohbet/s1");

    // Persist yolundan gelen chip'ler.
    await expect(page.getByRole("button", { name: "3 kaynak" })).toBeVisible();
    await page.getByRole("button", { name: "Kaynaklar (3)" }).click();

    const drawer = page.getByRole(drawerRole(page, 1024), { name: "İlgili Kararlar (2)" });
    await expect(drawer).toBeVisible();
    await expect(drawer.getByText("İçtihat")).toHaveCount(2);
    await expect(drawer.getByRole("heading", { name: "Mevzuat" })).toBeVisible();
    await assertNoSeriousA11yViolations(page, { include: ".drawer-anim" });

    // Kart → drawer içi tam metin.
    await drawer.getByRole("button", { name: SOURCES[0].title }).click();
    await expect(drawer.getByText("Gerekçe paragrafı.")).toBeVisible();
    await expect(drawer.getByText(/2015\/89 E\. · 2018\/140 K\./)).toBeVisible(); // künye satırı
    await assertNoSeriousA11yViolations(page, { include: ".drawer-anim" });

    // Listeye dön → getirilemeyen kart hata kutusu gösterir.
    await drawer.getByRole("button", { name: /Listeye dön/ }).click();
    await drawer.getByRole("button", { name: SOURCES[1].title }).click();
    await expect(drawer.getByText(/Belge getirilemedi/)).toBeVisible();

    // Kapat.
    await drawer.getByRole("button", { name: "Kapat" }).click();
    await expect(page.getByRole("dialog")).toHaveCount(0);
  });

  test("tek drawer kuralı: editör ve kararlar birbirini kapatır", async ({ page }) => {
    const width = page.viewportSize()?.width ?? 1280;
    test.skip(width < 1200, "dar ekranda editör overlay chat'i kapatır; geçiş masaüstü davranışıdır");

    await stubSignedInChat(page);
    await page.goto("/sohbet/s1");

    // Editörü aç (push modda sohbet görünür kalır).
    await page.getByRole("button", { name: "Editör" }).click();
    await expect(page).toHaveURL(/\/sohbet\/s1\/belge$/);
    await expect(page.getByRole("complementary", { name: "Belge editörü" })).toBeVisible();

    // Kaynak chip'i editörü kapatıp kararları açar.
    await page.getByRole("button", { name: "3 kaynak" }).click();
    await expect(page).toHaveURL(/\/sohbet\/s1$/);
    await expect(page.getByRole("complementary", { name: "İlgili Kararlar (2)" })).toBeVisible();

    // Editör toggle'ı kararları kapatıp editörü açar.
    await page.getByRole("button", { name: "Editör" }).click();
    await expect(page.getByRole("complementary", { name: "Belge editörü" })).toBeVisible();
    await expect(page.getByRole("complementary", { name: "İlgili Kararlar (2)" })).toHaveCount(0);
  });
});
