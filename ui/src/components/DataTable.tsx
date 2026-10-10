import { useMemo, useState, type ReactNode } from "react";
import { CaretLeft, CaretRight, MagnifyingGlass } from "../icons";

// Liste ekranlarının ortak veri tablosu: istemci tarafında arama + sayfalama.
// Görsel dil tema ile aynı: beyaz kart, ince ayraçlar, küçük büyük-harf başlıklar.

export type Column<T> = {
  key: string;
  label: string;
  /** th/td'ye eklenir — responsive gizleme için (ör. "hidden md:table-cell"). */
  className?: string;
  render: (row: T) => ReactNode;
};

export function DataTable<T>({
  columns,
  rows,
  rowKey,
  searchText,
  searchPlaceholder = "Ara…",
  pageSize = 10,
  onRowClick,
  empty = "Kayıt yok.",
}: {
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T) => string | number;
  /** Aramanın taradığı düz metin; satırın görünen alanlarını birleştirir. */
  searchText: (row: T) => string;
  searchPlaceholder?: string;
  pageSize?: number;
  onRowClick?: (row: T) => void;
  empty?: string;
}) {
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(0);

  const filtered = useMemo(() => {
    const q = query.trim().toLocaleLowerCase("tr-TR");
    if (!q) return rows;
    return rows.filter((r) => searchText(r).toLocaleLowerCase("tr-TR").includes(q));
  }, [rows, query, searchText]);

  // Arama daralınca geçerli sayfa taşabilir; gösterim her zaman geçerli aralığa kenetlenir.
  const pages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const cur = Math.min(page, pages - 1);
  const pageRows = filtered.slice(cur * pageSize, (cur + 1) * pageSize);
  const from = filtered.length === 0 ? 0 : cur * pageSize + 1;
  const to = cur * pageSize + pageRows.length;

  return (
    <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-sm">
      <div className="flex items-center gap-3 border-b border-zinc-100 px-4 py-2.5">
        <div className="relative ml-auto w-full max-w-60">
          <MagnifyingGlass
            size={14}
            aria-hidden
            className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 text-zinc-400"
          />
          <input
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setPage(0);
            }}
            placeholder={searchPlaceholder}
            className="w-full rounded-lg border border-zinc-200 bg-zinc-50 py-1.5 pl-8 pr-3 text-[13px] outline-none transition-colors focus:border-zinc-400 focus:bg-white"
          />
        </div>
      </div>

      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-zinc-100 text-left text-[11px] font-semibold uppercase tracking-wide text-zinc-400">
            {columns.map((c) => (
              <th key={c.key} className={`px-4 py-2 font-semibold ${c.className ?? ""}`}>
                {c.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-100">
          {pageRows.length === 0 && (
            <tr>
              <td colSpan={columns.length} className="px-4 py-10 text-center text-sm text-zinc-400">
                {query ? "Aramayla eşleşen kayıt yok." : empty}
              </td>
            </tr>
          )}
          {pageRows.map((r) => (
            <tr
              key={rowKey(r)}
              onClick={onRowClick ? () => onRowClick(r) : undefined}
              className={onRowClick ? "cursor-pointer transition-colors hover:bg-zinc-50" : "transition-colors hover:bg-zinc-50"}
            >
              {columns.map((c) => (
                <td key={c.key} className={`px-4 py-2.5 ${c.className ?? ""}`}>
                  {c.render(r)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>

      <div className="flex items-center justify-between border-t border-zinc-100 px-4 py-2">
        <span className="text-[12px] text-zinc-400">
          {filtered.length === 0 ? "0 kayıt" : `${from}-${to} / ${filtered.length}`}
        </span>
        {pages > 1 && (
          <div className="flex items-center gap-1">
            <button
              onClick={() => setPage(Math.max(0, cur - 1))}
              disabled={cur === 0}
              className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 hover:bg-zinc-100 disabled:opacity-30"
              title="Önceki sayfa"
            >
              <CaretLeft size={14} aria-hidden />
            </button>
            <span className="px-1 text-[12px] tabular-nums text-zinc-500">
              {cur + 1} / {pages}
            </span>
            <button
              onClick={() => setPage(Math.min(pages - 1, cur + 1))}
              disabled={cur >= pages - 1}
              className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 hover:bg-zinc-100 disabled:opacity-30"
              title="Sonraki sayfa"
            >
              <CaretRight size={14} aria-hidden />
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
