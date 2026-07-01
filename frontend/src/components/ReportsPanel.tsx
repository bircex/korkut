import { useEffect, useState } from "react";
import { api, type ReportsOverview } from "../api";

// Rapor — a read-only dashboard rolled up from the office's own data (dosya / müvekkil / mali /
// görev / evrak). No new records here: every figure is derived from what the other modules store.

const money = (n: number) => `${n.toLocaleString("tr-TR", { minimumFractionDigits: 2 })} ₺`;

const MATTER_STATUS_LABEL: Record<string, string> = {
  open: "Açık",
  closed: "Kapalı",
  archived: "Arşiv",
  "": "Belirsiz",
};

const DIR_LABEL: Record<string, string> = {
  incoming: "Gelen",
  outgoing: "Giden",
  internal: "İç",
};

function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-4">
      <div className="mb-3 text-[11px] font-semibold uppercase tracking-wide text-zinc-400">
        {title}
      </div>
      {children}
    </div>
  );
}

function Stat({
  label,
  value,
  accent,
}: {
  label: string;
  value: string | number;
  accent?: "red" | "green";
}) {
  const color =
    accent === "red" ? "text-red-600" : accent === "green" ? "text-emerald-600" : "text-zinc-900";
  return (
    <div>
      <div className="text-[11px] uppercase tracking-wide text-zinc-400">{label}</div>
      <div className={`mt-0.5 text-lg font-semibold ${color}`}>{value}</div>
    </div>
  );
}

export function ReportsPanel({ onClose }: { onClose: () => void }) {
  const [data, setData] = useState<ReportsOverview | null>(null);

  useEffect(() => {
    api
      .reportsOverview()
      .then(setData)
      .catch(() => {});
  }, []);

  return (
    <div className="flex h-dvh flex-col bg-white">
      <div className="flex items-center gap-3 border-b border-zinc-200 px-4 py-3">
        <button
          onClick={onClose}
          className="rounded-lg border border-zinc-300 px-3 py-1.5 text-sm text-zinc-700 hover:border-zinc-900"
        >
          Geri
        </button>
        <h1 className="text-lg font-bold text-zinc-900">Rapor</h1>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto bg-zinc-50">
        <div className="mx-auto max-w-3xl px-4 py-4">
          {!data ? (
            <p className="text-[13px] text-zinc-400">Yükleniyor…</p>
          ) : (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Card title="Dosyalar">
                <div className="mb-3">
                  <Stat label="Toplam dosya" value={data.matters.total} />
                </div>
                {Object.keys(data.matters.by_status).length === 0 ? (
                  <p className="text-[13px] text-zinc-400">Kayıt yok.</p>
                ) : (
                  <div className="space-y-1">
                    {Object.entries(data.matters.by_status).map(([status, count]) => (
                      <div key={status} className="flex justify-between text-sm text-zinc-700">
                        <span>{MATTER_STATUS_LABEL[status] ?? (status || "Belirsiz")}</span>
                        <span className="font-medium">{count}</span>
                      </div>
                    ))}
                  </div>
                )}
              </Card>

              <Card title="Müvekkiller">
                <Stat label="Toplam müvekkil" value={data.clients.total} />
              </Card>

              <Card title="Mali durum (cari)">
                <div className="grid grid-cols-2 gap-3">
                  <Stat label="Faturalanan" value={money(data.finance.invoiced)} />
                  <Stat label="Tahsil edilen" value={money(data.finance.paid)} accent="green" />
                  <Stat label="Bakiye" value={money(data.finance.outstanding)} accent="red" />
                  <Stat label="Masraf" value={money(data.finance.expenses)} />
                </div>
              </Card>

              <Card title="Görevler">
                <div className="grid grid-cols-3 gap-3">
                  <Stat label="Açık" value={data.tasks.open} />
                  <Stat label="Biten" value={data.tasks.done} accent="green" />
                  <Stat label="Geciken" value={data.tasks.overdue} accent="red" />
                </div>
              </Card>

              <Card title={`Ajanda (${data.agenda.days} gün)`}>
                <div className="grid grid-cols-3 gap-3">
                  <Stat label="Duruşma" value={data.agenda.hearings_upcoming} />
                  <Stat label="Süre" value={data.agenda.deadlines_upcoming} />
                  <Stat label="Geçen süre" value={data.agenda.deadlines_overdue} accent="red" />
                </div>
              </Card>

              <Card title="Evrak">
                <div className="mb-3">
                  <Stat label="Toplam evrak" value={data.evrak.total} />
                </div>
                {Object.keys(data.evrak.by_direction).length === 0 ? (
                  <p className="text-[13px] text-zinc-400">Kayıt yok.</p>
                ) : (
                  <div className="space-y-1">
                    {Object.entries(data.evrak.by_direction).map(([dir, count]) => (
                      <div key={dir} className="flex justify-between text-sm text-zinc-700">
                        <span>{DIR_LABEL[dir] ?? (dir || "Belirsiz")}</span>
                        <span className="font-medium">{count}</span>
                      </div>
                    ))}
                  </div>
                )}
              </Card>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
