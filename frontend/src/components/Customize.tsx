import { useEffect, useMemo, useState } from "react";
import {
  api,
  type TemplateCategory,
  type TemplateDetail,
  type TemplateKind,
  type TemplateScope,
  type User,
} from "../api";
import { DocumentEditor } from "./DocumentEditor";
import { CaretRight, Check } from "../icons";

// Turkish-aware fold for the search box (diacritics → ascii), mirroring the backend.
const FOLD: Record<string, string> = {
  ç: "c", ğ: "g", ı: "i", İ: "i", ö: "o", ş: "s", ü: "u", â: "a", î: "i", û: "u",
};
const fold = (s: string) => s.toLowerCase().replace(/[çğıİöşüâîû]/g, (c) => FOLD[c] ?? c);

const KIND_LABEL: Record<TemplateKind, string> = { dilekce: "Dilekçeler", sozlesme: "Sözleşmeler" };

function contentFor(detail: TemplateDetail, scope: TemplateScope): string {
  const override = scope === "user" ? detail.userOverride : detail.officeOverride;
  return override ?? detail.effective ?? detail.default ?? "";
}

export function Customize({ user }: { user: User }) {
  const [kind, setKind] = useState<TemplateKind>("dilekce");
  const [cats, setCats] = useState<TemplateCategory[]>([]);
  const [query, setQuery] = useState("");
  const [detail, setDetail] = useState<TemplateDetail | null>(null); // null → list view
  const [scope, setScope] = useState<TemplateScope>("user");
  const [content, setContent] = useState(""); // feeds the editor (re-fed on type/scope change)
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);

  async function refreshList() {
    const data = await api.listTemplates(kind);
    setCats(data.kategoriler);
  }

  useEffect(() => {
    api
      .listTemplates(kind)
      .then((d) => setCats(d.kategoriler))
      .catch(() => setCats([]));
  }, [kind]);

  function chooseKind(next: TemplateKind) {
    if (next === kind) return;
    setKind(next);
  }

  async function openType(id: string) {
    const d = await api.getTemplate(id);
    const startScope: TemplateScope = d.userOverride !== null ? "user" : "office";
    setScope(startScope);
    setContent(contentFor(d, startScope));
    setSaved(false);
    setDetail(d); // switches to editor view
  }

  function switchScope(next: TemplateScope) {
    if (!detail) return;
    setScope(next);
    setContent(contentFor(detail, next));
    setSaved(false);
  }

  async function saveOverride(markdown: string) {
    if (!detail) return;
    await api.saveTemplate(detail.id, scope, markdown);
    const [d] = await Promise.all([api.getTemplate(detail.id), refreshList()]);
    setDetail(d);
    setContent(contentFor(d, scope));
    setSaved(true);
  }

  async function revert() {
    if (!detail) return;
    const label = scope === "user" ? "kişisel" : "ofis";
    if (!confirm(`Bu tipin ${label} özelleştirmesi silinip varsayılana dönülsün mü?`)) return;
    setBusy(true);
    try {
      await api.deleteTemplate(detail.id, scope);
      const [d] = await Promise.all([api.getTemplate(detail.id), refreshList()]);
      setDetail(d);
      setContent(contentFor(d, scope));
      setSaved(false);
    } finally {
      setBusy(false);
    }
  }

  const filtered = useMemo(() => {
    const q = fold(query.trim());
    if (!q) return cats;
    return cats
      .map((c) => ({ ...c, tipler: c.tipler.filter((t) => fold(`${t.ad} ${t.id}`).includes(q)) }))
      .filter((c) => c.tipler.length > 0);
  }, [cats, query]);

  // ---- Editor view (the shared DocumentEditor) ----
  if (detail) {
    const hasOverride = (scope === "user" ? detail.userOverride : detail.officeOverride) !== null;
    const scopeToggle = (
      <div className="flex gap-1 rounded-lg bg-zinc-100 p-1 text-sm">
        {(["user", "office"] as TemplateScope[]).map((sc) => (
          <button
            key={sc}
            onClick={() => switchScope(sc)}
            className={`rounded-md px-3 py-1 ${
              scope === sc ? "bg-white font-medium text-zinc-900 shadow-sm" : "text-zinc-500"
            }`}
          >
            {sc === "user" ? "Kişisel" : "Ofis geneli"}
          </button>
        ))}
      </div>
    );
    const footer = (
      <div className="flex items-center gap-3">
        <span className="text-xs text-zinc-400">
          {hasOverride
            ? scope === "user"
              ? "Kişisel özelleştirme aktif."
              : "Ofis özelleştirmesi aktif."
            : "Varsayılan tema gösteriliyor."}
        </span>
        {saved && (
          <span className="inline-flex items-center gap-1 text-xs text-emerald-600">
            Kaydedildi <Check size={12} weight="bold" aria-hidden />
          </span>
        )}
        {hasOverride && (
          <button
            onClick={revert}
            disabled={busy}
            className="ml-auto rounded-lg border border-zinc-200 px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-50 disabled:opacity-50"
          >
            Varsayılana dön
          </button>
        )}
      </div>
    );
    return (
      <DocumentEditor
        value={content}
        title={detail.ad}
        subtitle={detail.id}
        onClose={() => setDetail(null)}
        onSave={saveOverride}
        headerExtra={scopeToggle}
        footer={footer}
        exportTitle={detail.ad}
      />
    );
  }

  // ---- List (browse) view ----
  return (
    <div className="flex h-full min-w-0 flex-1 flex-col bg-white">
      <header className="flex items-center gap-3 border-b border-zinc-200 px-4 py-3">
        <h1 className="text-base font-semibold text-zinc-900">Şablonlar</h1>
        <span className="ml-auto text-xs text-zinc-400">{user.office ?? ""}</span>
      </header>

      <div className="border-b border-zinc-100">
        <div className="mx-auto flex max-w-3xl flex-wrap items-center gap-3 px-4 py-3">
          <div className="flex gap-1 rounded-lg bg-zinc-100 p-1 text-sm">
            {(["dilekce", "sozlesme"] as TemplateKind[]).map((k) => (
              <button
                key={k}
                onClick={() => chooseKind(k)}
                className={`rounded-md px-4 py-1.5 ${
                  kind === k ? "bg-white font-medium text-zinc-900 shadow-sm" : "text-zinc-500"
                }`}
              >
                {KIND_LABEL[k]}
              </button>
            ))}
          </div>
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Tip ara…"
            className="min-w-40 flex-1 rounded-lg border border-zinc-200 px-3 py-1.5 text-sm outline-none focus:border-zinc-400"
          />
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto max-w-3xl px-4 py-4">
          {filtered.map((c) => (
            <div key={c.kategori} className="mb-6">
              <div className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-zinc-400">
                {c.kategori}
              </div>
              <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200">
                {c.tipler.map((t) => (
                  <button
                    key={t.id}
                    onClick={() => openType(t.id)}
                    className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-zinc-50"
                  >
                    <div className="min-w-0 flex-1">
                      <div className="truncate text-sm text-zinc-800">{t.ad}</div>
                      {t.ref && <div className="truncate text-xs text-zinc-400">{t.ref}</div>}
                    </div>
                    {t.hasUserOverride && (
                      <span className="shrink-0 rounded bg-emerald-500/15 px-1.5 py-0.5 text-[10px] text-emerald-600">
                        kişisel
                      </span>
                    )}
                    {t.hasOfficeOverride && (
                      <span className="shrink-0 rounded bg-sky-500/15 px-1.5 py-0.5 text-[10px] text-sky-600">
                        ofis
                      </span>
                    )}
                    <CaretRight size={14} aria-hidden className="shrink-0 text-zinc-300" />
                  </button>
                ))}
              </div>
            </div>
          ))}
          {filtered.length === 0 && (
            <div className="px-3 py-12 text-center text-sm text-zinc-400">Sonuç yok</div>
          )}
        </div>
      </div>
    </div>
  );
}
