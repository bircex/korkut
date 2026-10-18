import { useState } from "react";
import { ApiError } from "../api";
import { ConfirmDialog, useToast } from "../a11y";
import { DocumentStatusBadge } from "./DocumentStatusBadge";
import { documentsApi } from "./api";
import { capabilities, uiStatus, type DocumentDto } from "./status";
import { P, useCan } from "../permissions";

// One document row (KDM-031): filename truncates, status is explicit, and preview/
// download are offered ONLY when capabilities permit — a pending/rejected file has
// no preview affordance at all. Delete is destructive → confirmation + feedback.

export function DocumentRow({
  doc,
  onChanged,
  onOpenLinks,
}: {
  doc: DocumentDto;
  onChanged: () => void;
  onOpenLinks: (doc: DocumentDto) => void;
}) {
  const status = uiStatus(doc);
  const caps = capabilities(status);
  // What the file allows, narrowed by what the member may do.
  const mayWrite = useCan(P.documentsWrite);
  const mayDelete = useCan(P.documentsDelete);
  const canLink = caps.canLink && mayWrite;
  const canDelete = caps.canDelete && mayDelete;
  const { notify } = useToast();
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [busy, setBusy] = useState(false);

  async function preview() {
    try {
      const { url } = await documentsApi.presign(doc.id);
      window.open(url, "_blank", "noopener,noreferrer");
    } catch (err) {
      // Defence in depth: the backend refuses to presign a non-clean object.
      notify(err instanceof ApiError ? err.message : "Önizleme açılamadı", "error");
    }
  }

  async function remove() {
    setBusy(true);
    try {
      await documentsApi.remove(doc.id);
      notify("Belge silindi", "success");
      onChanged();
    } catch (err) {
      notify(err instanceof ApiError ? err.message : "Silinemedi", "error");
    } finally {
      setBusy(false);
      setConfirmDelete(false);
    }
  }

  return (
    <div className="flex flex-col gap-2 border-b border-zinc-100 py-3 sm:flex-row sm:items-center sm:gap-3">
      <div className="min-w-0 flex-1">
        <p className="m-0 truncate text-[14px] font-medium text-zinc-800" title={doc.original_filename}>
          {doc.original_filename}
        </p>
        <p className="m-0 text-[12px] text-zinc-500">
          v{doc.version} · {(doc.size / 1024).toFixed(0)} KB
        </p>
      </div>
      <DocumentStatusBadge status={status} />
      {/* Actions wrap on mobile so a long filename + toolbar never overlap/overflow. */}
      <div className="flex flex-wrap items-center gap-2">
        {caps.canPreview && (
          <button
            type="button"
            onClick={preview}
            className="rounded-[8px] border border-zinc-300 px-3 py-1.5 text-[13px] font-medium text-zinc-700 outline-none focus-visible:ring-2 focus-visible:ring-zinc-900/20"
          >
            Önizle
          </button>
        )}
        {canLink && (
          <button
            type="button"
            onClick={() => onOpenLinks(doc)}
            className="rounded-[8px] border border-zinc-300 px-3 py-1.5 text-[13px] font-medium text-zinc-700 outline-none focus-visible:ring-2 focus-visible:ring-zinc-900/20"
          >
            Bağla
          </button>
        )}
        {canDelete && (
          <button
            type="button"
            onClick={() => setConfirmDelete(true)}
            className="rounded-[8px] border border-red-200 px-3 py-1.5 text-[13px] font-medium text-red-700 outline-none focus-visible:ring-2 focus-visible:ring-red-500/40"
          >
            Sil
          </button>
        )}
      </div>

      <ConfirmDialog
        open={confirmDelete}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={remove}
        title="Belgeyi sil"
        message={
          <>
            <span className="font-medium">{doc.original_filename}</span> silinecek. Bu işlem geri alınamaz.
          </>
        }
        confirmLabel={busy ? "Siliniyor…" : "Sil"}
        destructive
      />
    </div>
  );
}
