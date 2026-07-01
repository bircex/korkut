import { useEffect, useState } from "react";
import { api, type DocumentRecord } from "../api";

// Evrak — the office's document register: log incoming/outgoing correspondence and internal notes
// against a matter. A first slice: create, list, filter by direction, delete. Metadata only (no
// file storage yet).

const input =
  "rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900";

const DIR_LABEL: Record<string, string> = {
  incoming: "Gelen",
  outgoing: "Giden",
  internal: "İç",
};

function fmtDate(iso: string | null): string {
  if (!iso) return "";
  const [y, m, d] = iso.split("-");
  return d && m && y ? `${d}.${m}.${y}` : iso;
}

export function EvrakPanel({ onClose }: { onClose: () => void }) {
  const [filter, setFilter] = useState("");
  const [items, setItems] = useState<DocumentRecord[]>([]);
  const [f, setF] = useState({ direction: "incoming", title: "", reference_no: "", doc_date: "" });

  useEffect(() => {
    api
      .evrak(filter || undefined)
      .then((d) => setItems(d.items))
      .catch(() => {});
  }, [filter]);

  function reload() {
    api
      .evrak(filter || undefined)
      .then((d) => setItems(d.items))
      .catch(() => {});
  }

  async function add() {
    const title = f.title.trim();
    if (!title) return;
    await api.createEvrak({
      direction: f.direction,
      title,
      reference_no: f.reference_no.trim() || null,
      doc_date: f.doc_date || null,
    });
    setF({ direction: f.direction, title: "", reference_no: "", doc_date: "" });
    reload();
  }

  async function remove(id: number) {
    await api.deleteEvrak(id);
    reload();
  }

  return (
    <div className="flex h-dvh flex-col bg-white">
      <div className="flex items-center gap-3 border-b border-zinc-200 px-4 py-3">
        <button
          onClick={onClose}
          className="rounded-lg border border-zinc-300 px-3 py-1.5 text-sm text-zinc-700 hover:border-zinc-900"
        >
          Geri
        </button>
        <h1 className="text-lg font-bold text-zinc-900">Evrak</h1>
        <select
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          className={`${input} ml-auto text-zinc-700`}
        >
          <option value="">Tümü</option>
          <option value="incoming">Gelen</option>
          <option value="outgoing">Giden</option>
          <option value="internal">İç</option>
        </select>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto max-w-2xl px-4 py-4">
          <div className="mb-4 flex flex-wrap items-center gap-2 rounded-lg border border-zinc-200 bg-zinc-50 p-3">
            <select
              value={f.direction}
              onChange={(e) => setF({ ...f, direction: e.target.value })}
              className={`${input} text-zinc-700`}
            >
              <option value="incoming">Gelen</option>
              <option value="outgoing">Giden</option>
              <option value="internal">İç</option>
            </select>
            <input
              value={f.title}
              onChange={(e) => setF({ ...f, title: e.target.value })}
              placeholder="Evrak konusu"
              className={`${input} min-w-48 flex-1`}
            />
            <input
              value={f.reference_no}
              onChange={(e) => setF({ ...f, reference_no: e.target.value })}
              placeholder="Sayı/referans"
              className={`${input} w-32`}
            />
            <input
              type="date"
              value={f.doc_date}
              onChange={(e) => setF({ ...f, doc_date: e.target.value })}
              className={`${input} text-zinc-700`}
            />
            <button
              onClick={add}
              disabled={!f.title.trim()}
              className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
            >
              Ekle
            </button>
          </div>

          {items.length === 0 ? (
            <p className="text-[13px] text-zinc-400">Evrak yok.</p>
          ) : (
            <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200">
              {items.map((d) => (
                <div key={d.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                  <span className="shrink-0 rounded-md bg-zinc-100 px-2 py-0.5 text-[11px] text-zinc-600">
                    {DIR_LABEL[d.direction] ?? d.direction}
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-zinc-800">{d.title}</div>
                    <div className="truncate text-[12px] text-zinc-400">
                      {[d.reference_no, fmtDate(d.doc_date)].filter(Boolean).join(" · ") || "—"}
                    </div>
                  </div>
                  <button
                    onClick={() => remove(d.id)}
                    className="shrink-0 rounded-md px-2 py-1 text-[12px] text-red-600 hover:bg-red-50"
                  >
                    Sil
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
