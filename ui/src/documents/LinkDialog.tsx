import { useEffect, useState } from "react";
import { ApiError } from "../api";
import { Button, Dialog, ConfirmDialog, Field, useToast, Spinner } from "../a11y";
import { documentsApi, type DocumentVersion, type LinkTarget } from "./api";
import type { DocumentDto } from "./status";

// Linking + version/provenance panel (KDM-031). Link a document to a client/matter/
// party; unlink is destructive → confirmation + feedback. Version history shows the
// source provenance (checksum + parser version) from the backend (KDM-027/028).

export function LinkDialog({ doc, onClose, onChanged }: { doc: DocumentDto; onClose: () => void; onChanged: () => void }) {
  const [target, setTarget] = useState<LinkTarget>("matter");
  const [targetId, setTargetId] = useState("");
  const [busy, setBusy] = useState(false);
  const [versions, setVersions] = useState<DocumentVersion[] | null>(null);
  const [confirmUnlink, setConfirmUnlink] = useState<{ target: LinkTarget; id: string } | null>(null);
  const { notify } = useToast();

  useEffect(() => {
    documentsApi.versions(doc.id).then(setVersions).catch(() => setVersions([]));
  }, [doc.id]);

  async function link(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      await documentsApi.link(doc.id, target, targetId.trim());
      notify("Bağlantı eklendi", "success");
      setTargetId("");
      onChanged();
    } catch (err) {
      notify(err instanceof ApiError ? err.message : "Bağlanamadı", "error");
    } finally {
      setBusy(false);
    }
  }

  async function unlink() {
    if (!confirmUnlink) return;
    try {
      await documentsApi.unlink(doc.id, confirmUnlink.target, confirmUnlink.id);
      notify("Bağlantı kaldırıldı", "success");
      onChanged();
    } catch (err) {
      notify(err instanceof ApiError ? err.message : "Kaldırılamadı", "error");
    } finally {
      setConfirmUnlink(null);
    }
  }

  return (
    <Dialog open onClose={onClose} title={`Bağlantılar — ${doc.original_filename}`}>
      <form onSubmit={link} className="flex flex-col gap-3">
        <label className="flex flex-col gap-1.5 text-[13px] text-zinc-500">
          Tür
          <select
            value={target}
            onChange={(e) => setTarget(e.target.value as LinkTarget)}
            className="rounded-[10px] border border-zinc-300 bg-white px-3 py-2.5 text-[15px] text-zinc-900 outline-none focus-visible:ring-2 focus-visible:ring-zinc-900/20"
          >
            <option value="matter">Dosya</option>
            <option value="client">Müvekkil</option>
            <option value="party">Taraf</option>
          </select>
        </label>
        <Field label="Kayıt no (ID)" value={targetId} onChange={(e) => setTargetId(e.target.value)} required />
        <Button type="submit" disabled={busy || !targetId.trim()}>
          {busy ? "Bağlanıyor…" : "Bağla"}
        </Button>
      </form>

      <div className="mt-4">
        <h3 className="m-0 mb-1 text-[13px] font-semibold text-zinc-700">Sürüm geçmişi</h3>
        {versions === null ? (
          <Spinner label="Yükleniyor" />
        ) : versions.length === 0 ? (
          <p className="m-0 text-[13px] text-zinc-500">Sürüm kaydı yok.</p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-1 p-0">
            {versions.map((v) => (
              <li key={v.version} className="text-[12px] text-zinc-500">
                v{v.version} · {v.parser_version ?? "—"} · <span className="font-mono">{v.checksum_sha256.slice(0, 12)}…</span>
              </li>
            ))}
          </ul>
        )}
      </div>

      {confirmUnlink && (
        <ConfirmDialog
          open
          onCancel={() => setConfirmUnlink(null)}
          onConfirm={unlink}
          title="Bağlantıyı kaldır"
          message="Bu bağlantı kaldırılacak."
          confirmLabel="Kaldır"
          destructive
        />
      )}
    </Dialog>
  );
}
