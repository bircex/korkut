import { type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, useId } from "react";
import { X } from "../icons";
import { FieldShell } from "./fieldShell";
import { FIELD_INPUT_CLS, type BaseFieldProps, useFieldIds } from "./fieldIds";

// Typed form-field primitives (KDM roadmap: tool/calculator forms). Every field carries its own
// native input type (number/date/select) so browsers give the right keyboard/picker and the
// backend's declared min/max shows up as both a hint and a hard constraint, instead of every
// field being a plain text box that only "required" is checked against.

type BaseProps = BaseFieldProps;

function rangeHint(min: number | undefined, max: number | undefined): string | undefined {
  if (min === undefined && max === undefined) return undefined;
  if (min !== undefined && max !== undefined) return `${min} - ${max} arası`;
  if (min !== undefined) return `en az ${min}`;
  return `en çok ${max}`;
}

export function NumberField({
  label,
  error,
  hint,
  required,
  min,
  max,
  ...props
}: BaseProps & Omit<InputHTMLAttributes<HTMLInputElement>, "type" | "aria-invalid" | "min" | "max"> & { min?: number; max?: number }) {
  const { id, errorId, hintId, describedBy } = useFieldIds(error, hint);
  return (
    <FieldShell id={id} label={label} required={required} hint={hint ?? rangeHint(min, max)} hintId={hintId} error={error} errorId={errorId}>
      <input
        {...props}
        id={id}
        type="number"
        min={min}
        max={max}
        required={required}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        aria-errormessage={error ? errorId : undefined}
        className={FIELD_INPUT_CLS}
      />
    </FieldShell>
  );
}

export function DateField({
  label,
  error,
  hint,
  required,
  ...props
}: BaseProps & Omit<InputHTMLAttributes<HTMLInputElement>, "type" | "aria-invalid">) {
  const { id, errorId, hintId, describedBy } = useFieldIds(error, hint);
  return (
    <FieldShell id={id} label={label} required={required} hint={hint} hintId={hintId} error={error} errorId={errorId}>
      <input
        {...props}
        id={id}
        type="date"
        required={required}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        aria-errormessage={error ? errorId : undefined}
        className={FIELD_INPUT_CLS}
      />
    </FieldShell>
  );
}

export function DateRangeField({
  label,
  required,
  startLabel = "Başlangıç",
  endLabel = "Bitiş",
  start,
  end,
  onStartChange,
  onEndChange,
  hint,
}: {
  label: string;
  required?: boolean;
  startLabel?: string;
  endLabel?: string;
  start: string;
  end: string;
  onStartChange: (v: string) => void;
  onEndChange: (v: string) => void;
  hint?: ReactNode;
}) {
  const rangeInvalid = Boolean(start && end && start > end);
  return (
    <div className="flex flex-col gap-1.5 sm:col-span-2">
      <span className="text-[13px] text-zinc-500">
        {label}
        {required && (
          <span className="ml-0.5 text-red-600" aria-hidden>
            *
          </span>
        )}
      </span>
      <div className="grid grid-cols-2 gap-3">
        <DateField label={startLabel} value={start} onChange={(e) => onStartChange(e.target.value)} required={required} />
        <DateField
          label={endLabel}
          value={end}
          onChange={(e) => onEndChange(e.target.value)}
          required={required}
          error={rangeInvalid ? "Bitiş, başlangıçtan önce olamaz" : undefined}
        />
      </div>
      {hint && !rangeInvalid && <span className="text-[12px] text-zinc-500">{hint}</span>}
    </div>
  );
}

export function SelectField({
  label,
  error,
  hint,
  required,
  options,
  placeholder = "Seçiniz",
  ...props
}: BaseProps &
  Omit<SelectHTMLAttributes<HTMLSelectElement>, "aria-invalid"> & {
    options: { value: string; label: string }[];
    placeholder?: string;
  }) {
  const { id, errorId, hintId, describedBy } = useFieldIds(error, hint);
  return (
    <FieldShell id={id} label={label} required={required} hint={hint} hintId={hintId} error={error} errorId={errorId}>
      <select
        {...props}
        id={id}
        required={required}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        aria-errormessage={error ? errorId : undefined}
        className={FIELD_INPUT_CLS}
      >
        <option value="">{placeholder}</option>
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </FieldShell>
  );
}

export function CheckboxField({ label, checked, onChange, hint }: { label: string; checked: boolean; onChange: (v: boolean) => void; hint?: ReactNode }) {
  const id = useId();
  const hintId = `${id}-hint`;
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="flex items-center gap-2 text-[15px] text-zinc-900">
        <input
          id={id}
          type="checkbox"
          checked={checked}
          onChange={(e) => onChange(e.target.checked)}
          aria-describedby={hint ? hintId : undefined}
          className="h-4 w-4 rounded border-zinc-300"
        />
        {label}
      </label>
      {hint && (
        <span id={hintId} className="text-[12px] text-zinc-500">
          {hint}
        </span>
      )}
    </div>
  );
}

export function RepeatableListField({
  label,
  required,
  hint,
  error,
  items,
  itemType = "text",
  itemPlaceholder,
  onChange,
  addLabel = "Ekle",
  emptyHint = "Henüz eklenmedi.",
}: {
  label: string;
  required?: boolean;
  hint?: ReactNode;
  error?: ReactNode;
  items: string[];
  itemType?: "int" | "text";
  itemPlaceholder?: string;
  onChange: (items: string[]) => void;
  addLabel?: string;
  emptyHint?: string;
}) {
  function update(i: number, v: string) {
    const next = items.slice();
    next[i] = v;
    onChange(next);
  }
  function remove(i: number) {
    onChange(items.filter((_, idx) => idx !== i));
  }
  return (
    <div className="flex flex-col gap-1.5 sm:col-span-2">
      <span className="text-[13px] text-zinc-500">
        {label}
        {required && (
          <span className="ml-0.5 text-red-600" aria-hidden>
            *
          </span>
        )}
      </span>
      {hint && <span className="text-[12px] text-zinc-500">{hint}</span>}
      <div className="flex flex-col gap-2">
        {items.map((v, i) => (
          <div key={i} className="flex items-center gap-2">
            <input
              value={v}
              onChange={(e) => update(i, e.target.value)}
              type={itemType === "int" ? "number" : "text"}
              placeholder={itemPlaceholder}
              aria-label={`${label} - satır ${i + 1}`}
              className={FIELD_INPUT_CLS}
            />
            <button
              type="button"
              onClick={() => remove(i)}
              aria-label="Satırı sil"
              className="flex shrink-0 items-center justify-center rounded-lg border border-zinc-200 p-2.5 text-zinc-400 hover:border-red-200 hover:text-red-600"
            >
              <X size={14} aria-hidden />
            </button>
          </div>
        ))}
        {items.length === 0 && <span className="text-[12.5px] text-zinc-400">{emptyHint}</span>}
        <button
          type="button"
          onClick={() => onChange([...items, ""])}
          className="self-start rounded-lg border border-dashed border-zinc-300 px-3 py-1.5 text-[12.5px] text-zinc-500 hover:border-zinc-400 hover:text-zinc-700"
        >
          + {addLabel}
        </button>
      </div>
      {error && (
        <p role="alert" className="m-0 text-[13px] text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}
