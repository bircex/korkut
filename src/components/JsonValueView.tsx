// Generic JSON-object renderer: introspects an arbitrary key/value object (no per-shape code) and
// renders it as a label/value grid, recursing into nested objects and arrays-of-objects (as a
// table). Originally built for CalculatorForm.tsx's 33 differently-shaped calculator responses;
// reused by ToolCallCard (Workspace.tsx) to render arbitrary tool-call arguments/results in chat —
// both are "render whatever JSON came back" problems with the same shape.

const ISO_DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

function labelize(key: string): string {
  return key
    .split("_")
    .map((w) => (w.length > 0 ? w[0].toLocaleUpperCase("tr-TR") + w.slice(1) : w))
    .join(" ");
}

/** Optional per-key number formatting (e.g. CalculatorForm's TL/percent heuristics) — defaults to plain locale formatting. */
export type NumberFormatter = (key: string, n: number) => string;

function formatScalar(key: string, value: unknown, formatNumber?: NumberFormatter): string {
  if (value === null || value === undefined) return "–";
  if (typeof value === "boolean") return value ? "Evet" : "Hayır";
  if (typeof value === "number") return formatNumber ? formatNumber(key, value) : value.toLocaleString("tr-TR", { maximumFractionDigits: 2 });
  if (typeof value === "string") {
    if (ISO_DATE_RE.test(value)) {
      const [y, m, d] = value.split("-");
      return `${d}.${m}.${y}`;
    }
    return value;
  }
  return String(value);
}

function ItemsTable({ items, formatNumber }: { items: Record<string, unknown>[]; formatNumber?: NumberFormatter }) {
  const columns = Object.keys(items[0] ?? {});
  return (
    <div className="overflow-x-auto rounded-lg border border-zinc-200">
      <table className="w-full text-[12.5px]">
        <thead>
          <tr className="border-b border-zinc-200 bg-zinc-50">
            {columns.map((c) => (
              <th key={c} className="px-3 py-2 text-left font-semibold text-zinc-500">
                {labelize(c)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.map((row, i) => (
            <tr key={i} className="border-b border-zinc-100 last:border-0">
              {columns.map((c) => (
                <td key={c} className="px-3 py-2 text-zinc-700">
                  {formatScalar(c, row[c], formatNumber)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function JsonValueGrid({ entries, formatNumber }: { entries: [string, unknown][]; formatNumber?: NumberFormatter }) {
  return (
    <div className="grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2">
      {entries.map(([key, value]) => {
        if (value === null || value === undefined) return null;
        if (Array.isArray(value)) {
          if (value.length === 0) return null;
          const isObjectArray = typeof value[0] === "object" && value[0] !== null;
          return (
            <div key={key} className="sm:col-span-2">
              <div className="mb-1.5 text-[11px] font-semibold uppercase tracking-wide text-zinc-400">{labelize(key)}</div>
              {isObjectArray ? (
                <ItemsTable items={value as Record<string, unknown>[]} formatNumber={formatNumber} />
              ) : (
                <div className="text-[13.5px] text-zinc-800">
                  {(value as unknown[]).map((v) => formatScalar(key, v, formatNumber)).join(", ")}
                </div>
              )}
            </div>
          );
        }
        if (typeof value === "object") {
          return (
            <div key={key} className="sm:col-span-2">
              <div className="mb-1.5 text-[11px] font-semibold uppercase tracking-wide text-zinc-400">{labelize(key)}</div>
              <div className="rounded-lg border border-zinc-100 bg-zinc-50/60 p-3">
                <JsonValueGrid entries={Object.entries(value as Record<string, unknown>)} formatNumber={formatNumber} />
              </div>
            </div>
          );
        }
        return (
          <div key={key}>
            <div className="text-[11px] font-semibold uppercase tracking-wide text-zinc-400">{labelize(key)}</div>
            <div className="text-[13.5px] text-zinc-800">{formatScalar(key, value, formatNumber)}</div>
          </div>
        );
      })}
    </div>
  );
}
