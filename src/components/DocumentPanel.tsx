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
  // Son kaydedilen markdown — dirty kıyası closure'lardan okunduğu için ref.
  const savedRef = useRef("");

  // Reload on session change AND whenever the chat drafts into this editor (refreshKey bumps).
  useEffect(() => {
    api
      .getDocument(sessionId)
      .then((d) => {
        setDoc(d);
        setTitle(d.title);
        setContent(d.content_md || ""); // re-feeds the editor via the `value` prop
        savedRef.current = d.content_md || "";
        onDirtyChange?.(false);
      })
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

  return (
    <DocumentEditor
      value={content}
      title={title}
      titleEditable
      onTitleChange={setTitle}
      onClose={onClose}
      onSave={save}
      onChange={(markdown) => onDirtyChange?.(markdown !== savedRef.current)}
      exportTitle={title}
      headerExtra={
        savedAt ? <span className="shrink-0 text-[11px] text-zinc-400">{savedAt}</span> : null
      }
    />
  );
}
