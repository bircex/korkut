import { useEffect, useState } from "react";
import { api, ApiError, type ModelData, type ModelHealth } from "../api";
import { Check, X } from "../icons";

// Office model picker (Ayarlar → Model, owner-only). Fully on-prem: every endpoint is local, so
// there is no cloud/redaction choice — the server just talks to the chosen OpenAI-compatible
// model server. Presets fill the common Ollama models; "Özel" lets the owner type any model +
// server address. The API key is write-only — the server never returns it.

const DEFAULT_BASE_URL = "http://localhost:11434/v1";

type Preset = { model: string; label: string; sub: string; icon: string };

// Common tool-calling-capable models served by a local Ollama. `model` is the Ollama tag; the
// owner can fine-tune it after picking (e.g. add a size suffix).
const PRESETS: Preset[] = [
  { model: "llama3.3", label: "Llama 3.3", sub: "Meta · 70B", icon: "🦙" },
  { model: "llama3.1", label: "Llama 3.1", sub: "Meta · 8B/70B", icon: "🦙" },
  { model: "qwen2.5", label: "Qwen 2.5", sub: "Alibaba", icon: "🌐" },
  { model: "mistral", label: "Mistral", sub: "Mistral AI", icon: "🌬️" },
  { model: "gemma2", label: "Gemma 2", sub: "Google", icon: "💎" },
  { model: "deepseek-r1", label: "DeepSeek-R1", sub: "DeepSeek", icon: "🔎" },
];

export function ModelSettings() {
  const [data, setData] = useState<ModelData | null>(null);
  const [enabled, setEnabled] = useState(false);
  const [model, setModel] = useState("");
  const [baseUrl, setBaseUrl] = useState(DEFAULT_BASE_URL);
  const [custom, setCustom] = useState(false); // true → free-text model field
  const [apiKey, setApiKey] = useState("");
  const [keyTouched, setKeyTouched] = useState(false);
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [testing, setTesting] = useState(false);
  const [health, setHealth] = useState<ModelHealth | null>(null);

  function hydrate(d: ModelData) {
    setData(d);
    setEnabled(d.enabled);
    setModel(d.model);
    setBaseUrl(d.base_url || DEFAULT_BASE_URL);
    setCustom(d.enabled ? !PRESETS.some((p) => p.model === d.model) : false);
    setApiKey("");
    setKeyTouched(false);
    setSaved(true);
  }

  useEffect(() => {
    api
      .getModel()
      .then(hydrate)
      .catch(() => setError("Model ayarları yüklenemedi."));
  }, []);

  const canEdit = data?.can_edit ?? false;
  const dirty = () => setSaved(false);

  function pick(p: Preset) {
    setModel(p.model);
    setCustom(false);
    setEnabled(true);
    dirty();
  }

  function pickCustom() {
    setCustom(true);
    setEnabled(true);
    if (PRESETS.some((p) => p.model === model)) setModel("");
    dirty();
  }

  async function save() {
    setBusy(true);
    setError(null);
    try {
      const d = await api.saveModel({
        enabled,
        is_local: true, // fully on-prem
        model,
        base_url: baseUrl,
        api_key: keyTouched ? apiKey : null,
      });
      hydrate(d);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Kaydedilemedi.");
    } finally {
      setBusy(false);
    }
  }

  async function remove() {
    if (!confirm("Özel model kaldırılıp yerleşik (varsayılan) modele dönülsün mü?")) return;
    setBusy(true);
    setError(null);
    try {
      await api.deleteModel();
      hydrate(await api.getModel());
    } finally {
      setBusy(false);
    }
  }

  // Tests the *saved* config (the server probes the stored endpoint), so a dirty form is checked
  // against what's persisted — the hint below tells the owner to save first.
  async function test() {
    setTesting(true);
    setHealth(null);
    try {
      setHealth(await api.modelHealth());
    } catch {
      setHealth({ connected: false, configured: false, detail: "Test başarısız oldu." });
    } finally {
      setTesting(false);
    }
  }

  const input =
    "w-full rounded-lg border border-zinc-200 px-3 py-2 text-sm outline-none focus:border-zinc-400 disabled:bg-zinc-50 disabled:text-zinc-400";

  return (
    <div>
      {!canEdit && (
        <div className="mb-4 rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-700">
          Modeli yalnızca ofis sahibi değiştirebilir.
        </div>
      )}

      {/* Master switch */}
      <label className="flex items-center gap-3 rounded-xl border border-zinc-200 px-4 py-3">
        <input
          type="checkbox"
          checked={enabled}
          disabled={!canEdit}
          onChange={(e) => {
            setEnabled(e.target.checked);
            dirty();
          }}
          className="h-4 w-4 accent-zinc-900"
        />
        <div>
          <div className="text-sm font-medium text-zinc-900">Özel model kullan</div>
          <div className="text-xs text-zinc-400">
            Kapalıyken Korkut yerleşik (varsayılan) modeli kullanır. Tüm bağlantı ofis
            sunucusunda kalır.
          </div>
        </div>
      </label>

      <fieldset
        disabled={!canEdit || !enabled}
        className="mt-4 transition-opacity disabled:opacity-50"
      >
        <p className="mb-2 text-xs font-medium text-zinc-500">Model seç</p>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
          {PRESETS.map((p) => {
            const active = !custom && model === p.model;
            return (
              <button
                key={p.model}
                type="button"
                onClick={() => pick(p)}
                className={`flex items-center gap-2.5 rounded-xl border px-3 py-2.5 text-left ${
                  active
                    ? "border-zinc-900 bg-zinc-900/[0.03] ring-1 ring-zinc-900"
                    : "border-zinc-200 hover:border-zinc-300"
                }`}
              >
                <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-zinc-100 text-lg">
                  {p.icon}
                </span>
                <span className="min-w-0">
                  <span className="block truncate text-sm font-medium text-zinc-900">
                    {p.label}
                  </span>
                  <span className="block truncate text-[11px] text-zinc-400">{p.sub}</span>
                </span>
              </button>
            );
          })}
          <button
            type="button"
            onClick={pickCustom}
            className={`flex items-center gap-2.5 rounded-xl border px-3 py-2.5 text-left ${
              custom
                ? "border-zinc-900 bg-zinc-900/[0.03] ring-1 ring-zinc-900"
                : "border-zinc-200 hover:border-zinc-300"
            }`}
          >
            <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-zinc-100 text-lg">
              ⚙️
            </span>
            <span className="min-w-0">
              <span className="block truncate text-sm font-medium text-zinc-900">Özel</span>
              <span className="block truncate text-[11px] text-zinc-400">Elle gir</span>
            </span>
          </button>
        </div>

        {custom && (
          <div className="mt-3">
            <label className="mb-1 block text-xs font-medium text-zinc-500">Model adı</label>
            <input
              className={input}
              value={model}
              placeholder="örn. llama3.3:70b"
              onChange={(e) => {
                setModel(e.target.value);
                dirty();
              }}
            />
          </div>
        )}

        <div className="mt-3">
          <label className="mb-1 block text-xs font-medium text-zinc-500">
            Sunucu adresi (OpenAI uyumlu /v1)
          </label>
          <input
            className={input}
            value={baseUrl}
            placeholder={DEFAULT_BASE_URL}
            onChange={(e) => {
              setBaseUrl(e.target.value);
              dirty();
            }}
          />
          <p className="mt-1 text-[11px] text-zinc-400">
            Ollama varsayılanı: {DEFAULT_BASE_URL}
          </p>
        </div>

        <details className="mt-3">
          <summary className="cursor-pointer text-xs text-zinc-500 hover:text-zinc-700">
            Gelişmiş — API anahtarı (yerel sunucularda genelde gereksiz)
          </summary>
          <input
            type="password"
            className={`${input} mt-2`}
            value={apiKey}
            placeholder={data?.hasApiKey ? "•••••••• (kayıtlı — değiştirmek için yazın)" : "—"}
            onChange={(e) => {
              setApiKey(e.target.value);
              setKeyTouched(true);
              dirty();
            }}
          />
        </details>
      </fieldset>

      {error && <div className="mt-4 text-xs text-red-600">{error}</div>}

      <div className="mt-4 flex items-center gap-3">
        <button
          onClick={test}
          disabled={testing}
          className="rounded-lg border border-zinc-200 px-3 py-1.5 text-sm text-zinc-700 hover:bg-zinc-50 disabled:opacity-50"
        >
          {testing ? "Test ediliyor…" : "Bağlantıyı test et"}
        </button>
        {health && (
          <span
            className={`inline-flex items-center gap-1 text-xs ${health.connected ? "text-emerald-600" : "text-red-600"}`}
          >
            {health.connected ? (
              <Check size={12} weight="bold" aria-hidden />
            ) : (
              <X size={12} weight="bold" aria-hidden />
            )}
            {health.detail}
          </span>
        )}
      </div>
      {!saved && (
        <p className="mt-1 text-[11px] text-zinc-400">
          Test, en son kaydedilmiş ayarı dener — değişiklik yaptıysanız önce kaydedin.
        </p>
      )}

      {canEdit && (
        <div className="mt-5 flex items-center gap-3">
          {saved && (
            <span className="inline-flex items-center gap-1 text-xs text-emerald-600">
              Kaydedildi <Check size={12} weight="bold" aria-hidden />
            </span>
          )}
          <div className="ml-auto flex gap-2">
            {data?.enabled && (
              <button
                onClick={remove}
                disabled={busy}
                className="rounded-lg border border-zinc-200 px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-50 disabled:opacity-50"
              >
                Kaldır
              </button>
            )}
            <button
              onClick={save}
              disabled={busy}
              className="rounded-lg bg-zinc-900 px-4 py-1.5 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-40"
            >
              {busy ? "…" : "Kaydet"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
