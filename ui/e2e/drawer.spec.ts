import { test, expect, type Page } from "@playwright/test";
import { assertNoSeriousA11yViolations } from "./support";

// Drawer e2e (DRAWER_ROADMAP Faz 7): sourced bir sohbet açılır, kaynak chip'i İlgili Kararlar
// drawer'ını açar, kart drawer içi tam metne gider; editör drawer'ı ile tek-drawer kuralı
// korunur. Tüm drawer'lar tek davranışı paylaşır: sol menü hariç tam ekran, dialog semantiği
// (role="dialog"); viewport'a göre değişmez, aynı spec üç viewport projesinde de koşar.

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
  await page.route("**/api/coverage", (r) =>
    r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total_decisions: 1000, by_court: [], decision_sources: [] }) }));
  await page.route("**/api/tasks**", (r) =>
    r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ items: [] }) }));
  // Sohbetin belgesi Belgeler listesinden (conversation_id eşleşmesi) türer — sohbetteki belge
  // chip'inin veri kaynağı budur (header'da artık Editör düğmesi yok).
  await page.route("**/api/draft-documents", (r) =>
    r.fulfill({
      status: 200, contentType: "application/json",
      body: JSON.stringify([
        { id: "d1", conversation_id: "s1", title: "Taslak", kind: null, updated_at: "2026-07-31T10:00:00Z" },
      ]),
    }));
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

test.describe("İlgili Kararlar drawer'ı", () => {
  test("chip → kart listesi → tam metin → hata fallback akışı", async ({ page }) => {
    await stubSignedInChat(page);
    await page.goto("/sohbet/s1");

    // Persist yolundan gelen chip'ler.
    await expect(page.getByRole("button", { name: "3 kaynak" })).toBeVisible();
    await page.getByRole("button", { name: "Kaynaklar (3)" }).click();

    const drawer = page.getByRole("dialog", { name: "İlgili Kararlar (2)" });
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
    await stubSignedInChat(page);
    await page.goto("/sohbet/s1");

    // Editörü belge chip'inden aç (header'da Editör düğmesi yok; DocumentEditor kendi Geri
    // düğmesini getirdiği için editör drawer'ı chrome'suzdur — role="dialog", başlıksız).
    await page.getByRole("button", { name: "Taslak" }).click();
    await expect(page).toHaveURL(/\/sohbet\/s1\/belge$/);
    await expect(page.getByRole("dialog", { name: "Belge editörü" })).toBeVisible();

    // Kaynak chip'i editörü kapatıp kararları açar.
    await page.getByRole("button", { name: "3 kaynak" }).click();
    await expect(page).toHaveURL(/\/sohbet\/s1$/);
    await expect(page.getByRole("dialog", { name: "İlgili Kararlar (2)" })).toBeVisible();

    // Belge chip'i kararları kapatıp editörü açar.
    await page.getByRole("button", { name: "Taslak" }).click();
    await expect(page.getByRole("dialog", { name: "Belge editörü" })).toBeVisible();
    await expect(page.getByRole("dialog", { name: "İlgili Kararlar (2)" })).toHaveCount(0);
  });
});
