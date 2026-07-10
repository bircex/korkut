import { useRef, useState, type ReactNode } from "react";
import { type Editor } from "ckeditor5";
import { ArrowLeft } from "../icons";
import { MarkdownEditor } from "./MarkdownEditor";
import { ExportMenu } from "./ExportMenu";

// The one and only editor frame used everywhere in the app (document Canvas, Özelleştir, …):
// a UYAP-like A4 page (MarkdownEditor) under a consistent header — back, title, Save, Export.
// Callers only supply their own data + actions; the editing surface is identical every time.
export function DocumentEditor({
  value,
  title,
  subtitle,
  titleEditable = false,
  onTitleChange,
  onClose,
  onSave,
  headerExtra,
  footer,
  exportTitle,
}: {
  value: string;
  title: string;
  subtitle?: string;
  titleEditable?: boolean;
  onTitleChange?: (t: string) => void;
  onClose?: () => void;
  onSave?: (markdown: string) => void | Promise<void>;
  headerExtra?: ReactNode;
  footer?: ReactNode;
  exportTitle?: string;
}) {
  const editorRef = useRef<Editor | null>(null);
  const [busy, setBusy] = useState(false);
  const liveMarkdown = () =>
    editorRef.current ? (editorRef.current.getData() as string) : value;

  async function save() {
    if (!onSave) return;
    setBusy(true);
    try {
      await onSave(liveMarkdown());
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex h-full min-w-0 flex-col bg-white">
      <header className="flex items-center gap-2 border-b border-zinc-200 px-3 py-2">
        {onClose && (
          <button
            onClick={onClose}
            className="flex shrink-0 items-center gap-1 rounded-md px-2 py-1 text-sm text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900"
          >
            <ArrowLeft size={14} aria-hidden /> Geri
          </button>
        )}
        <div className="min-w-0 flex-1">
          {titleEditable ? (
            <input
              value={title}
              onChange={(e) => onTitleChange?.(e.target.value)}
              onBlur={save}
              className="w-full bg-transparent text-[15px] font-semibold text-zinc-900 outline-none"
              placeholder="Başlık"
            />
          ) : (
            <div className="truncate text-[15px] font-semibold text-zinc-900">{title}</div>
          )}
          {subtitle && <div className="truncate text-xs text-zinc-400">{subtitle}</div>}
        </div>
        {headerExtra}
        {onSave && (
          <button
            onClick={save}
            disabled={busy}
            className="shrink-0 rounded-lg bg-zinc-900 px-4 py-1.5 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-50"
          >
            {busy ? "…" : "Kaydet"}
          </button>
        )}
        <ExportMenu title={exportTitle ?? title} getMarkdown={liveMarkdown} />
      </header>

      <div className="doc-canvas min-h-0 flex-1 overflow-y-auto">
        <MarkdownEditor
          value={value}
          onReady={(editor) => {
            editorRef.current = editor;
          }}
        />
      </div>

      {footer && <div className="border-t border-zinc-100 px-4 py-3">{footer}</div>}
    </div>
  );
}
