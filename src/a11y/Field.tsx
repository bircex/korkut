import type { InputHTMLAttributes, TextareaHTMLAttributes } from "react";
import { FieldShell } from "./fieldShell";
import { FIELD_INPUT_CLS, type BaseFieldProps, useFieldIds } from "./fieldIds";

// Accessible form field primitives (KDM-029). Every control is programmatically
// associated with its label; an error is exposed via aria-invalid + aria-describedby
// and announced with role="alert", so assistive tech and keyboard users get the same
// feedback as sighted users. This is the WCAG 2.2 baseline the domain screens build on.

export function Field({
  label,
  error,
  hint,
  required,
  ...props
}: BaseFieldProps & Omit<InputHTMLAttributes<HTMLInputElement>, "aria-invalid">) {
  const { id, errorId, hintId, describedBy } = useFieldIds(error, hint);
  return (
    <FieldShell id={id} label={label} required={required} hint={hint} hintId={hintId} error={error} errorId={errorId}>
      <input
        {...props}
        id={id}
        required={required}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        aria-errormessage={error ? errorId : undefined}
        className={FIELD_INPUT_CLS}
      />
    </FieldShell>
  );
}

export function TextArea({
  label,
  error,
  hint,
  required,
  ...props
}: BaseFieldProps & Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, "aria-invalid">) {
  const { id, errorId, hintId, describedBy } = useFieldIds(error, hint);
  return (
    <FieldShell id={id} label={label} required={required} hint={hint} hintId={hintId} error={error} errorId={errorId}>
      <textarea
        {...props}
        id={id}
        required={required}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        aria-errormessage={error ? errorId : undefined}
        className={FIELD_INPUT_CLS}
      />
    </FieldShell>
  );
}
