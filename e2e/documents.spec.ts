import { test, expect, type Page } from "@playwright/test";
import { assertNoSeriousA11yViolations } from "./support";

// Secure-document UI e2e (KDM-031): quarantine-aware states, denied preview for
// non-clean files, linking + destructive unlink, retry, mobile, axe. Runs against
// the isolated documents harness with stubbed /api/documents.

type Doc = {
  id: string;
  original_filename: string;
  content_type: string;
  size: number;
  status: "quarantine" | "clean" | "rejected" | "tombstoned";
  version: number;
  extraction_ready?: boolean;
};

const DOCS: Doc[] = [
  { id: "d-clean", original_filename: "sozlesme.pdf", content_type: "application/pdf", size: 20480, status: "clean", version: 2, extraction_ready: true },
  { id: "d-pending", original_filename: "tarama-bekliyor.pdf", content_type: "application/pdf", size: 10240, status: "quarantine", version: 1 },
  { id: "d-rejected", original_filename: "virus.pdf", content_type: "application/pdf", size: 512, status: "rejected", version: 1 },
];

async function stubDocuments(page: Page, docs: Doc[] = DOCS) {
  await page.route("**/api/documents", (r) => {
    if (r.request().method() === "GET") {
      return r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(docs) });
    }
    return r.continue();
  });
  await page.route("**/api/documents/*/download", (r) => {
    const clean = r.request().url().includes("d-clean");
    return clean
      ? r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ url: "https://minio.local/clean/abc?X-Amz-Expires=60" }) })
      : r.fulfill({ status: 409, contentType: "application/json", body: JSON.stringify({ detail: "Belge henüz taranmadı" }) });
  });
}

test.describe("Documents list — quarantine states", () => {
  test("shows each state and only offers preview on a clean file", async ({ page }) => {
    await stubDocuments(page);
    await page.goto("/documents-harness.html");

    await expect(page.getByText("sozlesme.pdf")).toBeVisible();
    await expect(page.getByText("Hazır")).toBeVisible(); // clean
    await expect(page.getByText("Tarama bekliyor")).toBeVisible(); // quarantine
    await expect(page.getByText("Reddedildi")).toBeVisible(); // rejected

    // Exactly one "Önizle" (preview) button — the clean document only.
    await expect(page.getByRole("button", { name: "Önizle" })).toHaveCount(1);
    await assertNoSeriousA11yViolations(page);
  });

  test("pending/rejected rows expose no preview affordance at all", async ({ page }) => {
    await stubDocuments(page, [DOCS[1], DOCS[2]]); // pending + rejected only
    await page.goto("/documents-harness.html");
    await expect(page.getByText("Tarama bekliyor")).toBeVisible();
    await expect(page.getByRole("button", { name: "Önizle" })).toHaveCount(0);
  });
});

test.describe("Denied download", () => {
  test("a non-clean presign is refused and surfaced (defence in depth)", async ({ page }) => {
    // Force a clean-looking row whose presign the backend still refuses.
    const doc: Doc = { ...DOCS[0], id: "d-clean" };
    await page.route("**/api/documents", (r) => r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([doc]) }));
    await page.route("**/api/documents/*/download", (r) =>
      r.fulfill({ status: 409, contentType: "application/json", body: JSON.stringify({ detail: "Belge henüz taranmadı" }) }),
    );
    await page.goto("/documents-harness.html");
    await page.getByRole("button", { name: "Önizle" }).click();
    await expect(page.getByRole("alert")).toContainText("taranmadı");
  });
});

test.describe("Linking + destructive delete", () => {
  test("delete asks for confirmation and reports the result", async ({ page }) => {
    await stubDocuments(page, [DOCS[0]]);
    let deleted = false;
    await page.route("**/api/documents/d-clean", (r) => {
      deleted = true;
      return r.fulfill({ status: 204, body: "" });
    });
    // After delete, the list is empty.
    await page.route("**/api/documents", (r) =>
      r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(deleted ? [] : [DOCS[0]]) }),
    );

    await page.goto("/documents-harness.html");
    await page.getByRole("button", { name: "Sil" }).click();

    // Confirmation dialog (not an immediate delete).
    const dialog = page.getByRole("dialog", { name: "Belgeyi sil" });
    await expect(dialog).toBeVisible();
    await assertNoSeriousA11yViolations(page);
    await dialog.getByRole("button", { name: "Sil" }).click();

    await expect(page.getByRole("status")).toContainText("silindi"); // result feedback
    await expect(page.getByText("Henüz belge yok")).toBeVisible();
  });

  test("linking a document to a matter is possible from the row", async ({ page }) => {
    await stubDocuments(page, [DOCS[0]]);
    await page.route("**/api/documents/d-clean/versions", (r) =>
      r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([{ version: 2, checksum_sha256: "abc123def456", parser_version: "plaintext-v1", created_at: "2026-07-19" }]) }),
    );
    let linkBody = "";
    await page.route("**/api/documents/d-clean/links", (r) => {
      linkBody = r.request().postData() ?? "";
      return r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ id: "rel-1" }) });
    });

    await page.goto("/documents-harness.html");
    await page.getByRole("button", { name: "Bağla" }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();
    await assertNoSeriousA11yViolations(page);

    await dialog.getByRole("textbox", { name: "Kayıt no (ID)" }).fill("11111111-1111-1111-1111-111111111111");
    await dialog.getByRole("button", { name: "Bağla" }).click();
    await expect(page.getByRole("status")).toContainText("Bağlantı eklendi");
    expect(linkBody).toContain("matter");
  });
});

test.describe("Retry", () => {
  test("a failed list load offers retry and recovers", async ({ page }) => {
    let attempt = 0;
    await page.route("**/api/documents", (r) => {
      attempt += 1;
      return attempt === 1
        ? r.fulfill({ status: 500, contentType: "application/json", body: JSON.stringify({ detail: "Sunucu hatası" }) })
        : r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([DOCS[0]]) });
    });
    await page.goto("/documents-harness.html");
    await expect(page.getByRole("alert")).toBeVisible();
    await page.getByRole("button", { name: "Tekrar dene" }).click();
    await expect(page.getByText("sozlesme.pdf")).toBeVisible();
  });
});
