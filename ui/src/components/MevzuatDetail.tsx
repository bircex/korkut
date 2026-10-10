import { useEffect, useMemo, useState, type ReactNode } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { api, ApiError, type MaddeTreeNode } from "../api";
import { ArrowSquareOut, CaretDown, CaretRight, FileText, MagnifyingGlass, Printer } from "../icons";
import { Spinner } from "../a11y";
import { formatLegalText } from "./legalFormat";

// Mevzuat detayı: mevzuat.adalet.gov.tr'nin "AĞAÇ YAPISI" görünümünün klonu — solda kısım/bölüm/madde
// ağacı, sağda seçili maddenin (veya hiçbiri seçili değilse tüm metnin) içeriği + yazdır düğmesi.
// Sadece source==="mevzuat" kararlar için kullanılır (bkz. DecisionDetail.tsx'teki isMevzuat/
// SmartDecisionDetail) — içtihatların madde ağacı yoktur.

function isLeaf(node: MaddeTreeNode): boolean {
  return node.madde_no != null;
}

function nodeKey(node: MaddeTreeNode, fallback: number): string {
  return node.madde_id ?? node.title ?? `n${fallback}`;
}

// Kaynak veride yaprak `title`'ı çoğu zaman ya boş anlamlı bilgi taşımayan "Madde No: X" tekrarı ya
// da birleştirilmiş bir aralığın bitiş numarasıdır (örn. "MADDE 3- 4- 5-" tek düğümde madde_no="3",
// title="5" olarak gelir) — ikisini de okunur tek satıra indirger.
function leafLabel(node: MaddeTreeNode): string {
  const title = node.title?.trim() ?? "";
  if (!title || /^madde\s*no\b/i.test(title)) return `Madde ${node.madde_no}`;
  if (/^\d+$/.test(title) && title !== node.madde_no) return `Madde ${node.madde_no}-${title}`;
  return `Madde ${node.madde_no}: ${title}`;
}

function matchesQuery(node: MaddeTreeNode, q: string): boolean {
  const hay = `${node.title ?? ""} ${node.madde_no ?? ""}`.toLowerCase();
  if (hay.includes(q)) return true;
  return node.children.some((c) => matchesQuery(c, q));
}

function TreeBranch({
  node,
  depth,
  query,
  selectedId,
  onSelect,
}: {
  node: MaddeTreeNode;
  depth: number;
  query: string;
  selectedId: string | null;
  onSelect: (node: MaddeTreeNode) => void;
}) {
  const [open, setOpen] = useState(depth < 1 || query.length > 0);
  const pad = { paddingLeft: `${8 + depth * 14}px` };

  if (isLeaf(node)) {
    const active = node.madde_id !== null && node.madde_id === selectedId;
    return (
      <button
        onClick={() => onSelect(node)}
        style={pad}
        className={
          "flex w-full items-center gap-1.5 rounded-md py-1.5 pr-2 text-left text-[12.5px] " +
          (active ? "bg-zinc-900 font-medium text-white" : "text-zinc-700 hover:bg-zinc-200/70")
        }
      >
        <FileText size={13} aria-hidden className={"shrink-0 " + (active ? "text-white" : "text-zinc-400")} />
        <span className="truncate">{leafLabel(node)}</span>
      </button>
    );
  }

  const visibleChildren = query ? node.children.filter((c) => matchesQuery(c, query)) : node.children;
  if (query && visibleChildren.length === 0 && !matchesQuery(node, query)) return null;

  return (
    <div>
      <button
        onClick={() => setOpen((o) => !o)}
        style={pad}
        className="flex w-full items-center gap-1 rounded-md py-1.5 pr-2 text-left text-[12.5px] font-medium text-zinc-800 hover:bg-zinc-200/70"
      >
        {open || query ? (
          <CaretDown size={12} aria-hidden className="shrink-0 text-zinc-400" />
        ) : (
          <CaretRight size={12} aria-hidden className="shrink-0 text-zinc-400" />
        )}
        <span className="truncate">{node.title}</span>
      </button>
      {(open || query) &&
        visibleChildren.map((c, i) => (
          <TreeBranch key={nodeKey(c, i)} node={c} depth={depth + 1} query={query} selectedId={selectedId} onSelect={onSelect} />
        ))}
    </div>
  );
}

type TreeState = "loading" | "empty" | { nodes: MaddeTreeNode[] };
// `forKey` ties a result to the mevzuatId+madde_id it was fetched for — the effect below never
// resets doc synchronously on selection change (that's a lint-flagged anti-pattern); instead the
// render compares forKey to the CURRENT selection and shows the spinner when they've drifted apart.
type DocState =
  | { forKey: string; state: "error"; detail: string }
  | { forKey: string; state: "ready"; md: string };

export function MevzuatDetail({
  mevzuatId,
  title,
  url,
  action,
}: {
  mevzuatId: string;
  title: string;
  url?: string | null;
  action?: ReactNode;
}) {
  const [tree, setTree] = useState<TreeState>("loading");
  const [selected, setSelected] = useState<MaddeTreeNode | null>(null);
  const [doc, setDoc] = useState<DocState | null>(null);
  const [query, setQuery] = useState("");

  // mevzuatId only changes via a caller remount (every call site keys by `${source}:${id}`), so the
  // initial useState values above already cover the "just switched document" case.
  useEffect(() => {
    let stale = false;
    api
      .legalMaddeTree(mevzuatId)
      .then((r) => {
        if (!stale) setTree(r.nodes.length ? { nodes: r.nodes } : "empty");
      })
      .catch(() => {
        if (!stale) setTree("empty");
      });
    return () => {
      stale = true;
    };
  }, [mevzuatId]);

  const docKey = `${mevzuatId}:${selected?.madde_id ?? ""}`;
  useEffect(() => {
    let stale = false;
    api
      .legalDocument("mevzuat", mevzuatId, selected?.madde_id ?? undefined)
      .then((d) => {
        if (!stale) setDoc({ forKey: docKey, state: "ready", md: formatLegalText(d.content_md) });
      })
      .catch((err) => {
        if (!stale) {
          setDoc({ forKey: docKey, state: "error", detail: err instanceof ApiError ? err.message : "Belge getirilemedi." });
        }
      });
    return () => {
      stale = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- docKey is derived from these two
  }, [mevzuatId, selected?.madde_id]);

  const docLoading = doc === null || doc.forKey !== docKey;
  const lowerQuery = query.trim().toLowerCase();
  const treeNodes = tree !== "loading" && tree !== "empty" ? tree.nodes : [];

  const printLabel = useMemo(
    () => (selected ? `${title} — Madde ${selected.madde_no}` : title),
    [title, selected],
  );

  return (
    <div className="flex min-h-0 flex-1">
      <div className="flex w-60 shrink-0 flex-col border-r border-zinc-200 bg-zinc-50">
        <div className="relative p-2">
          <MagnifyingGlass size={13} aria-hidden className="pointer-events-none absolute top-1/2 left-4 -translate-y-1/2 text-zinc-400" />
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Maddelerde ara…"
            className="w-full rounded-md border border-zinc-300 bg-white py-1.5 pr-2 pl-7 text-[12.5px] text-zinc-800 placeholder:text-zinc-400"
          />
        </div>
        <div className="flex-1 overflow-y-auto px-1.5 pb-2">
          {tree === "loading" && (
            <div className="flex justify-center py-6">
              <Spinner label="Ağaç yükleniyor…" />
            </div>
          )}
          {tree === "empty" && (
            <p className="px-2 py-3 text-[12px] text-zinc-400">Bu mevzuat için madde ağacı yayımlanmamış.</p>
          )}
          {treeNodes.length > 0 && (
            <button
              onClick={() => setSelected(null)}
              className={
                "mb-1 flex w-full items-center gap-1.5 rounded-md px-2 py-1.5 text-left text-[12.5px] font-medium " +
                (selected === null ? "bg-zinc-900 text-white" : "text-zinc-700 hover:bg-zinc-200/70")
              }
            >
              Tüm metin
            </button>
          )}
          {treeNodes.map((n, i) => (
            <TreeBranch key={nodeKey(n, i)} node={n} depth={0} query={lowerQuery} selectedId={selected?.madde_id ?? null} onSelect={setSelected} />
          ))}
        </div>
      </div>

      <div className="flex min-w-0 flex-1 flex-col">
        <div className="flex items-center gap-1.5 border-b border-zinc-200 px-3 py-2">
          <button
            onClick={() => window.print()}
            title="Yazdır"
            className="rounded-md p-1.5 text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900"
          >
            <Printer size={16} aria-hidden />
          </button>
          <span className="ml-auto flex items-center gap-1.5">
            {action}
            {url && (
              <a
                href={url}
                target="_blank"
                rel="noopener noreferrer"
                title="Kaynakta aç"
                className="shrink-0 rounded-md p-1 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
              >
                <ArrowSquareOut size={15} aria-hidden />
              </a>
            )}
          </span>
        </div>
        <div className="print-area min-h-0 flex-1 overflow-y-auto px-5 py-4 text-[13.5px] leading-relaxed text-zinc-800">
          <h3 className="text-[14px] font-semibold leading-snug text-zinc-900">{printLabel}</h3>
          {docLoading && (
            <div className="flex justify-center py-8">
              <Spinner label="Belge yükleniyor…" />
            </div>
          )}
          {!docLoading && doc && doc.state === "error" && (
            <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5 text-[13px] text-amber-800">
              {doc.detail}
              {url && (
                <>
                  {" "}
                  <a href={url} target="_blank" rel="noopener noreferrer" className="font-medium underline">
                    Kaynakta açmayı deneyin
                  </a>
                  .
                </>
              )}
            </div>
          )}
          {!docLoading && doc && doc.state === "ready" && (
            <div className="md mt-3">
              <ReactMarkdown remarkPlugins={[remarkGfm]}>{doc.md}</ReactMarkdown>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
