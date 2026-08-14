import { useEffect, useState } from "react";
import { api, ApiError, type CalcSchemaResponse } from "../api";
import { Calculator, Warning } from "../icons";
import { CheckboxField, DateField, Field, NumberField, RepeatableListField, SelectField, Spinner } from "../a11y";
import type { CalcDefinition, CalcField } from "../calcDefinitions";
import { JsonValueGrid } from "./JsonValueView";

// Tek hesap makinesinin formu + sonucu. `def.fields`'e göre generic render edilir — 33 hesap
// makinesinin hepsi bu TEK komponenti kullanır (bkz. calcDefinitions.ts başındaki açıklama).
// Alan tipine göre render, ortak a11y/FormFields.tsx primitive'lerine devredilir (NumberField
// min/max ile, DateField native date picker'la, RepeatableListField gerçek satır ekle/sil ile).

type FormState = Record<string, string>; // text/decimal/int/date/enum/boolOptional ortak metin değeri
type BoolState = Record<string, boolean>; // yalnız type:"bool" (checkbox)
type ListState = Record<string, string[]>; // yalnız type:"list"

function buildBody(def: CalcDefinition, values: FormState, bools: BoolState, lists: ListState): Record<string, unknown> {
  const body: Record<string, unknown> = {};
  for (const f of def.fields) {
    if (f.type === "bool") {
      body[f.key] = bools[f.key] ?? false;
      continue;
    }
    if (f.type === "list") {
      const items = (lists[f.key] ?? []).map((s) => s.trim()).filter(Boolean);
      if (items.length > 0) body[f.key] = f.listItem === "int" ? items.map((s) => parseInt(s, 10)) : items;
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
      default: // text, date
        body[f.key] = raw;
    }
  }
  return body;
}

function missingRequired(def: CalcDefinition, values: FormState, lists: ListState): Set<string> {
  const missing = new Set<string>();
  for (const f of def.fields) {
    if (!f.required || f.type === "bool") continue; // bool zorunluları her zaman dolu (checkbox)
    if (f.type === "list") {
      if ((lists[f.key] ?? []).filter((s) => s.trim()).length === 0) missing.add(f.key);
      continue;
    }
    if (!(values[f.key] ?? "").trim()) missing.add(f.key);
  }
  return missing;
}

function FieldRenderer({
  field,
  value,
  onChange,
  boolValue,
  onBoolChange,
  listValue,
  onListChange,
  invalid,
}: {
  field: CalcField;
  value: string;
  onChange: (v: string) => void;
  boolValue: boolean;
  onBoolChange: (v: boolean) => void;
  listValue: string[];
  onListChange: (v: string[]) => void;
  invalid: boolean;
}) {
  const requiredError = invalid ? "Bu alan zorunlu." : undefined;
  switch (field.type) {
    case "bool":
      return <CheckboxField label={field.label} checked={boolValue} onChange={onBoolChange} hint={field.help} />;
    case "boolOptional":
      return (
        <SelectField
          label={field.label}
          hint={field.help}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          options={[
            { value: "true", label: "Evet" },
            { value: "false", label: "Hayır" },
          ]}
          placeholder="(Belirtilmedi)"
        />
      );
    case "enum":
      return (
        <SelectField
          label={field.label}
          required={field.required}
          hint={field.help}
          error={requiredError}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          options={field.options ?? []}
        />
      );
    case "date":
      return (
        <DateField
          label={field.label}
          required={field.required}
          hint={field.help}
          error={requiredError}
          value={value}
          onChange={(e) => onChange(e.target.value)}
        />
      );
    case "decimal":
    case "int":
      return (
        <NumberField
          label={field.label}
          required={field.required}
          hint={field.help}
          error={requiredError}
          min={field.min}
          max={field.max}
          step={field.type === "int" ? 1 : "any"}
          placeholder={field.placeholder}
          value={value}
          onChange={(e) => onChange(e.target.value)}
        />
      );
    case "list":
      return (
        <RepeatableListField
          label={field.label}
          required={field.required}
          hint={field.help}
          error={requiredError}
          items={listValue}
          itemType={field.listItem === "int" ? "int" : "text"}
          itemPlaceholder={field.placeholder}
          onChange={onListChange}
        />
      );
    default:
      return (
        <Field
          label={field.label}
          required={field.required}
          hint={field.help}
          error={requiredError}
          placeholder={field.placeholder}
          value={value}
          onChange={(e) => onChange(e.target.value)}
        />
      );
  }
}

// ---- Sonuç render'ı: 33 farklı response şeklini introspect ederek çizer (per-calculator özel kod yok) ----
// Grid/tablo mantığı JsonValueView.tsx'te ortak.

const META_KEYS = ["kaynak", "tarife_donemi", "uyarilar", "varsayimlar", "notlar", "not"];

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
      <JsonValueGrid entries={mainEntries} formatNumber={formatNumber} />
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
  const [lists, setLists] = useState<ListState>({});
  const [invalid, setInvalid] = useState<Set<string>>(new Set());
  const [submit, setSubmit] = useState<SubmitState>({ state: "idle" });
  const [schema, setSchema] = useState<CalcSchemaResponse | null>(null);

  // Faz 3: backend'in [Range] Contract attribute'larından türettiği canlı min/max, aşağıdaki
  // render'da calcDefinitions.ts'teki elle yazılmış değerlerin üstüne uygulanır — bkz. api.ts.
  useEffect(() => {
    let cancelled = false;
    api
      .calcSchema()
      .then((s) => {
        if (!cancelled) setSchema(s);
      })
      .catch(() => {}); // sessizce calcDefinitions.ts'teki sabit min/max'e düşülür
    return () => {
      cancelled = true;
    };
  }, []);
  const liveFields = schema?.[def.slug];

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    const missing = missingRequired(def, values, lists);
    if (missing.size > 0) {
      setInvalid(missing);
      return;
    }
    setInvalid(new Set());
    setSubmit({ state: "loading" });
    try {
      const body = buildBody(def, values, bools, lists);
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
          {def.fields.map((f) => {
            const live = liveFields?.[f.key];
            const field = live ? { ...f, min: live.minimum ?? f.min, max: live.maximum ?? f.max } : f;
            return (
              <FieldRenderer
                key={f.key}
                field={field}
                value={values[f.key] ?? ""}
                onChange={(v) => setValues((s) => ({ ...s, [f.key]: v }))}
                boolValue={bools[f.key] ?? false}
                onBoolChange={(v) => setBools((s) => ({ ...s, [f.key]: v }))}
                listValue={lists[f.key] ?? []}
                onListChange={(v) => setLists((s) => ({ ...s, [f.key]: v }))}
                invalid={invalid.has(f.key)}
              />
            );
          })}
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
