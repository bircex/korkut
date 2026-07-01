import { useCallback, useEffect, useState } from "react";
import { api, type Expense, type FinanceSummary, type Invoice } from "../api";
import { Modal } from "./Modal";

// Mali — the office's bookkeeping (fatura / masraf / cari). A first slice: record invoices and
// expenses and see a simple current-account summary. Real e-SMM/e-fatura submission (GİB) is out
// of scope for now.

const input =
  "rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900";

const STATUS_LABEL: Record<string, string> = {
  draft: "Taslak",
  issued: "Kesildi",
  paid: "Tahsil edildi",
  cancelled: "İptal",
};

const money = (n: number) => `${n.toLocaleString("tr-TR", { minimumFractionDigits: 2 })} ₺`;

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
          <div className="mb-6 divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200">
            {invoices.length === 0 && (
              <div className="px-4 py-3 text-sm text-zinc-400">Fatura yok.</div>
            )}
            {invoices.map((i) => (
              <div key={i.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                <div className="min-w-0 flex-1">
                  <div className="truncate text-zinc-800">
                    {money(i.gross)}{" "}
                    <span className="text-[12px] text-zinc-400">
                      (net {money(i.amount)} + %{i.vat_rate})
                    </span>
                  </div>
                  <div className="truncate text-[12px] text-zinc-400">
                    {STATUS_LABEL[i.status] ?? i.status}
                    {i.description ? ` · ${i.description}` : ""}
                  </div>
                </div>
                {i.status !== "paid" && (
                  <button
                    onClick={() => markPaid(i.id)}
                    className="shrink-0 rounded-md border border-zinc-300 px-2 py-1 text-[12px] text-zinc-700 hover:border-zinc-900"
                  >
                    Tahsil edildi
                  </button>
                )}
                <button
                  onClick={() => removeInvoice(i.id)}
                  className="shrink-0 rounded-md px-2 py-1 text-[12px] text-red-600 hover:bg-red-50"
                >
                  Sil
                </button>
              </div>
            ))}
          </div>

          <h2 className="mb-2 text-sm font-semibold text-zinc-700">Masraflar</h2>
          <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200">
            {expenses.length === 0 && (
              <div className="px-4 py-3 text-sm text-zinc-400">Masraf yok.</div>
            )}
            {expenses.map((e) => (
              <div key={e.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                <div className="min-w-0 flex-1">
                  <div className="truncate text-zinc-800">{money(e.amount)}</div>
                  <div className="truncate text-[12px] text-zinc-400">
                    {[e.kind, e.description].filter(Boolean).join(" · ") || "—"}
                  </div>
                </div>
                <button
                  onClick={() => removeExpense(e.id)}
                  className="shrink-0 rounded-md px-2 py-1 text-[12px] text-red-600 hover:bg-red-50"
                >
                  Sil
                </button>
              </div>
            ))}
          </div>
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
