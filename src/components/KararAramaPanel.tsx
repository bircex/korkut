import { useState, type ReactNode } from "react";
import { api, ApiError, type LawSearchResult } from "../api";
import {
  ArrowsClockwise,
  ArrowSquareOut,
  BookOpenText,
  CaretDown,
  Eye,
  Funnel,
  MagnifyingGlass,
  Warning,
} from "../icons";
import { Spinner } from "../a11y";
import { RightDrawer } from "./RightDrawer";
import { SmartDecisionDetail, drawerWidthFor } from "./DecisionDetail";
import { SaveDecisionButton } from "./SaveDecisionButton";

// Karar Arama: Araçlar altında bağımsız bir sayfa. Sohbetteki "İlgili Kararlar" drawer'ıyla aynı
// backend'i (ILawClientsSearchService/ILawClientsDocumentService, /api/legal/search+documents)
// kullanır. Sonuç listesi sayfada kalır, bir sonuca tıklanınca tam metin sağ drawer'da açılır
// (liste kaybolmaz); hem liste kartından hem drawer'dan Kararlarıma ekle mümkündür.

type FieldKind =
  | "chamber"
  | "decisionType"
  | "applicationType"
  | "scope"
  | "legislationType"
  | "searchScope"
  | "tamCumle"
  | "mevzuatNo"
  | "gazetteNumber";

type SourceMeta = {
  value: string;
  label: string;
  sortable: boolean;
  dateFilter: boolean;
  dateLabel?: string;
  fields: FieldKind[];
  decisionTypeOptions?: { value: string; label: string }[];
};

const SOURCES: SourceMeta[] = [
  { value: "yargitay", label: "Yargıtay", sortable: true, dateFilter: true, fields: ["chamber"] },
  { value: "danistay", label: "Danıştay", sortable: true, dateFilter: true, fields: ["chamber"] },
  { value: "emsal", label: "Bölge Adliye / İstinaf", sortable: true, dateFilter: true, fields: [] },
  { value: "kyb", label: "Kanun Yararına Bozma", sortable: true, dateFilter: true, fields: [] },
  { value: "anayasa", label: "Anayasa Mahkemesi", sortable: false, dateFilter: false, fields: ["applicationType"] },
  {
    value: "mevzuat",
    label: "Mevzuat",
    sortable: false,
    dateFilter: true,
    dateLabel: "Resmî Gazete başlangıç tarihi",
    fields: ["legislationType", "searchScope", "tamCumle", "mevzuatNo", "gazetteNumber"],
  },
  { value: "gib", label: "Gelir İdaresi (Özelgeler)", sortable: false, dateFilter: false, fields: [] },
  {
    value: "kik",
    label: "Kamu İhale Kurulu",
    sortable: false,
    dateFilter: true,
    fields: ["decisionType"],
    decisionTypeOptions: [
      { value: "uyusmazlik", label: "Uyuşmazlık" },
      { value: "duzenleyici", label: "Düzenleyici" },
      { value: "mahkeme", label: "Mahkeme" },
    ],
  },
  { value: "rekabet", label: "Rekabet Kurumu", sortable: false, dateFilter: false, fields: ["decisionType"] },
  {
    value: "sayistay",
    label: "Sayıştay",
    sortable: false,
    dateFilter: false,
    fields: ["decisionType"],
    decisionTypeOptions: [
      { value: "genel_kurul", label: "Genel Kurul" },
      { value: "temyiz_kurulu", label: "Temyiz Kurulu" },
      { value: "daire", label: "Daire" },
    ],
  },
  { value: "uyusmazlik", label: "Uyuşmazlık Mahkemesi", sortable: false, dateFilter: false, fields: ["scope"] },
  { value: "bddk", label: "BDDK", sortable: false, dateFilter: false, fields: [] },
  { value: "kvkk", label: "KVKK", sortable: false, dateFilter: false, fields: [] },
  { value: "sigorta_tahkim", label: "Sigorta Tahkim", sortable: false, dateFilter: false, fields: [] },
];

const LEGISLATION_TYPES = [
  { value: "KANUN", label: "Kanun" },
  { value: "CB_KARARNAME", label: "CB Kararnamesi" },
  { value: "YONETMELIK", label: "Yönetmelik" },
  { value: "CB_YONETMELIK", label: "CB Yönetmeliği" },
  { value: "CB_KARAR", label: "CB Kararı" },
  { value: "CB_GENELGE", label: "CB Genelgesi" },
  { value: "KHK", label: "KHK" },
  { value: "TUZUK", label: "Tüzük" },
  { value: "KKY", label: "Kurum Yönetmeliği" },
  { value: "UY", label: "Üniversite Yönetmeliği" },
  { value: "TEBLIGLER", label: "Tebliğ" },
  { value: "MULGA", label: "Mülga" },
];

// Detaylı Arama alanı görünümü — UYAP Mevzuat/İçtihat'ın "Detaylı Arama" kartıyla aynı kalıp
// (etiket üstte, tekdüze input stili, ızgara düzeni). Tüm filtre alanları bunu paylaşır.
const INPUT_CLS =
  "w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-[13.5px] text-zinc-800 placeholder:text-zinc-400 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-zinc-900";

function Field({
  label,
  required,
  className = "",
  children,
}: {
  label: string;
  required?: boolean;
  className?: string;
  children: ReactNode;
}) {
  return (
    <label className={`flex flex-col gap-1.5 text-[12.5px] font-medium text-zinc-600 ${className}`}>
      <span>
        {label}
        {required && <span className="text-red-500"> *</span>}
      </span>
      {children}
    </label>
  );
}

// UYAP'ın "Detaylı İçtihat Arama" sonuç tablosuyla aynı sütun düzeni (Karar No/Esas No/Daire-Birim/
// Mahkeme/Karar Türü/Karar Tarihi/Kesinleşme Durumu) — bunlar backend'in Bedesten kaynaklarından
// (yargitay/danistay/emsal/kyb) 1:1 geldiği alanlar; diğer kaynaklarda "–" görünür. "Konu" sütunu
// mevzuat/diğer kaynaklarda tek anlamlı ayırt edici olduğu için referansa ek olarak baştadır.
const TH_CLS = "px-3.5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-zinc-500";
const TD_CLS = "px-3.5 py-2.5 text-zinc-600";

function cell(value?: string | null): string {
  return value && value.trim() ? value : "–";
}

function ResultsTable({ results, onOpen }: { results: LawSearchResult[]; onOpen: (r: LawSearchResult) => void }) {
  return (
    <div className="overflow-x-auto rounded-t-xl">
      <table className="w-full min-w-[980px] border-collapse text-[13px]">
        <thead>
          <tr className="border-b border-zinc-200 bg-zinc-50">
            <th className={TH_CLS}>Konu</th>
            <th className={TH_CLS}>Esas No</th>
            <th className={TH_CLS}>Karar No</th>
            <th className={TH_CLS}>Daire / Birim</th>
            <th className={TH_CLS}>Mahkeme</th>
            <th className={TH_CLS}>Karar Türü</th>
            <th className={TH_CLS}>Karar Tarihi</th>
            <th className={TH_CLS}>Kesinleşme Durumu</th>
            <th className={`${TH_CLS} text-right`}>İşlemler</th>
          </tr>
        </thead>
        <tbody>
          {results.map((r, i) => (
            <tr
              key={`${r.source}:${r.id}`}
              onClick={() => onOpen(r)}
              className={"cursor-pointer border-b border-zinc-100 last:border-0 hover:bg-zinc-100/70 " + (i % 2 ? "bg-zinc-50/60" : "bg-white")}
            >
              <td className={`${TD_CLS} max-w-[260px] truncate font-medium text-zinc-900`}>{r.title}</td>
              <td className={`${TD_CLS} whitespace-nowrap`}>{cell(r.case_number)}</td>
              <td className={`${TD_CLS} whitespace-nowrap`}>{cell(r.decision_number)}</td>
              <td className={TD_CLS}>{cell(r.chamber ?? r.court)}</td>
              <td className={TD_CLS}>{cell(r.court_type)}</td>
              <td className={TD_CLS}>{cell(r.decision_type)}</td>
              <td className={`${TD_CLS} whitespace-nowrap`}>{cell(r.date)}</td>
              <td className={TD_CLS}>{cell(r.finalization_status)}</td>
              <td className="px-3.5 py-2.5" onClick={(e) => e.stopPropagation()}>
                <div className="flex items-center justify-end gap-1">
                  <SaveDecisionButton decision={r} />
                  <button
                    onClick={() => onOpen(r)}
                    title="Görüntüle"
                    className="rounded-md p-1.5 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
                  >
                    <Eye size={15} aria-hidden />
                  </button>
                  {r.url && (
                    <a
                      href={r.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      title="Kaynakta aç"
                      className="rounded-md p-1.5 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
                    >
                      <ArrowSquareOut size={15} aria-hidden />
                    </a>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

const PAGE_SIZES = [10, 20, 50, 100];

function pageList(current: number, total: number): (number | "...")[] {
  if (total <= 6) return Array.from({ length: total }, (_, i) => i + 1);
  if (current <= 5) return [1, 2, 3, 4, 5, "...", total];
  if (current >= total - 1) return [1, "...", total - 4, total - 3, total - 2, total - 1, total];
  return [1, "...", current - 1, current, current + 1, "...", total];
}

function Pagination({
  page,
  pageSize,
  total,
  onPage,
  onPageSize,
}: {
  page: number;
  pageSize: number;
  total: number;
  onPage: (p: number) => void;
  onPageSize: (n: number) => void;
}) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-b-xl border-t border-zinc-100 bg-white px-3.5 py-3">
      <div className="flex items-center gap-1">
        {PAGE_SIZES.map((n) => (
          <button
            key={n}
            onClick={() => onPageSize(n)}
            className={
              "rounded-md px-2.5 py-1 text-[12.5px] font-medium " +
              (n === pageSize ? "bg-zinc-900 text-white" : "text-zinc-500 hover:bg-zinc-100")
            }
          >
            {n}
          </button>
        ))}
      </div>
      <div className="flex items-center gap-3 text-[12.5px] text-zinc-500">
        <span>
          Sayfa {page} / {totalPages} ({total.toLocaleString("tr-TR")} Kayıt)
        </span>
        <div className="flex items-center gap-0.5">
          {pageList(page, totalPages).map((p, i) =>
            p === "..." ? (
              <span key={`e${i}`} className="px-1.5 text-zinc-400">
                …
              </span>
            ) : (
              <button
                key={p}
                onClick={() => onPage(p)}
                className={
                  "min-w-[26px] rounded-md px-2 py-1 text-center font-medium " +
                  (p === page ? "bg-zinc-900 text-white" : "text-zinc-600 hover:bg-zinc-100")
                }
              >
                {p}
              </button>
            ),
          )}
        </div>
      </div>
    </div>
  );
}

type SearchState =
  | { state: "idle" }
  | { state: "loading" }
  | { state: "error"; detail: string }
  | { state: "ready"; total: number | null; results: LawSearchResult[] };

export function KararAramaPanel() {
  const [source, setSource] = useState("yargitay");
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState<"relevance" | "date">("relevance");
  const [dateStart, setDateStart] = useState("");
  const [dateEnd, setDateEnd] = useState("");
  const [chamber, setChamber] = useState("");
  const [decisionType, setDecisionType] = useState("");
  const [applicationType, setApplicationType] = useState("");
  const [scope, setScope] = useState("");
  const [legislationType, setLegislationType] = useState("");
  const [searchScope, setSearchScope] = useState("");
  const [tamCumle, setTamCumle] = useState(false);
  const [mevzuatNo, setMevzuatNo] = useState("");
  const [gazetteNumber, setGazetteNumber] = useState("");
  const [showFilters, setShowFilters] = useState(false);
  const [search, setSearch] = useState<SearchState>({ state: "idle" });
  const [detail, setDetail] = useState<LawSearchResult | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const sourceMeta = SOURCES.find((s) => s.value === source) ?? SOURCES[0];
  const has = (f: FieldKind) => sourceMeta.fields.includes(f);

  function clearFilters() {
    setSort("relevance");
    setDateStart("");
    setDateEnd("");
    setChamber("");
    setDecisionType("");
    setApplicationType("");
    setScope("");
    setLegislationType("");
    setSearchScope("");
    setTamCumle(false);
    setMevzuatNo("");
    setGazetteNumber("");
  }

  const activeFilterCount = [
    sourceMeta.dateFilter && (dateStart || dateEnd),
    has("chamber") && chamber,
    has("applicationType") && applicationType,
    has("scope") && scope,
    has("decisionType") && decisionType,
    has("legislationType") && legislationType,
    has("searchScope") && searchScope,
    has("tamCumle") && tamCumle,
    has("mevzuatNo") && mevzuatNo,
    has("gazetteNumber") && gazetteNumber,
  ].filter(Boolean).length;

  // targetPage/targetLimit dışarıdan verilir (sayfalama/sayfa boyutu tıklaması) — state'ten okumak
  // yerine böylece setPageSize sonrası hemen aynı istekte kullanılabilir (React state güncellemesi
  // asenkron olduğu için kapanıştaki `pageSize` bir sonraki render'a kadar eski değeri taşır).
  async function runSearch(opts?: { e?: React.FormEvent; page?: number; limit?: number }) {
    opts?.e?.preventDefault();
    if (!query.trim()) return;
    const targetPage = opts?.page ?? 1;
    const targetLimit = opts?.limit ?? pageSize;
    setDetail(null);
    setPage(targetPage);
    if (opts?.limit) setPageSize(opts.limit);
    setSearch({ state: "loading" });
    try {
      const res = await api.legalSearch({
        source,
        query: query.trim(),
        page: targetPage,
        limit: targetLimit,
        sort: sourceMeta.sortable ? sort : undefined,
        dateStart: sourceMeta.dateFilter && dateStart ? dateStart : undefined,
        dateEnd: sourceMeta.dateFilter && dateEnd ? dateEnd : undefined,
        chamber: has("chamber") && chamber ? chamber : undefined,
        decisionType: has("decisionType") && decisionType ? decisionType : undefined,
        applicationType: has("applicationType") && applicationType ? applicationType : undefined,
        scope: has("scope") && scope ? scope : undefined,
        legislationType: has("legislationType") && legislationType ? legislationType : undefined,
        searchScope: has("searchScope") && searchScope ? searchScope : undefined,
        tamCumle: has("tamCumle") && tamCumle ? true : undefined,
        mevzuatNo: has("mevzuatNo") && mevzuatNo ? mevzuatNo : undefined,
        gazetteNumber: has("gazetteNumber") && gazetteNumber ? gazetteNumber : undefined,
      });
      setSearch({ state: "ready", total: res.total, results: res.results });
    } catch (err) {
      setSearch({
        state: "error",
        detail: err instanceof ApiError ? err.message : "Arama yapılamadı, lütfen tekrar deneyin.",
      });
    }
  }

  return (
    <div className="flex min-w-0 flex-1">
      <div className="min-w-0 flex-1 overflow-y-auto px-6 py-6">
      <div className="mx-auto w-full max-w-4xl">
        <form onSubmit={(e) => runSearch({ e })} className="rounded-2xl border border-zinc-200 bg-white shadow-sm">
          <div className="flex items-center gap-2 border-b border-zinc-100 px-5 py-3.5">
            <Funnel size={16} aria-hidden className="text-zinc-500" />
            <h2 className="text-[14.5px] font-semibold text-zinc-900">Detaylı Arama</h2>
            {activeFilterCount > 0 && (
              <span className="rounded-full bg-zinc-900 px-1.5 py-0.5 text-[10px] font-semibold text-white">
                {activeFilterCount}
              </span>
            )}
            <button
              type="button"
              onClick={() => setShowFilters((v) => !v)}
              className="ml-auto flex items-center gap-1 text-[12.5px] font-medium text-zinc-500 hover:text-zinc-800"
            >
              {showFilters ? "Basit Sorgulama" : "Gelişmiş Sorgulama"}
              <CaretDown size={11} aria-hidden className={"transition-transform " + (showFilters ? "rotate-180" : "")} />
            </button>
          </div>

          <div className="p-5">
            <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2 lg:grid-cols-3">
              <Field label="Kaynak">
                <select value={source} onChange={(e) => setSource(e.target.value)} className={INPUT_CLS}>
                  {SOURCES.map((s) => (
                    <option key={s.value} value={s.value}>
                      {s.label}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Arama Terimi" required className="sm:col-span-2">
                <div className="relative">
                  <MagnifyingGlass
                    size={15}
                    aria-hidden
                    className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-zinc-400"
                  />
                  <input
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
                    placeholder="örn. kira artışı TÜFE"
                    className={`${INPUT_CLS} pl-9`}
                  />
                </div>
              </Field>

              {showFilters && (
                <>
                  {sourceMeta.sortable && (
                    <Field label="Sıralama">
                      <select
                        value={sort}
                        onChange={(e) => setSort(e.target.value as "relevance" | "date")}
                        className={INPUT_CLS}
                      >
                        <option value="relevance">Alaka</option>
                        <option value="date">En yeni</option>
                      </select>
                    </Field>
                  )}
                  {sourceMeta.dateFilter && (
                    <>
                      <Field label={sourceMeta.dateLabel ?? "Başlangıç tarihi"}>
                        <input
                          type="date"
                          value={dateStart}
                          onChange={(e) => setDateStart(e.target.value)}
                          className={INPUT_CLS}
                        />
                      </Field>
                      <Field label="Bitiş tarihi">
                        <input
                          type="date"
                          value={dateEnd}
                          onChange={(e) => setDateEnd(e.target.value)}
                          className={INPUT_CLS}
                        />
                      </Field>
                    </>
                  )}
                  {has("chamber") && (
                    <Field label="Daire">
                      <input
                        value={chamber}
                        onChange={(e) => setChamber(e.target.value)}
                        placeholder="örn. 3. Hukuk Dairesi"
                        className={INPUT_CLS}
                      />
                    </Field>
                  )}
                  {has("applicationType") && (
                    <Field label="Başvuru türü">
                      <select value={applicationType} onChange={(e) => setApplicationType(e.target.value)} className={INPUT_CLS}>
                        <option value="">(tümü)</option>
                        <option value="norm">Norm denetimi</option>
                        <option value="individual">Bireysel başvuru</option>
                      </select>
                    </Field>
                  )}
                  {has("scope") && (
                    <Field label="Kapsam">
                      <select value={scope} onChange={(e) => setScope(e.target.value)} className={INPUT_CLS}>
                        <option value="">(tümü)</option>
                        <option value="all">Tümü</option>
                        <option value="esas">Esas</option>
                        <option value="karar">Karar</option>
                      </select>
                    </Field>
                  )}
                  {has("decisionType") &&
                    (sourceMeta.decisionTypeOptions ? (
                      <Field label="Karar türü">
                        <select value={decisionType} onChange={(e) => setDecisionType(e.target.value)} className={INPUT_CLS}>
                          <option value="">(tümü)</option>
                          {sourceMeta.decisionTypeOptions.map((o) => (
                            <option key={o.value} value={o.value}>
                              {o.label}
                            </option>
                          ))}
                        </select>
                      </Field>
                    ) : (
                      <Field label="Karar türü">
                        <input
                          value={decisionType}
                          onChange={(e) => setDecisionType(e.target.value)}
                          placeholder="karar türü"
                          className={INPUT_CLS}
                        />
                      </Field>
                    ))}
                  {has("legislationType") && (
                    <Field label="Mevzuat türü">
                      <select value={legislationType} onChange={(e) => setLegislationType(e.target.value)} className={INPUT_CLS}>
                        <option value="">(tümü)</option>
                        {LEGISLATION_TYPES.map((o) => (
                          <option key={o.value} value={o.value}>
                            {o.label}
                          </option>
                        ))}
                      </select>
                    </Field>
                  )}
                  {has("searchScope") && (
                    <Field label="Arama kapsamı">
                      <select value={searchScope} onChange={(e) => setSearchScope(e.target.value)} className={INPUT_CLS}>
                        <option value="">Başlıkta (varsayılan)</option>
                        <option value="content">Metin içinde</option>
                        <option value="title_and_content">Başlık + metin</option>
                      </select>
                    </Field>
                  )}
                  {has("mevzuatNo") && (
                    <Field label="Mevzuat no">
                      <input
                        value={mevzuatNo}
                        onChange={(e) => setMevzuatNo(e.target.value)}
                        placeholder="örn. 5237"
                        className={INPUT_CLS}
                      />
                    </Field>
                  )}
                  {has("gazetteNumber") && (
                    <Field label="RG sayısı">
                      <input
                        value={gazetteNumber}
                        onChange={(e) => setGazetteNumber(e.target.value)}
                        placeholder="örn. 25611"
                        className={INPUT_CLS}
                      />
                    </Field>
                  )}
                  {has("tamCumle") && (
                    <label className="flex items-center gap-1.5 self-end pb-2 text-[12.5px] font-medium text-zinc-600">
                      <input type="checkbox" checked={tamCumle} onChange={(e) => setTamCumle(e.target.checked)} />
                      Tam ifade eşleştir
                    </label>
                  )}
                </>
              )}
            </div>

            {showFilters && sourceMeta.fields.length === 0 && !sourceMeta.dateFilter && !sourceMeta.sortable && (
              <div className="mt-3.5 flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5 text-[12.5px] text-amber-800">
                <Warning size={15} aria-hidden className="mt-0.5 shrink-0" />
                {sourceMeta.label} için ek filtre bulunmuyor — yalnızca kaynak ve arama terimiyle sorgulanır.
              </div>
            )}

            <div className="mt-4 flex items-center justify-end gap-2 border-t border-zinc-100 pt-4">
              <button
                type="button"
                onClick={clearFilters}
                className="flex items-center gap-1.5 rounded-lg border border-zinc-300 bg-white px-3.5 py-2 text-[13.5px] font-medium text-zinc-600 hover:bg-zinc-50"
              >
                <ArrowsClockwise size={14} aria-hidden />
                Temizle
              </button>
              <button
                type="submit"
                disabled={!query.trim() || search.state === "loading"}
                className="flex items-center gap-1.5 rounded-lg bg-zinc-900 px-4 py-2 text-[13.5px] font-medium text-white hover:bg-zinc-800 disabled:opacity-40"
              >
                <MagnifyingGlass size={14} aria-hidden />
                {search.state === "loading" ? "Aranıyor…" : "Sorgula"}
              </button>
            </div>
          </div>
        </form>

        <div className="mt-6">
          {search.state === "idle" && (
            <p className="text-center text-[13px] text-zinc-400">
              Yargıtay, Danıştay, Anayasa Mahkemesi ve mevzuatta arama yapmak için bir sorgu girin.
            </p>
          )}
          {search.state === "loading" && (
            <div className="flex justify-center py-10">
              <Spinner label="Aranıyor…" />
            </div>
          )}
          {search.state === "error" && (
            <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5 text-[13px] text-amber-800">
              <Warning size={16} aria-hidden className="mt-0.5 shrink-0" />
              {search.detail}
            </div>
          )}
          {search.state === "ready" &&
            (search.results.length === 0 ? (
              <div className="flex flex-col items-center gap-2 rounded-xl border border-zinc-200 bg-white py-10 text-center text-zinc-400">
                <BookOpenText size={28} aria-hidden />
                <p className="text-[13px]">Sonuç bulunamadı. Farklı bir sorgu veya kaynak deneyin.</p>
              </div>
            ) : (
              <div className="rounded-xl border border-zinc-200 bg-white shadow-sm">
                <ResultsTable results={search.results} onOpen={setDetail} />
                {search.total != null ? (
                  <Pagination
                    page={page}
                    pageSize={pageSize}
                    total={search.total}
                    onPage={(p) => runSearch({ page: p })}
                    onPageSize={(n) => runSearch({ page: 1, limit: n })}
                  />
                ) : (
                  <div className="border-t border-zinc-100 px-3.5 py-3 text-[12.5px] text-zinc-500">
                    {search.results.length} sonuç
                  </div>
                )}
              </div>
            ))}
        </div>
      </div>
      </div>

      <RightDrawer
        open={detail !== null}
        onClose={() => setDetail(null)}
        label="Karar detayı"
        title={detail?.title ?? "Karar detayı"}
        width={drawerWidthFor(detail)}
      >
        {detail && (
          <SmartDecisionDetail
            key={`${detail.source}:${detail.id}`}
            decision={detail}
            action={<SaveDecisionButton decision={detail} size="md" />}
          />
        )}
      </RightDrawer>
    </div>
  );
}
