import { useEffect, useState } from "react";
import { EditorContent, useEditor } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import { Markdown } from "tiptap-markdown";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { api, ApiError, type DocumentData, type PendingEdit } from "../api";

export function DocumentPanel({
  sessionId,
  onClose,
}: {
  sessionId: number;
  onClose: () => void;
}) {
  const [doc, setDoc] = useState<DocumentData | null>(null);
  const [title, setTitle] = useState("");
  const [pending, setPending] = useState<PendingEdit | null>(null);
  const [instruction, setInstruction] = useState("");
  const [busy, setBusy] = useState(false);
  const [savedAt, setSavedAt] = useState<string>("");

  const editor = useEditor({
    extensions: [StarterKit, Markdown],
    content: "",
    editorProps: { attributes: { class: "prose-doc" } },
  });

  useEffect(() => {
    api
      .getDocument(sessionId)
      .then((d) => {
        setDoc(d);
        setTitle(d.title);
        setPending(d.pending);
        editor?.commands.setContent(d.content_md || "");
      })
      .catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId, editor]);

  function currentMarkdown(): string {
    // tiptap-markdown augments storage at runtime; TS doesn't know the shape.
    const storage = editor?.storage as { markdown?: { getMarkdown?: () => string } } | undefined;
    return storage?.markdown?.getMarkdown?.() ?? "";
  }

  async function save() {
    if (!doc) return;
    setBusy(true);
    try {
      const d = await api.saveDocument(doc.id, title, currentMarkdown());
      setDoc(d);
      setSavedAt(new Date().toLocaleTimeString("tr-TR"));
    } catch {
      /* ignore */
    } finally {
      setBusy(false);
    }
  }

  async function draft() {
    if (!doc) return;
    const instr = instruction.trim();
    setBusy(true);
    setInstruction("");
    try {
      // save the lawyer's current edits first so Korkut works on the latest text
      await api.saveDocument(doc.id, title, currentMarkdown());
      const pe = await api.draftDocument(doc.id, instr);
      setPending(pe);
    } catch (err) {
      const detail = err instanceof ApiError ? err.message : "taslak alınamadı";
      alert(detail);
    } finally {
      setBusy(false);
    }
  }

  async function accept() {
    if (!doc) return;
    setBusy(true);
    try {
      const d = await api.acceptEdit(doc.id);
      setDoc(d);
      setPending(null);
      editor?.commands.setContent(d.content_md || "");
    } finally {
      setBusy(false);
    }
  }

  async function revert() {
    if (!doc) return;
    setBusy(true);
    try {
      const d = await api.revertEdit(doc.id);
      setDoc(d);
      setPending(null);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex h-full min-w-0 flex-col border-l border-zinc-800 bg-zinc-950">
      <div className="flex items-center gap-2 border-b border-zinc-800 px-3 py-2">
        <span className="text-zinc-400">📄</span>
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          onBlur={save}
          className="min-w-0 flex-1 bg-transparent text-[15px] font-semibold text-zinc-100 outline-none"
          placeholder="Belge başlığı"
        />
        <a
          href={doc ? api.exportUrl(doc.id, "udf") : "#"}
          className="rounded-md border border-zinc-700 px-2 py-1 text-[12px] text-zinc-300 hover:text-white"
          title="UYAP UDF indir"
        >
          UDF
        </a>
        <a
          href={doc ? api.exportUrl(doc.id, "docx") : "#"}
          className="rounded-md border border-zinc-700 px-2 py-1 text-[12px] text-zinc-300 hover:text-white"
          title="Word DOCX indir"
        >
          DOCX
        </a>
        <button onClick={onClose} className="px-1.5 text-zinc-500 hover:text-zinc-200" title="Kapat">
          ✕
        </button>
      </div>

      {/* toolbar */}
      <div className="flex flex-wrap items-center gap-1 border-b border-zinc-900 px-3 py-1.5 text-zinc-300">
        <ToolBtn on={() => editor?.chain().focus().toggleBold().run()} active={editor?.isActive("bold")}>B</ToolBtn>
        <ToolBtn on={() => editor?.chain().focus().toggleItalic().run()} active={editor?.isActive("italic")}>i</ToolBtn>
        <ToolBtn on={() => editor?.chain().focus().toggleHeading({ level: 1 }).run()} active={editor?.isActive("heading", { level: 1 })}>H1</ToolBtn>
        <ToolBtn on={() => editor?.chain().focus().toggleHeading({ level: 2 }).run()} active={editor?.isActive("heading", { level: 2 })}>H2</ToolBtn>
        <ToolBtn on={() => editor?.chain().focus().toggleBulletList().run()} active={editor?.isActive("bulletList")}>•</ToolBtn>
        <ToolBtn on={() => editor?.chain().focus().toggleOrderedList().run()} active={editor?.isActive("orderedList")}>1.</ToolBtn>
        <ToolBtn on={() => editor?.chain().focus().toggleBlockquote().run()} active={editor?.isActive("blockquote")}>❝</ToolBtn>
        <button
          onClick={save}
          disabled={busy}
          className="ml-auto rounded-md bg-white px-2.5 py-1 text-[12px] font-semibold text-black hover:opacity-90 disabled:opacity-50"
        >
          Kaydet
        </button>
        {savedAt && <span className="text-[11px] text-zinc-600">kaydedildi {savedAt}</span>}
      </div>

      {pending && (
        <div className="border-b border-amber-500/40 bg-amber-500/5 px-3 py-2">
          <div className="mb-1 flex items-center justify-between gap-2">
            <span className="text-[13px] text-amber-300">
              Korkut bir değişiklik önerdi {pending.summary ? `(${pending.summary})` : ""}
            </span>
            <div className="flex gap-2">
              <button onClick={accept} disabled={busy} className="rounded-md bg-white px-2.5 py-1 text-[12px] font-semibold text-black hover:opacity-90 disabled:opacity-50">
                Kabul et
              </button>
              <button onClick={revert} disabled={busy} className="rounded-md border border-zinc-700 px-2.5 py-1 text-[12px] text-zinc-300 hover:text-white">
                Geri al
              </button>
            </div>
          </div>
          <div className="md max-h-48 overflow-y-auto rounded-md border border-zinc-800 bg-zinc-900 px-3 py-2 text-[13px]">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{pending.content_md}</ReactMarkdown>
          </div>
        </div>
      )}

      <div className="doc-editor md flex-1 overflow-y-auto px-4 py-3">
        <EditorContent editor={editor} />
      </div>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          draft();
        }}
        className="flex items-end gap-2 border-t border-zinc-800 bg-zinc-950 px-3 py-2"
      >
        <input
          value={instruction}
          onChange={(e) => setInstruction(e.target.value)}
          placeholder="Korkut'a yaptır: ör. 'boşanma dilekçesi hazırla' / 'Sonuç ve İstem'i güçlendir'"
          className="min-w-0 flex-1 rounded-[10px] border border-zinc-800 bg-zinc-900 px-3 py-2 text-[14px] text-zinc-100 outline-none focus:border-white"
        />
        <button
          type="submit"
          disabled={busy}
          className="shrink-0 rounded-[10px] bg-white px-3 py-2 text-[14px] font-semibold text-black hover:opacity-90 disabled:opacity-50"
        >
          {busy ? "…" : "Yaptır"}
        </button>
      </form>
    </div>
  );
}

function ToolBtn({
  children,
  on,
  active,
}: {
  children: React.ReactNode;
  on: () => void;
  active?: boolean;
}) {
  return (
    <button
      type="button"
      onMouseDown={(e) => {
        e.preventDefault();
        on();
      }}
      className={
        "min-w-[28px] rounded-md px-2 py-1 text-[13px] " +
        (active ? "bg-zinc-700 text-white" : "text-zinc-400 hover:bg-zinc-900 hover:text-white")
      }
    >
      {children}
    </button>
  );
}
