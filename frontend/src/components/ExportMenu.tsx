import { useState } from "react";
import { api, type ExportFormat } from "../api";
import { CaretDown } from "../icons";

const FORMATS: { value: ExportFormat; label: string }[] = [
  { value: "udf", label: "UYAP (UDF)" },
  { value: "docx", label: "Word (DOCX)" },
];

function safeName(title: string): string {
  return (title.replace(/[^A-Za-z0-9_-]+/g, "_").replace(/^_+|_+$/g, "").slice(0, 60) || "belge");
}

// Dropdown that exports the current editor content (live markdown) to a chosen format.
export function ExportMenu({ title, getMarkdown }: { title: string; getMarkdown: () => string }) {
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  async function download(format: ExportFormat) {
    setOpen(false);
    setBusy(true);
    try {
      const blob = await api.exportMarkdown(format, title || "belge", getMarkdown());
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${safeName(title)}.${format}`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch {
      alert("Dışa aktarma başarısız oldu.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="relative">
      <button
        onClick={() => setOpen((v) => !v)}
        disabled={busy}
        className="flex items-center gap-1.5 rounded-lg bg-zinc-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-50"
      >
        {busy ? "…" : "Dışa aktar"} <CaretDown size={12} aria-hidden className="opacity-70" />
      </button>
      {open && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => setOpen(false)} />
          <div className="absolute right-0 z-50 mt-1 flex w-48 flex-col overflow-hidden rounded-xl border border-zinc-700 bg-zinc-900 py-1 shadow-xl">
            {FORMATS.map((f) => (
              <button
                key={f.value}
                onClick={() => download(f.value)}
                className="w-full px-4 py-2.5 text-left text-sm text-zinc-100 hover:bg-zinc-700"
              >
                {f.label}
              </button>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
