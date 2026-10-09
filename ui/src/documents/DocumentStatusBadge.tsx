import { type DocumentStatus, statusLabel } from "./status";

// Status pill (KDM-031). Colour is paired with text so it never relies on colour
// alone (WCAG 1.4.1), and a spinner announces in-flight work politely.

const TONE: Record<DocumentStatus, string> = {
  uploading: "border-zinc-200 bg-zinc-50 text-zinc-600",
  quarantine: "border-amber-200 bg-amber-50 text-amber-800",
  clean: "border-green-200 bg-green-50 text-green-700",
  rejected: "border-red-200 bg-red-50 text-red-700",
  processing: "border-blue-200 bg-blue-50 text-blue-700",
  failed: "border-orange-200 bg-orange-50 text-orange-800",
  tombstoned: "border-zinc-200 bg-zinc-100 text-zinc-500",
};

const BUSY: DocumentStatus[] = ["uploading", "quarantine", "processing"];

export function DocumentStatusBadge({ status }: { status: DocumentStatus }) {
  const busy = BUSY.includes(status);
  return (
    <span
      role={busy ? "status" : undefined}
      aria-live={busy ? "polite" : undefined}
      className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-[12px] font-medium ${TONE[status]}`}
    >
      {busy && (
        <span aria-hidden className="h-2.5 w-2.5 animate-spin rounded-full border-2 border-current border-t-transparent motion-reduce:animate-none" />
      )}
      {statusLabel(status)}
    </span>
  );
}
