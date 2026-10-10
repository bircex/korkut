import { useMemo, useState } from "react";
import { ArrowLeft, Calculator, MagnifyingGlass } from "../icons";
import { usePath, navigate, matchPath } from "../router";
import { CALC_CATEGORIES, CALC_DEFINITIONS, type CalcDefinition } from "../calcDefinitions";
import { CalculatorForm } from "./CalculatorForm";

// Araçlar > Hesap Makineleri: kategori gruplu kart listesi + arama; bir karta tıklayınca liste
// yerini forma bırakır (/hesap-makineleri/:slug), MevzuatDetail/docRoute'un liste↔detay kalıbıyla
// aynı — drawer değil, form+sonuç tam genişlik yer istiyor.

const BASE_PATH = "/hesap-makineleri";

function slugFromPath(path: string): string | null {
  const m = matchPath(`${BASE_PATH}/:slug`, path);
  return m?.slug ?? null;
}

function CalcCard({ def, onOpen }: { def: CalcDefinition; onOpen: () => void }) {
  return (
    <button
      onClick={onOpen}
      className="flex flex-col items-start gap-1.5 rounded-xl border border-zinc-200 bg-white p-4 text-left shadow-sm transition-colors hover:border-zinc-300 hover:bg-zinc-50"
    >
      <div className="text-[13.5px] font-semibold text-zinc-900">{def.title}</div>
      <p className="text-[12.5px] leading-snug text-zinc-500">{def.description}</p>
    </button>
  );
}

function CalculatorsList() {
  const [query, setQuery] = useState("");

  const grouped = useMemo(() => {
    const q = query.trim().toLocaleLowerCase("tr-TR");
    const match = (d: CalcDefinition) =>
      !q ||
      d.title.toLocaleLowerCase("tr-TR").includes(q) ||
      d.category.toLocaleLowerCase("tr-TR").includes(q) ||
      d.description.toLocaleLowerCase("tr-TR").includes(q);
    return CALC_CATEGORIES.map((cat) => ({
      category: cat,
      items: CALC_DEFINITIONS.filter((d) => d.category === cat && match(d)),
    })).filter((g) => g.items.length > 0);
  }, [query]);

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 px-4 py-6">
      <div>
        <h1 className="flex items-center gap-2 text-lg font-semibold text-zinc-900">
          <Calculator size={20} aria-hidden />
          Hesap Makineleri
        </h1>
        <p className="mt-1 text-[13px] text-zinc-500">
          İş, aile, ceza, vergi ve sosyal güvenlik alanlarında {CALC_DEFINITIONS.length} hesap makinesi.
        </p>
      </div>

      <div className="relative max-w-sm">
        <MagnifyingGlass size={14} aria-hidden className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Hesap makinesi ara…"
          className="w-full rounded-lg border border-zinc-300 bg-white py-2 pl-8 pr-3 text-[13.5px] outline-none focus:border-zinc-400"
        />
      </div>

      {grouped.length === 0 ? (
        <p className="py-10 text-center text-[13px] text-zinc-400">Aramayla eşleşen hesap makinesi yok.</p>
      ) : (
        grouped.map((g) => (
          <div key={g.category}>
            <h2 className="mb-2.5 text-[11px] font-semibold uppercase tracking-wider text-zinc-400">{g.category}</h2>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {g.items.map((d) => (
                <CalcCard key={d.slug} def={d} onOpen={() => navigate(`${BASE_PATH}/${d.slug}`)} />
              ))}
            </div>
          </div>
        ))
      )}
    </div>
  );
}

export function CalculatorsPanel() {
  const path = usePath();
  const slug = slugFromPath(path);
  const def = slug ? CALC_DEFINITIONS.find((d) => d.slug === slug) : null;

  if (slug && def) {
    return (
      <div className="flex min-w-0 flex-1 flex-col overflow-y-auto">
        <div className="sticky top-0 z-10 border-b border-zinc-100 bg-white/90 px-4 py-3 backdrop-blur">
          <button
            onClick={() => navigate(BASE_PATH)}
            className="flex items-center gap-1.5 text-[13px] font-medium text-zinc-500 hover:text-zinc-800"
          >
            <ArrowLeft size={14} aria-hidden />
            Hesap Makineleri
          </button>
          <h1 className="mt-1 text-[15px] font-semibold text-zinc-900">{def.title}</h1>
        </div>
        <CalculatorForm key={def.slug} def={def} />
      </div>
    );
  }

  return (
    <div className="min-w-0 flex-1 overflow-y-auto">
      <CalculatorsList />
    </div>
  );
}
