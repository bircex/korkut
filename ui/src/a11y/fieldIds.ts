import { useId, type ReactNode } from "react";

// Shared id-wiring behind every accessible field primitive (Field/TextArea in Field.tsx,
// NumberField/DateField/etc. in FormFields.tsx, FieldShell). Plain functions/types only — no
// JSX — so components that import this stay fast-refresh safe.

export type BaseFieldProps = {
  label: string;
  error?: ReactNode;
  hint?: ReactNode;
  required?: boolean;
};

export function useFieldIds(error: ReactNode, hint: ReactNode) {
  const id = useId();
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  const describedBy = [hint ? hintId : null, error ? errorId : null].filter(Boolean).join(" ") || undefined;
  return { id, errorId, hintId, describedBy };
}

export const FIELD_INPUT_CLS =
  "w-full rounded-[10px] border border-zinc-300 bg-white px-3.5 py-3 text-[15px] text-zinc-900 outline-none focus-visible:border-zinc-900 focus-visible:ring-2 focus-visible:ring-zinc-900/20 aria-[invalid=true]:border-red-500";
