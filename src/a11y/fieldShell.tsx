import type { ReactNode } from "react";

/** Shared label + hint + error chrome. Every typed field (Field, NumberField, DateField, SelectField, ...) wraps its native control in this. */
export function FieldShell({
  id,
  label,
  required,
  hint,
  hintId,
  error,
  errorId,
  children,
}: {
  id: string;
  label: string;
  required?: boolean;
  hint?: ReactNode;
  hintId: string;
  error?: ReactNode;
  errorId: string;
  children: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-[13px] text-zinc-500">
        {label}
        {required && (
          <span className="ml-0.5 text-red-600" aria-hidden>
            *
          </span>
        )}
      </label>
      {hint && (
        <span id={hintId} className="text-[12px] text-zinc-500">
          {hint}
        </span>
      )}
      {children}
      {error && (
        <p id={errorId} role="alert" className="m-0 text-[13px] text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}
