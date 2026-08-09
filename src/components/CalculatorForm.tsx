import { useState, type ReactNode } from "react";
import { api, ApiError } from "../api";
import { Calculator, Warning } from "../icons";
import { Spinner } from "../a11y";
import type { CalcDefinition, CalcField } from "../calcDefinitions";

// Tek hesap makinesinin formu + sonucu. `def.fields`'e göre generic render edilir — 33 hesap
// makinesinin hepsi bu TEK komponenti kullanır (bkz. calcDefinitions.ts başındaki açıklama).

const INPUT_CLS =
  "w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-[13.5px] text-zinc-800 placeholder:text-zinc-400 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-zinc-900";

function Field({
  label,
  required,
  help,
  invalid,
  children,
}: {
  label: string;
  required?: boolean;
  help?: string;
  invalid?: boolean;
  children: ReactNode;
}) {
  return (
    <label className="flex flex-col gap-1.5 text-[12.5px] font-medium text-zinc-600">
      <span>
        {label}
        {required && <span className="text-red-500"> *</span>}
      </span>
      {children}
      {help && <span className="text-[11.5px] font-normal text-zinc-400">{help}</span>}
      {invalid && <span className="text-[11.5px] font-normal text-red-500">Bu alan zorunlu.</span>}
    </label>
  );
}

type FormState = Record<string, string>; // text/decimal/int/date/list/enum/boolOptional ortak metin değeri
type BoolState = Record<string, boolean>; // yalnız type:"bool" (checkbox)

function buildBody(def: CalcDefinition, values: FormState, bools: BoolState): Record<string, unknown> {
  const body: Record<string, unknown> = {};
  for (const f of def.fields) {
    if (f.type === "bool") {
      body[f.key] = bools[f.key] ?? false;
      continue;
    }
    const raw = (values[f.key] ?? "").trim();
    if (f.type === "boolOptional") {
      if (raw === "true") body[f.key] = true;
      else if (raw === "false") body[f.key] = false;
      continue; // "" → alan tamamen atlanır
    }
    if (!raw) continue; // required alanlar submit öncesi zaten kontrol edildi; opsiyonel boşsa atlanır
    switch (f.type) {
      case "decimal": {
        const n = parseFloat(raw);
        if (!Number.isNaN(n)) body[f.key] = n;
        break;
      }
      case "int": {
        const n = parseInt(raw, 10);
        if (!Number.isNaN(n)) body[f.key] = n;
        break;
      }
      case "enum":
        body[f.key] = f.numeric ? Number(raw) : raw;
        break;
      case "list": {
        const items = raw.split(",").map((s) => s.trim()).filter(Boolean);
        if (items.length > 0) body[f.key] = f.listItem === "int" ? items.map((s) => parseInt(s, 10)) : items;
        break;
      }
      default: // text, date
        body[f.key] = raw;
    }
  }
  return body;
}

function missingRequired(def: CalcDefinition, values: FormState): Set<string> {
  const missing = new Set<string>();
  for (const f of def.fields) {
    if (!f.required || f.type === "bool") continue; // bool zorunluları her zaman dolu (checkbox)
    if (!(values[f.key] ?? "").trim()) missing.add(f.key);
  }
  return missing;
}

function FieldInput({
  field,
  value,
  onChange,
  boolValue,
  onBoolChange,
}: {
  field: CalcField;
  value: string;
  onChange: (v: string) => void;
  boolValue: boolean;
  onBoolChange: (v: boolean) => void;
}) {
  switch (field.type) {
    case "bool":
      return (
        <label className="flex items-center gap-1.5 text-[13px] text-zinc-700">
          <input type="checkbox" checked={boolValue} onChange={(e) => onBoolChange(e.target.checked)} />
          Evet
        </label>
      );
    case "boolOptional":
      return (
        <select value={value} onChange={(e) => onChange(e.target.value)} className={INPUT_CLS}>
          <option value="">(Belirtilmedi)</option>
          <option value="true">Evet</option>
          <option value="false">Hayır</option>
        </select>
      );
    case "enum":
      return (
        <select value={value} onChange={(e) => onChange(e.target.value)} className={INPUT_CLS}>
          <option value="">Seçiniz</option>
          {field.options?.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      );
    case "date":
      return <input type="date" value={value} onChange={(e) => onChange(e.target.value)} className={INPUT_CLS} />;
    case "decimal":
    case "int":
      return (
        <input
          value={value}
          onChange={(e) => onChange(e.target.value)}
          inputMode={field.type === "int" ? "numeric" : "decimal"}
          placeholder={field.placeholder}
          className={INPUT_CLS}
        />
      );
    default:
      return (
        <input value={value} onChange={(e) => onChange(e.target.value)} placeholder={field.placeholder} className={INPUT_CLS} />
      );
  }
}

// ---- Sonuç render'ı: 33 farklı response şeklini introspect ederek çizer (per-calculator özel kod yok) ----

const META_KEYS = ["kaynak", "tarife_donemi", "uyarilar", "varsayimlar", "notlar", "not"];
const ISO_DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

function labelize(key: string): string {
  return key
    .split("_")
    .map((w) => (w.length > 0 ? w[0].toLocaleUpperCase("tr-TR") + w.slice(1) : w))
    .join(" ");
}

function formatNumber(key: string, n: number): string {
  // "binde_oran" (StampDutyResponse) tek istisna: yüzde değil, binde (per-mille) — % işareti yanıltıcı olur.
  if (key === "binde_oran") return n.toLocaleString("tr-TR", { maximumFractionDigits: 2 });
  if (key.includes("oran") || key.includes("yuzde")) {
    return `%${n.toLocaleString("tr-TR", { maximumFractionDigits: 2 })}`;
  }
  if (/(_gun|_yil|_ay|_hafta|_saat|_sayisi)$/.test(key)) {
    return n.toLocaleString("tr-TR", { maximumFractionDigits: 2 });
  }
  return `${n.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} TL`;
}

function formatScalar(key: string, value: unknown): string {
  if (value === null || value === undefined) return "–";
  if (typeof value === "boolean") return value ? "Evet" : "Hayır";
  if (typeof value === "number") return formatNumber(key, value);
  if (typeof value === "string") {
    if (ISO_DATE_RE.test(value)) {
      const [y, m, d] = value.split("-");
      return `${d}.${m}.${y}`;
    }
    return value;
  }
  return String(value);
}

function ItemsTable({ items }: { items: Record<string, unknown>[] }) {
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
                  {formatScalar(c, row[c])}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ResultGrid({ entries }: { entries: [string, unknown][] }) {
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
                <ItemsTable items={value as Record<string, unknown>[]} />
              ) : (
                <div className="text-[13.5px] text-zinc-800">
                  {(value as unknown[]).map((v) => formatScalar(key, v)).join(", ")}
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
                <ResultGrid entries={Object.entries(value as Record<string, unknown>)} />
              </div>
            </div>
          );
        }
        return (
          <div key={key}>
            <div className="text-[11px] font-semibold uppercase tracking-wide text-zinc-400">{labelize(key)}</div>
            <div className="text-[13.5px] text-zinc-800">{formatScalar(key, value)}</div>
          </div>
        );
      })}
    </div>
  );
}

function ResultView({ result }: { result: Record<string, unknown> }) {
  const mainEntries = Object.entries(result).filter(([k]) => !META_KEYS.includes(k));
  const notes = [
    ...(Array.isArray(result.uyarilar) ? (result.uyarilar as string[]) : []),
    ...(Array.isArray(result.varsayimlar) ? (result.varsayimlar as string[]) : []),
    ...(Array.isArray(result.notlar) ? (result.notlar as string[]) : []),
    ...(typeof result.not === "string" ? [result.not] : []),
  ];

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
      <div className="mb-4 flex items-center gap-2 text-[13px] font-semibold text-zinc-900">
        <Calculator size={16} aria-hidden />
        Sonuç
      </div>
      <ResultGrid entries={mainEntries} />
      {notes.length > 0 && (
        <ul className="mt-4 flex flex-col gap-1.5 border-t border-zinc-100 pt-4 text-[12.5px] text-zinc-500">
          {notes.map((n, i) => (
            <li key={i} className="flex gap-1.5">
              <span className="text-zinc-300">•</span>
              {n}
            </li>
          ))}
        </ul>
      )}
      {(typeof result.kaynak === "string" || typeof result.tarife_donemi === "string") && (
        <div className="mt-4 border-t border-zinc-100 pt-3 text-[11.5px] italic text-zinc-400">
          {typeof result.tarife_donemi === "string" && <div>Tarife dönemi: {result.tarife_donemi}</div>}
          {typeof result.kaynak === "string" && <div>Kaynak: {result.kaynak}</div>}
        </div>
      )}
    </div>
  );
}

type SubmitState = { state: "idle" } | { state: "loading" } | { state: "error"; detail: string } | { state: "ready"; result: Record<string, unknown> };

export function CalculatorForm({ def }: { def: CalcDefinition }) {
  const [values, setValues] = useState<FormState>({});
  const [bools, setBools] = useState<BoolState>({});
  const [invalid, setInvalid] = useState<Set<string>>(new Set());
  const [submit, setSubmit] = useState<SubmitState>({ state: "idle" });

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    const missing = missingRequired(def, values);
    if (missing.size > 0) {
      setInvalid(missing);
      return;
    }
    setInvalid(new Set());
    setSubmit({ state: "loading" });
    try {
      const body = buildBody(def, values, bools);
      const result = await api.calc(def.endpoint, body, def.method ?? "POST");
      setSubmit({ state: "ready", result });
    } catch (err) {
      setSubmit({
        state: "error",
        detail: err instanceof ApiError ? err.message : "Hesaplama yapılamadı, lütfen tekrar deneyin.",
      });
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-4 py-6">
      <form onSubmit={onSubmit} className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
        <div className="grid grid-cols-1 gap-x-6 gap-y-4 sm:grid-cols-2">
          {def.fields.map((f) => (
            <Field key={f.key} label={f.label} required={f.required} help={f.help} invalid={invalid.has(f.key)}>
              <FieldInput
                field={f}
                value={values[f.key] ?? ""}
                onChange={(v) => setValues((s) => ({ ...s, [f.key]: v }))}
                boolValue={bools[f.key] ?? false}
                onBoolChange={(v) => setBools((s) => ({ ...s, [f.key]: v }))}
              />
            </Field>
          ))}
        </div>
        <div className="mt-5 flex justify-end border-t border-zinc-100 pt-4">
          <button
            type="submit"
            disabled={submit.state === "loading"}
            className="flex items-center gap-1.5 rounded-lg bg-zinc-900 px-4 py-2 text-[13.5px] font-medium text-white hover:bg-zinc-800 disabled:opacity-40"
          >
            <Calculator size={14} aria-hidden />
            {submit.state === "loading" ? "Hesaplanıyor…" : "Hesapla"}
          </button>
        </div>
      </form>

      {submit.state === "loading" && (
        <div className="flex justify-center py-6">
          <Spinner label="Hesaplanıyor…" />
        </div>
      )}
      {submit.state === "error" && (
        <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5 text-[13px] text-amber-800">
          <Warning size={16} aria-hidden className="mt-0.5 shrink-0" />
          {submit.detail}
        </div>
      )}
      {submit.state === "ready" && <ResultView result={submit.result} />}
    </div>
  );
}
