import { useEffect, useMemo, useState } from "react";
import {
  api,
  type TemplateCategory,
  type TemplateDetail,
  type TemplateKind,
  type TemplateScope,
  type User,
} from "../api";

// Turkish-aware fold for the search box (diacritics → ascii), mirroring the backend.
const FOLD: Record<string, string> = {
  ç: "c", ğ: "g", ı: "i", İ: "i", ö: "o", ş: "s", ü: "u", â: "a", î: "i", û: "u",
};
const fold = (s: string) => s.toLowerCase().replace(/[çğıİöşüâîû]/g, (c) => FOLD[c] ?? c);

function contentFor(detail: TemplateDetail, scope: TemplateScope): string {
  const override = scope === "user" ? detail.userOverride : detail.officeOverride;
  return override ?? detail.effective ?? detail.default ?? "";
}

export function Customize({ user, onClose }: { user: User; onClose: () => void }) {
  const [kind, setKind] = useState<TemplateKind>("dilekce");
  const [cats, setCats] = useState<TemplateCategory[]>([]);
  const [query, setQuery] = useState("");
  const [selected, setSelected] = useState<string | null>(null);
  const [detail, setDetail] = useState<TemplateDetail | null>(null);
  const [scope, setScope] = useState<TemplateScope>("user");
  const [content, setContent] = useState("");
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
    setSelected(null);
    setDetail(null);
    setKind(next);
  }

  async function openType(id: string) {
    setSelected(id);
    setSaved(false);
    const d = await api.getTemplate(id);
    setDetail(d);
    const startScope: TemplateScope = d.userOverride !== null ? "user" : "office";
    setScope(startScope);
    setContent(contentFor(d, startScope));
  }

  function switchScope(next: TemplateScope) {
    if (!detail) return;
    setScope(next);
    setContent(contentFor(detail, next));
    setSaved(false);
  }

  async function save() {
    if (!detail) return;
    setBusy(true);
    try {
      await api.saveTemplate(detail.id, scope, content);
      const [d] = await Promise.all([api.getTemplate(detail.id), refreshList()]);
      setDetail(d);
      setContent(contentFor(d, scope));
      setSaved(true);
    } finally {
      setBusy(false);
    }
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
      .map((c) => ({
        ...c,
        tipler: c.tipler.filter((t) => fold(`${t.ad} ${t.id}`).includes(q)),
      }))
      .filter((c) => c.tipler.length > 0);
  }, [cats, query]);

  const hasOverrideForScope =
    detail !== null && (scope === "user" ? detail.userOverride : detail.officeOverride) !== null;
  const dirty = detail !== null && content !== contentFor(detail, scope);

  return (
    <div className="flex h-dvh flex-col bg-white">
      <header className="flex items-center gap-3 border-b border-zinc-200 px-4 py-3">
        <button onClick={onClose} className="text-sm text-zinc-500 hover:text-zinc-900">
          ← Geri
        </button>
        <h1 className="text-base font-semibold text-zinc-900">Özelleştir — Şablon temaları</h1>
        <span className="ml-auto text-xs text-zinc-400">{user.office ?? ""}</span>
      </header>

      <div className="flex min-h-0 flex-1">
        {/* Left: kind toggle + search + type list */}
        <aside className="flex w-80 shrink-0 flex-col border-r border-zinc-200">
          <div className="space-y-2 border-b border-zinc-100 p-3">
            <div className="flex gap-1 rounded-lg bg-zinc-100 p-1 text-sm">
              {(["dilekce", "sozlesme"] as TemplateKind[]).map((k) => (
                <button
                  key={k}
                  onClick={() => chooseKind(k)}
                  className={`flex-1 rounded-md py-1.5 ${
                    kind === k ? "bg-white font-medium text-zinc-900 shadow-sm" : "text-zinc-500"
                  }`}
                >
                  {k === "dilekce" ? "Dilekçe" : "Sözleşme"}
                </button>
              ))}
            </div>
            <input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Tip ara…"
              className="w-full rounded-lg border border-zinc-200 px-3 py-1.5 text-sm outline-none focus:border-zinc-400"
            />
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto p-2">
            {filtered.map((c) => (
              <div key={c.kategori} className="mb-2">
                <div className="px-2 py-1 text-[11px] font-semibold uppercase tracking-wide text-zinc-400">
                  {c.kategori}
                </div>
                {c.tipler.map((t) => (
                  <button
                    key={t.id}
                    onClick={() => openType(t.id)}
                    className={`flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm ${
                      selected === t.id ? "bg-zinc-900 text-white" : "text-zinc-700 hover:bg-zinc-100"
                    }`}
                  >
                    <span className="min-w-0 flex-1 truncate">{t.ad}</span>
                    {t.hasUserOverride && (
                      <span className="shrink-0 rounded bg-emerald-500/15 px-1 text-[10px] text-emerald-600">
                        kişisel
                      </span>
                    )}
                    {t.hasOfficeOverride && (
                      <span className="shrink-0 rounded bg-sky-500/15 px-1 text-[10px] text-sky-600">
                        ofis
                      </span>
                    )}
                  </button>
                ))}
              </div>
            ))}
            {filtered.length === 0 && (
              <div className="px-3 py-6 text-center text-sm text-zinc-400">Sonuç yok</div>
            )}
          </div>
        </aside>

        {/* Right: editor */}
        <section className="flex min-w-0 flex-1 flex-col">
          {detail === null ? (
            <div className="grid flex-1 place-items-center text-sm text-zinc-400">
              Düzenlemek için soldan bir tip seç.
            </div>
          ) : (
            <>
              <div className="flex flex-wrap items-center gap-3 border-b border-zinc-100 px-4 py-3">
                <div className="min-w-0">
                  <div className="truncate text-sm font-medium text-zinc-900">{detail.ad}</div>
                  <div className="text-xs text-zinc-400">{detail.id}</div>
                </div>
                <div className="ml-auto flex gap-1 rounded-lg bg-zinc-100 p-1 text-sm">
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
              </div>
              <textarea
                value={content}
                onChange={(e) => {
                  setContent(e.target.value);
                  setSaved(false);
                }}
                spellCheck={false}
                className="min-h-0 flex-1 resize-none p-4 font-mono text-[13px] leading-relaxed text-zinc-800 outline-none"
                placeholder="Tema markdown…"
              />
              <div className="flex items-center gap-3 border-t border-zinc-100 px-4 py-3">
                <span className="text-xs text-zinc-400">
                  {hasOverrideForScope
                    ? scope === "user"
                      ? "Kişisel özelleştirme aktif."
                      : "Ofis özelleştirmesi aktif."
                    : "Varsayılan tema gösteriliyor."}
                </span>
                {saved && !dirty && <span className="text-xs text-emerald-600">Kaydedildi ✓</span>}
                <div className="ml-auto flex gap-2">
                  {hasOverrideForScope && (
                    <button
                      onClick={revert}
                      disabled={busy}
                      className="rounded-lg border border-zinc-200 px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-50 disabled:opacity-50"
                    >
                      Varsayılana dön
                    </button>
                  )}
                  <button
                    onClick={save}
                    disabled={busy || !dirty}
                    className="rounded-lg bg-zinc-900 px-4 py-1.5 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-40"
                  >
                    {busy ? "…" : "Kaydet"}
                  </button>
                </div>
              </div>
            </>
          )}
        </section>
      </div>
    </div>
  );
}
