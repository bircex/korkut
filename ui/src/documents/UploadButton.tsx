import { useRef, useState } from "react";
import { ApiError } from "../api";
import { useToast } from "../a11y";
import { uploadDocument } from "./api";

// Accessible upload control with progress (KDM-031). Rejections (magic-byte
// mismatch, malware, zip-bomb — KDM-026) surface as a toast; the optimistic
// "uploading" state is announced via aria-live.

export function UploadButton({ matterId, onUploaded }: { matterId?: string; onUploaded: () => void }) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [pct, setPct] = useState<number | null>(null);
  const { notify } = useToast();

  async function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = ""; // allow re-selecting the same file
    if (!file) return;
    setPct(0);
    try {
      const doc = await uploadDocument(file, { matterId, onProgress: setPct });
      if (doc.status === "rejected") {
        notify(`${file.name} reddedildi (güvenlik taraması).`, "error");
      } else {
        notify(`${file.name} yüklendi, taranıyor.`, "success");
      }
      onUploaded();
    } catch (err) {
      notify(err instanceof ApiError ? err.message : "Yükleme başarısız", "error");
    } finally {
      setPct(null);
    }
  }

  return (
    <div className="flex items-center gap-3">
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        disabled={pct !== null}
        className="rounded-[10px] bg-zinc-900 px-4 py-2.5 text-[14px] font-semibold text-white outline-none transition-opacity hover:opacity-90 focus-visible:ring-2 focus-visible:ring-zinc-900/30 disabled:opacity-50"
      >
        {pct !== null ? "Yükleniyor…" : "Belge yükle"}
      </button>
      <input ref={inputRef} type="file" onChange={onPick} className="sr-only" aria-hidden tabIndex={-1} />
      {pct !== null && (
        <div className="flex items-center gap-2" role="status" aria-live="polite">
          <progress value={pct} max={100} className="h-2 w-32">
            {pct}%
          </progress>
          <span className="text-[12px] text-zinc-500">%{pct}</span>
        </div>
      )}
    </div>
  );
}
