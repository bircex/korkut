import { useCallback, useEffect, useState } from "react";
import { P, useCan } from "../permissions";
import { ToastProvider } from "../a11y";
import { AsyncBoundary, type AsyncStatus } from "../components/AsyncState";
import { DocumentRow } from "./DocumentRow";
import { UploadButton } from "./UploadButton";
import { LinkDialog } from "./LinkDialog";
import { documentsApi } from "./api";
import type { DocumentDto } from "./status";

// The documents experience (KDM-031): upload + quarantine-aware list + linking,
// wrapped in the standard async states (loading/error/empty/permission-denied/
// concurrent-update). Self-contained so it can be dropped into a matter or the
// global library.

function Panel({ matterId }: { matterId?: string }) {
  const [docs, setDocs] = useState<DocumentDto[]>([]);
  const [status, setStatus] = useState<AsyncStatus>("loading");
  const [error, setError] = useState<unknown>(null);
  const [linking, setLinking] = useState<DocumentDto | null>(null);

  // Silent refetch — leaves the current list visible while it revalidates.
  const fetchDocs = useCallback(() => {
    documentsApi
      .list(matterId)
      .then((d) => {
        setDocs(d);
        setStatus("ready");
      })
      .catch((e) => {
        setError(e);
        setStatus("error");
      });
  }, [matterId]);

  // Explicit reload (retry button) — shows the loading state again.
  const reload = useCallback(() => {
    setStatus("loading");
    fetchDocs();
  }, [fetchDocs]);

  // Initial status is already "loading"; the fetch resolves asynchronously.
  useEffect(() => {
    fetchDocs();
  }, [fetchDocs]);

  const canUpload = useCan(P.documentsWrite);
  return (
    <section aria-label="Belgeler" className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h2 className="m-0 text-lg font-bold text-zinc-900">Belgeler</h2>
        {canUpload && <UploadButton matterId={matterId} onUploaded={reload} />}
      </div>

      <AsyncBoundary status={status} error={error} isEmpty={docs.length === 0} emptyTitle="Henüz belge yok" onRetry={reload}>
        <div>
          {docs.map((doc) => (
            <DocumentRow key={doc.id} doc={doc} onChanged={fetchDocs} onOpenLinks={setLinking} />
          ))}
        </div>
      </AsyncBoundary>

      {linking && <LinkDialog doc={linking} onClose={() => setLinking(null)} onChanged={fetchDocs} />}
    </section>
  );
}

export function DocumentsPanel({ matterId }: { matterId?: string }) {
  return (
    <ToastProvider>
      <Panel matterId={matterId} />
    </ToastProvider>
  );
}
