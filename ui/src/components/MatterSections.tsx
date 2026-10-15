import { useEffect, useState } from "react";
import { api, notesApi, type CalendarEvent, type Expense, type Invoice, type Note, type SessionBrief } from "../api";
import { CaretRight } from "../icons";

// Dosya sayfasının sekmelerindeki listeler. Her biri yalnızca bu dosyanın kayıtlarını ister; kimin
// neyi gördüğünü sunucu belirler (dosya ekibi, yetkiler), burada yalnızca gösterilir.

const box = "divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200";
const empty = "px-4 py-3 text-sm text-zinc-400";

function when(iso: string | null): string {
  return iso ? new Date(iso).toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric" }) : "";
}

function useLoad<T>(load: () => Promise<T>, deps: unknown[]): { data: T | null; error: boolean; reload: () => void } {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState(false);
  const [tick, setTick] = useState(0);
  useEffect(() => {
    let live = true;
    load()
      .then((d) => live && (setData(d), setError(false)))
      .catch(() => live && setError(true));
    return () => {
      live = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick]);
  return { data, error, reload: () => setTick((t) => t + 1) };
}

export function MatterChats({
  matterId,
  onOpen,
  onNew,
}: {
  matterId: string;
  onOpen: (sessionId: number) => void;
  onNew: () => void;
}) {
  const { data, error } = useLoad(() => api.sessions(50, 0, matterId), [matterId]);
  const items: SessionBrief[] = data?.items ?? [];
  return (
    <>
      <div className="mb-2 flex items-center">
        <h2 className="text-sm font-semibold text-zinc-700">Sohbetler</h2>
        <button
          onClick={onNew}
          className="ml-auto rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90"
        >
          + Yeni sohbet
        </button>
      </div>
      <div className={box}>
        {error && <div className="px-4 py-3 text-sm text-red-600">Sohbetler yüklenemedi.</div>}
        {data && items.length === 0 && <div className={empty}>Bu dosyada henüz sohbet yok.</div>}
        {items.map((s) => (
          <button
            key={s.id}
            onClick={() => onOpen(s.id)}
            className="flex w-full items-center gap-3 px-4 py-2.5 text-left text-sm hover:bg-zinc-50"
          >
            <span className="min-w-0 flex-1 truncate text-zinc-800">{s.title}</span>
            <span className="shrink-0 text-xs text-zinc-400">
              {[s.created_by_name, when(s.updated_at)].filter(Boolean).join(" · ")}
            </span>
            <CaretRight size={14} aria-hidden className="shrink-0 text-zinc-300" />
          </button>
        ))}
      </div>
    </>
  );
}

const CATEGORY_LABEL: Record<string, string> = {
  hearing: "Duruşma",
  deadline: "Süre",
  gorev: "Görev",
  hatirlatma: "Hatırlatma",
  randevu: "Randevu",
};

export function MatterAgenda({ matterId }: { matterId: string }) {
  const { data, error } = useLoad(
    () => api.calendarEvents({ matter_id: matterId as unknown as number }),
    [matterId],
  );
  const items: CalendarEvent[] = data?.items ?? [];
  return (
    <>
      <h2 className="mb-2 text-sm font-semibold text-zinc-700">Ajanda</h2>
      <div className={box}>
        {error && <div className="px-4 py-3 text-sm text-red-600">Ajanda yüklenemedi.</div>}
        {data && items.length === 0 && <div className={empty}>Bu dosyada ajanda kaydı yok.</div>}
        {items.map((e) => (
          <div key={`${e.category}-${e.id}-${e.start_at}`} className="flex items-center gap-3 px-4 py-2.5 text-sm">
            <span className="w-24 shrink-0 font-medium text-zinc-800">{when(e.start_at)}</span>
            <span className="shrink-0 rounded-md bg-zinc-100 px-1.5 py-0.5 text-xs text-zinc-600">
              {CATEGORY_LABEL[e.category] ?? e.category}
            </span>
            <span className="min-w-0 flex-1 truncate text-zinc-700">{e.title}</span>
            {e.assignee_name && <span className="shrink-0 text-xs text-zinc-400">{e.assignee_name}</span>}
          </div>
        ))}
      </div>
    </>
  );
}

export function MatterNotes({ matterId, canWrite }: { matterId: string; canWrite: boolean }) {
  const { data, error, reload } = useLoad(() => notesApi.list(matterId), [matterId]);
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);
  const [saveError, setSaveError] = useState(false);
  const items: Note[] = data ?? [];

  async function add() {
    if (!text.trim()) return;
    setBusy(true);
    setSaveError(false);
    try {
      await notesApi.create(matterId, text.trim());
      setText("");
      reload();
    } catch {
      setSaveError(true);
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <h2 className="mb-2 text-sm font-semibold text-zinc-700">Notlar</h2>
      {canWrite && (
        <div className="mb-3 flex items-end gap-2">
          <label htmlFor="matter-note" className="sr-only">
            Yeni not
          </label>
          <textarea
            id="matter-note"
            rows={2}
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder="Görüşme notu ekleyin…"
            className="min-h-[2.5rem] flex-1 resize-y rounded-lg border border-zinc-200 px-3 py-2 text-sm outline-none focus:border-zinc-400"
          />
          <button
            onClick={add}
            disabled={busy || !text.trim()}
            className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
          >
            Ekle
          </button>
        </div>
      )}
      {saveError && <div className="mb-2 text-xs text-red-600">Not kaydedilemedi.</div>}
      <div className={box}>
        {error && <div className="px-4 py-3 text-sm text-red-600">Notlar yüklenemedi.</div>}
        {data && items.length === 0 && <div className={empty}>Bu dosyada not yok.</div>}
        {items.map((n) => (
          <div key={n.id} className="px-4 py-2.5 text-sm">
            <div className="text-xs text-zinc-400">{when(n.occurred_at)}</div>
            <div className="whitespace-pre-wrap text-zinc-800">{n.body}</div>
          </div>
        ))}
      </div>
    </>
  );
}

const tl = (n: number) => n.toLocaleString("tr-TR", { style: "currency", currency: "TRY" });

export function MatterFinance({ matterId }: { matterId: string }) {
  const { data, error } = useLoad(
    () => Promise.all([api.invoices(), api.expenses()]),
    [matterId],
  );
  const same = (id: number | null) => id != null && String(id) === matterId;
  const invoices: Invoice[] = (data?.[0].items ?? []).filter((i) => same(i.matter_id));
  const expenses: Expense[] = (data?.[1].items ?? []).filter((e) => same(e.matter_id));
  const billed = invoices.reduce((sum, i) => sum + i.gross, 0);
  const spent = expenses.reduce((sum, e) => sum + e.amount, 0);
  return (
    <>
      <h2 className="mb-2 text-sm font-semibold text-zinc-700">Mali</h2>
      {error && <div className="mb-2 text-sm text-red-600">Mali kayıtlar yüklenemedi.</div>}
      {data && (
        <div className="mb-4 grid grid-cols-2 gap-3 text-sm">
          <div className="rounded-xl border border-zinc-200 px-4 py-3">
            <div className="text-[11px] uppercase tracking-wide text-zinc-400">Faturalanan</div>
            <div className="mt-0.5 font-semibold text-zinc-900">{tl(billed)}</div>
          </div>
          <div className="rounded-xl border border-zinc-200 px-4 py-3">
            <div className="text-[11px] uppercase tracking-wide text-zinc-400">Masraf</div>
            <div className="mt-0.5 font-semibold text-zinc-900">{tl(spent)}</div>
          </div>
        </div>
      )}
      <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-zinc-400">Faturalar</h3>
      <div className={`${box} mb-4`}>
        {data && invoices.length === 0 && <div className={empty}>Bu dosyada fatura yok.</div>}
        {invoices.map((i) => (
          <div key={i.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
            <span className="w-24 shrink-0 text-zinc-500">{when(i.issued_at)}</span>
            <span className="min-w-0 flex-1 truncate text-zinc-700">{i.description ?? "Fatura"}</span>
            <span className="shrink-0 text-xs text-zinc-400">{i.status}</span>
            <span className="shrink-0 font-medium text-zinc-800">{tl(i.gross)}</span>
          </div>
        ))}
      </div>
      <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-zinc-400">Masraflar</h3>
      <div className={box}>
        {data && expenses.length === 0 && <div className={empty}>Bu dosyada masraf yok.</div>}
        {expenses.map((e) => (
          <div key={e.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
            <span className="w-24 shrink-0 text-zinc-500">{when(e.spent_at)}</span>
            <span className="min-w-0 flex-1 truncate text-zinc-700">{e.description ?? e.kind ?? "Masraf"}</span>
            <span className="shrink-0 font-medium text-zinc-800">{tl(e.amount)}</span>
          </div>
        ))}
      </div>
    </>
  );
}
