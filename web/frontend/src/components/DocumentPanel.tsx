import { useEffect, useRef, useState } from "react";
import { CKEditor } from "@ckeditor/ckeditor5-react";
import {
  Alignment,
  Autoformat,
  BlockQuote,
  Bold,
  ClassicEditor,
  Essentials,
  Font,
  Heading,
  HorizontalLine,
  Italic,
  Link,
  List,
  Markdown,
  Paragraph,
  Table,
  TableToolbar,
  Underline,
  type Editor,
} from "ckeditor5";
import "ckeditor5/ckeditor5.css";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { api, ApiError, type DocumentData, type PendingEdit } from "../api";

const EDITOR_CONFIG = {
  licenseKey: "GPL",
  plugins: [
    Essentials, Paragraph, Heading, Bold, Italic, Underline, List, BlockQuote, Link,
    Alignment, Autoformat, Font, HorizontalLine, Table, TableToolbar, Markdown,
  ],
  toolbar: [
    "undo", "redo", "|", "heading", "|", "fontSize", "|", "bold", "italic", "underline",
    "|", "alignment", "|", "bulletedList", "numberedList", "|", "blockQuote", "insertTable",
    "link", "horizontalLine",
  ],
  table: { contentToolbar: ["tableColumn", "tableRow", "mergeTableCells"] },
};

export function DocumentPanel({ sessionId, onClose }: { sessionId: number; onClose: () => void }) {
  const [doc, setDoc] = useState<DocumentData | null>(null);
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");
  const [pending, setPending] = useState<PendingEdit | null>(null);
  const [instruction, setInstruction] = useState("");
  const [busy, setBusy] = useState(false);
  const [savedAt, setSavedAt] = useState("");
  const editorRef = useRef<Editor | null>(null);

  useEffect(() => {
    api
      .getDocument(sessionId)
      .then((d) => {
        setDoc(d);
        setTitle(d.title);
        setContent(d.content_md || "");
        setPending(d.pending);
      })
      .catch(() => {});
  }, [sessionId]);

  function liveMarkdown(): string {
    return editorRef.current ? (editorRef.current.getData() as string) : content;
  }

  async function save() {
    if (!doc) return;
    setBusy(true);
    try {
      const d = await api.saveDocument(doc.id, title, liveMarkdown());
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
      await api.saveDocument(doc.id, title, liveMarkdown());
      setPending(await api.draftDocument(doc.id, instr));
    } catch (err) {
      alert(err instanceof ApiError ? err.message : "taslak alınamadı");
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
      setContent(d.content_md || ""); // re-feeds the editor via the `data` prop
      setPending(null);
    } finally {
      setBusy(false);
    }
  }

  async function revert() {
    if (!doc) return;
    setBusy(true);
    try {
      await api.revertEdit(doc.id);
      setPending(null);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex h-full min-w-0 flex-col border-l border-zinc-200 bg-white">
      <div className="flex items-center gap-2 border-b border-zinc-200 px-3 py-2">
        <span className="text-zinc-400">📄</span>
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          onBlur={save}
          className="min-w-0 flex-1 bg-transparent text-[15px] font-semibold text-zinc-900 outline-none"
          placeholder="Belge başlığı"
        />
        <button onClick={save} disabled={busy}
          className="rounded-md bg-zinc-900 px-2.5 py-1 text-[12px] font-semibold text-white hover:opacity-90 disabled:opacity-50">
          Kaydet
        </button>
        {savedAt && <span className="text-[11px] text-zinc-400">{savedAt}</span>}
        <a href={doc ? api.exportUrl(doc.id, "udf") : "#"}
          className="rounded-md border border-zinc-300 px-2 py-1 text-[12px] text-zinc-700 hover:border-zinc-900" title="UYAP UDF indir">
          UDF
        </a>
        <a href={doc ? api.exportUrl(doc.id, "docx") : "#"}
          className="rounded-md border border-zinc-300 px-2 py-1 text-[12px] text-zinc-700 hover:border-zinc-900" title="Word DOCX indir">
          DOCX
        </a>
        <button onClick={onClose} className="px-1.5 text-zinc-400 hover:text-zinc-900" title="Kapat">✕</button>
      </div>

      {pending && (
        <div className="border-b border-amber-300 bg-amber-50 px-3 py-2">
          <div className="mb-1 flex items-center justify-between gap-2">
            <span className="text-[13px] text-amber-800">
              Korkut bir değişiklik önerdi {pending.summary ? `(${pending.summary})` : ""}
            </span>
            <div className="flex gap-2">
              <button onClick={accept} disabled={busy}
                className="rounded-md bg-zinc-900 px-2.5 py-1 text-[12px] font-semibold text-white hover:opacity-90 disabled:opacity-50">
                Kabul et
              </button>
              <button onClick={revert} disabled={busy}
                className="rounded-md border border-zinc-300 px-2.5 py-1 text-[12px] text-zinc-700 hover:border-zinc-900">
                Geri al
              </button>
            </div>
          </div>
          <div className="md max-h-48 overflow-y-auto rounded-md border border-zinc-200 bg-white px-3 py-2 text-[13px]">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{pending.content_md}</ReactMarkdown>
          </div>
        </div>
      )}

      <div className="doc-canvas flex-1 overflow-y-auto">
        <CKEditor
          editor={ClassicEditor}
          config={EDITOR_CONFIG}
          data={content}
          onReady={(editor) => {
            editorRef.current = editor;
          }}
        />
      </div>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          draft();
        }}
        className="flex items-end gap-2 border-t border-zinc-200 bg-white px-3 py-2"
      >
        <input
          value={instruction}
          onChange={(e) => setInstruction(e.target.value)}
          placeholder="Korkut'a yaptır: ör. 'boşanma dilekçesi hazırla' / 'Sonuç ve İstem'i güçlendir'"
          className="min-w-0 flex-1 rounded-[10px] border border-zinc-300 bg-white px-3 py-2 text-[14px] text-zinc-900 outline-none focus:border-zinc-900"
        />
        <button type="submit" disabled={busy}
          className="shrink-0 rounded-[10px] bg-zinc-900 px-3 py-2 text-[14px] font-semibold text-white hover:opacity-90 disabled:opacity-50">
          {busy ? "…" : "Yaptır"}
        </button>
      </form>
    </div>
  );
}
