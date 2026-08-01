import { useEffect, useRef, useState } from "react";
import { api, type DocumentData } from "../api";
import { DocumentEditor } from "./DocumentEditor";

export function DocumentPanel({
  sessionId,
  refreshKey,
  onClose,
  onDirtyChange,
}: {
  sessionId: number;
  refreshKey: number;
  onClose?: () => void;
  /** Editördeki içerik son kaydedilenden saptığında/eşitlendiğinde bildirir (drawer kapatma onayı için). */
  onDirtyChange?: (dirty: boolean) => void;
}) {
  const [doc, setDoc] = useState<DocumentData | null>(null);
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");
  const [savedAt, setSavedAt] = useState("");
  const [deciding, setDeciding] = useState(false); // kabul/geri-al isteği uçuşta
  // Son kaydedilen markdown — dirty kıyası closure'lardan okunduğu için ref.
  const savedRef = useRef("");

  // Bekleyen taslak varken editör taslağı GÖSTERİR (avukat okuyup karar verir); onay bandı
  // Kabul Et / Geri Al sunar. Kabul sunucudaki bekleyen içeriği işler, Geri Al son kabul edilmiş
  // içeriğe döner. Bu moddayken Kaydet gizlenir ve dirty takibi kapalıdır.
  const pending = doc?.pending ?? null;

  function applyDoc(d: DocumentData) {
    setDoc(d);
    setTitle(d.title);
    const shown = d.pending ? d.pending.content_md : d.content_md || "";
    setContent(shown); // re-feeds the editor via the `value` prop
    savedRef.current = shown;
    onDirtyChange?.(false);
  }

  // Reload on session change AND whenever the chat drafts into this editor (refreshKey bumps).
  useEffect(() => {
    api
      .getDocument(sessionId)
      .then(applyDoc)
      .catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId, refreshKey]);

  async function save(markdown: string) {
    if (!doc) return;
    const d = await api.saveDocument(doc.id, title, markdown);
    setDoc(d);
    setSavedAt(new Date().toLocaleTimeString("tr-TR"));
    savedRef.current = markdown;
    onDirtyChange?.(false);
  }

  async function decide(action: "accept" | "revert") {
    if (!doc || deciding) return;
    setDeciding(true);
    try {
      const d = action === "accept" ? await api.acceptEdit(doc.id) : await api.revertEdit(doc.id);
      applyDoc(d);
      if (action === "accept") setSavedAt(new Date().toLocaleTimeString("tr-TR"));
    } catch {
      /* band açık kalır; kullanıcı yeniden dener */
    } finally {
      setDeciding(false);
    }
  }

  return (
    <div className="flex h-full min-w-0 flex-1 flex-col">
      {pending && (
        <div className="flex flex-wrap items-center gap-2 border-b border-amber-200 bg-amber-50 px-4 py-2.5">
          <div className="min-w-0 flex-1">
            <div className="text-[13px] font-semibold text-amber-900">Korkut'un taslağı onay bekliyor</div>
            {pending.summary && (
              <div className="truncate text-[12px] text-amber-800/80">{pending.summary}</div>
            )}
          </div>
          <button
            onClick={() => decide("revert")}
            disabled={deciding}
            className="shrink-0 rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-[13px] font-medium text-amber-900 hover:bg-amber-100 disabled:opacity-50"
          >
            Geri Al
          </button>
          <button
            onClick={() => decide("accept")}
            disabled={deciding}
            className="shrink-0 rounded-lg bg-zinc-900 px-3.5 py-1.5 text-[13px] font-semibold text-white hover:bg-zinc-700 disabled:opacity-50"
          >
            {deciding ? "…" : "Kabul Et"}
          </button>
        </div>
      )}
      <DocumentEditor
        value={content}
        title={title}
        titleEditable={!pending}
        onTitleChange={setTitle}
        onClose={onClose}
        onSave={pending ? undefined : save}
        onChange={(markdown) => {
          if (!pending) onDirtyChange?.(markdown !== savedRef.current);
        }}
        exportTitle={title}
        headerExtra={
          savedAt && !pending ? (
            <span className="shrink-0 text-[11px] text-zinc-400">{savedAt}</span>
          ) : null
        }
      />
    </div>
  );
}
