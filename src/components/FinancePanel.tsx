import { useCallback, useEffect, useState } from "react";
import { api, type Expense, type FinanceSummary, type Invoice } from "../api";
import { Modal } from "./Modal";
import { DataTable } from "./DataTable";

// Mali — the office's bookkeeping (fatura / masraf / cari). A first slice: record invoices and
// expenses and see a simple current-account summary. Real e-SMM/e-fatura submission (GİB) is out
// of scope for now.

const input =
  "rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900";

const STATUS_LABEL: Record<string, string> = {
  draft: "Taslak",
  issued: "Kesildi",
  sent: "Gönderildi",
  paid: "Tahsil edildi",
  cancelled: "İptal",
};

// Durum hapları: tahsil yeşil, bekleyen amber, taslak nötr, iptal kırmızı.
const STATUS_PILL: Record<string, string> = {
  paid: "bg-emerald-500/15 text-emerald-700",
  sent: "bg-amber-500/15 text-amber-700",
  issued: "bg-amber-500/15 text-amber-700",
  draft: "bg-zinc-100 text-zinc-500",
  cancelled: "bg-red-500/10 text-red-600",
};

const money = (n: number) => `${n.toLocaleString("tr-TR", { minimumFractionDigits: 2 })} ₺`;

// KDV oranı backend'de 0-1 aralığında kesir olarak tutulur (0.20 → %20); eski
// kayıtlarda yüzde olarak gelmiş değerleri de olduğu gibi gösterir.
const vatPct = (r: number) => (r <= 1 ? Math.round(r * 100) : Math.round(r));

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border border-zinc-200 p-3">
      <div className="text-[11px] uppercase tracking-wide text-zinc-400">{label}</div>
      <div className="mt-0.5 text-lg font-semibold text-zinc-900">{money(value)}</div>
    </div>
  );
}

export function FinancePanel() {
  const [summary, setSummary] = useState<FinanceSummary | null>(null);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [expenses, setExpenses] = useState<Expense[]>([]);
  const [adding, setAdding] = useState<"invoice" | "expense" | null>(null);
  const [inv, setInv] = useState({ amount: "", vat_rate: "20", description: "" });
  const [exp, setExp] = useState({ amount: "", kind: "", description: "" });

  const refresh = useCallback(() => {
    Promise.all([api.financeSummary(), api.invoices(), api.expenses()])
      .then(([s, i, e]) => {
        setSummary(s);
        setInvoices(i.items);
        setExpenses(e.items);
      })
      .catch(() => {});
  }, []);

  useEffect(() => {
    Promise.all([api.financeSummary(), api.invoices(), api.expenses()])
      .then(([s, i, e]) => {
        setSummary(s);
        setInvoices(i.items);
        setExpenses(e.items);
      })
      .catch(() => {});
  }, []);

  async function addInvoice() {
    const amount = parseFloat(inv.amount);
    if (!amount) return;
    await api.createInvoice({
      amount,
      vat_rate: parseFloat(inv.vat_rate) || 0,
      description: inv.description.trim() || null,
      status: "issued",
    });
    setInv({ amount: "", vat_rate: "20", description: "" });
    setAdding(null);
    refresh();
  }

  async function addExpense() {
    const amount = parseFloat(exp.amount);
    if (!amount) return;
    await api.createExpense({
      amount,
      kind: exp.kind.trim() || null,
      description: exp.description.trim() || null,
    });
    setExp({ amount: "", kind: "", description: "" });
    setAdding(null);
    refresh();
  }

  async function markPaid(id: number) {
    await api.updateInvoice(id, { status: "paid" });
    refresh();
  }

  async function removeInvoice(id: number) {
    await api.deleteInvoice(id);
    refresh();
  }

  async function removeExpense(id: number) {
    await api.deleteExpense(id);
    refresh();
  }

  return (
    <div className="flex h-full min-w-0 flex-1 flex-col bg-white">
      <div className="flex items-center gap-3 border-b border-zinc-200 px-4 py-3">
        <h1 className="text-lg font-bold text-zinc-900">Mali</h1>
        <button
          onClick={() => setAdding("invoice")}
          className="ml-auto rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90"
        >
          + Fatura
        </button>
        <button
          onClick={() => setAdding("expense")}
          className="rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90"
        >
          + Masraf
        </button>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto max-w-3xl px-4 py-4">
          {summary && (
            <div className="mb-6 grid grid-cols-2 gap-2 sm:grid-cols-4">
              <Stat label="Kesilen" value={summary.invoiced} />
              <Stat label="Tahsil edilen" value={summary.paid} />
              <Stat label="Bakiye" value={summary.outstanding} />
              <Stat label="Masraf" value={summary.expenses} />
            </div>
          )}

          <h2 className="mb-2 text-sm font-semibold text-zinc-700">Faturalar</h2>
          <div className="mb-6">
            <DataTable
              rows={invoices}
              rowKey={(i) => i.id}
              pageSize={10}
              empty="Fatura yok."
              searchPlaceholder="Tutar, durum, açıklama ara…"
              searchText={(i) =>
                [money(i.gross), STATUS_LABEL[i.status] ?? i.status, i.description]
                  .filter(Boolean)
                  .join(" ")
              }
              columns={[
                {
                  key: "amount",
                  label: "Tutar",
                  render: (i) => (
                    <>
                      <div className="font-medium text-zinc-900">{money(i.gross)}</div>
                      <div className="text-xs text-zinc-400">
                        net {money(i.amount)} + %{vatPct(i.vat_rate)} KDV
                      </div>
                    </>
                  ),
                },
                {
                  key: "status",
                  label: "Durum",
                  render: (i) => (
                    <span
                      className={`whitespace-nowrap rounded-full px-2 py-0.5 text-[11px] font-medium ${
                        STATUS_PILL[i.status] ?? "bg-zinc-100 text-zinc-500"
                      }`}
                    >
                      {STATUS_LABEL[i.status] ?? i.status}
                    </span>
                  ),
                },
                {
                  key: "desc",
                  label: "Açıklama",
                  className: "hidden sm:table-cell",
                  render: (i) => <span className="text-zinc-500">{i.description ?? "-"}</span>,
                },
                {
                  key: "actions",
                  label: "",
                  className: "w-44 text-right",
                  render: (i) => (
                    <span className="flex items-center justify-end gap-1">
                      {i.status !== "paid" && (
                        <button
                          onClick={() => markPaid(i.id)}
                          className="rounded-md border border-zinc-300 px-2 py-1 text-[12px] text-zinc-700 hover:border-zinc-900"
                        >
                          Tahsil edildi
                        </button>
                      )}
                      <button
                        onClick={() => removeInvoice(i.id)}
                        className="rounded-md px-2 py-1 text-[12px] text-red-600 hover:bg-red-50"
                      >
                        Sil
                      </button>
                    </span>
                  ),
                },
              ]}
            />
          </div>

          <h2 className="mb-2 text-sm font-semibold text-zinc-700">Masraflar</h2>
          <DataTable
            rows={expenses}
            rowKey={(e) => e.id}
            pageSize={10}
            empty="Masraf yok."
            searchPlaceholder="Tutar, tür, açıklama ara…"
            searchText={(e) => [money(e.amount), e.kind, e.description].filter(Boolean).join(" ")}
            columns={[
              {
                key: "amount",
                label: "Tutar",
                render: (e) => <span className="font-medium text-zinc-900">{money(e.amount)}</span>,
              },
              {
                key: "kind",
                label: "Tür",
                render: (e) =>
                  e.kind ? (
                    <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-[11px] font-medium text-zinc-500">
                      {e.kind}
                    </span>
                  ) : (
                    <span className="text-zinc-400">-</span>
                  ),
              },
              {
                key: "desc",
                label: "Açıklama",
                className: "hidden sm:table-cell",
                render: (e) => <span className="text-zinc-500">{e.description ?? "-"}</span>,
              },
              {
                key: "actions",
                label: "",
                className: "w-16 text-right",
                render: (e) => (
                  <button
                    onClick={() => removeExpense(e.id)}
                    className="rounded-md px-2 py-1 text-[12px] text-red-600 hover:bg-red-50"
                  >
                    Sil
                  </button>
                ),
              },
            ]}
          />

        </div>
      </div>

      {adding === "invoice" && (
        <Modal
          title="Yeni fatura"
          onClose={() => setAdding(null)}
          footer={
            <>
              <button
                onClick={() => setAdding(null)}
                className="rounded-md px-3 py-2 text-sm font-medium text-zinc-600 hover:bg-zinc-100"
              >
                Vazgeç
              </button>
              <button
                onClick={addInvoice}
                disabled={!parseFloat(inv.amount)}
                className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
              >
                Ekle
              </button>
            </>
          }
        >
          <div className="flex flex-col gap-3">
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Tutar (KDV hariç)
              <input
                autoFocus
                value={inv.amount}
                onChange={(e) => setInv({ ...inv, amount: e.target.value })}
                placeholder="ör. 5000"
                inputMode="decimal"
                className={input}
              />
            </label>
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              KDV %
              <input
                value={inv.vat_rate}
                onChange={(e) => setInv({ ...inv, vat_rate: e.target.value })}
                placeholder="20"
                inputMode="decimal"
                className={input}
              />
            </label>
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Açıklama (opsiyonel)
              <input
                value={inv.description}
                onChange={(e) => setInv({ ...inv, description: e.target.value })}
                placeholder="ör. danışmanlık"
                className={input}
              />
            </label>
          </div>
        </Modal>
      )}

      {adding === "expense" && (
        <Modal
          title="Yeni masraf"
          onClose={() => setAdding(null)}
          footer={
            <>
              <button
                onClick={() => setAdding(null)}
                className="rounded-md px-3 py-2 text-sm font-medium text-zinc-600 hover:bg-zinc-100"
              >
                Vazgeç
              </button>
              <button
                onClick={addExpense}
                disabled={!parseFloat(exp.amount)}
                className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
              >
                Ekle
              </button>
            </>
          }
        >
          <div className="flex flex-col gap-3">
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Tutar
              <input
                autoFocus
                value={exp.amount}
                onChange={(e) => setExp({ ...exp, amount: e.target.value })}
                placeholder="ör. 250"
                inputMode="decimal"
                className={input}
              />
            </label>
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Tür (opsiyonel)
              <input
                value={exp.kind}
                onChange={(e) => setExp({ ...exp, kind: e.target.value })}
                placeholder="ör. harç, posta"
                className={input}
              />
            </label>
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Açıklama (opsiyonel)
              <input
                value={exp.description}
                onChange={(e) => setExp({ ...exp, description: e.target.value })}
                placeholder="ör. tebligat gideri"
                className={input}
              />
            </label>
          </div>
        </Modal>
      )}
    </div>
  );
}
