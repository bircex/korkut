// Document lifecycle model for the UI (KDM-031), mirroring the backend
// quarantine→clean/rejected + extraction/tombstone lifecycle (KDM-025/026/027).

export type DocumentStatus = "uploading" | "quarantine" | "clean" | "rejected" | "processing" | "failed" | "tombstoned";

export type DocumentDto = {
  id: string;
  original_filename: string;
  content_type: string;
  size: number;
  status: "quarantine" | "clean" | "rejected" | "tombstoned";
  version: number;
  /** Extraction/OCR readiness for a clean document (KDM-027). */
  extraction_ready?: boolean;
  extraction_failed?: boolean;
  created_at: string;
};

export type Capabilities = {
  canPreview: boolean;
  canDownload: boolean;
  canLink: boolean;
  canDelete: boolean;
};

/**
 * Derive the UI status. A clean document that has no extraction yet is shown as
 * "processing" (OCR running); a clean doc whose extraction failed is "failed" but
 * still downloadable.
 */
export function uiStatus(doc: Pick<DocumentDto, "status" | "extraction_ready" | "extraction_failed">): DocumentStatus {
  if (doc.status === "tombstoned") return "tombstoned";
  if (doc.status === "quarantine") return "quarantine";
  if (doc.status === "rejected") return "rejected";
  // clean
  if (doc.extraction_failed) return "failed";
  if (doc.extraction_ready === false) return "processing";
  return "clean";
}

/**
 * Capabilities are the single source of truth for affordances. Preview/download are
 * allowed ONLY for a clean document (the backend refuses to presign anything else —
 * KDM-026); this mirrors that so the UI never offers an action the API will reject.
 */
export function capabilities(status: DocumentStatus): Capabilities {
  const isClean = status === "clean" || status === "processing" || status === "failed";
  return {
    // Preview needs the bytes served — only a scanned-clean object qualifies.
    canPreview: status === "clean" || status === "failed",
    canDownload: isClean,
    canLink: status !== "tombstoned" && status !== "rejected" && status !== "uploading",
    canDelete: status !== "tombstoned" && status !== "uploading",
  };
}

const LABELS: Record<DocumentStatus, string> = {
  uploading: "Yükleniyor",
  quarantine: "Tarama bekliyor",
  clean: "Hazır",
  rejected: "Reddedildi",
  processing: "İşleniyor (OCR)",
  failed: "Metin çıkarılamadı",
  tombstoned: "Silindi",
};

export function statusLabel(status: DocumentStatus): string {
  return LABELS[status];
}
