import { useId, type InputHTMLAttributes, type ReactNode, type TextareaHTMLAttributes } from "react";

// Accessible form field primitives (KDM-029). Every control is programmatically
// associated with its label; an error is exposed via aria-invalid + aria-describedby
// and announced with role="alert", so assistive tech and keyboard users get the same
// feedback as sighted users. This is the WCAG 2.2 baseline the domain screens build on.

type BaseProps = {
  label: string;
  error?: ReactNode;
  hint?: ReactNode;
  required?: boolean;
};

function useFieldIds(error: ReactNode, hint: ReactNode) {
  const id = useId();
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  const describedBy = [hint ? hintId : null, error ? errorId : null].filter(Boolean).join(" ") || undefined;
  return { id, errorId, hintId, describedBy };
}

export function Field({
  label,
  error,
  hint,
  required,
  ...props
}: BaseProps & Omit<InputHTMLAttributes<HTMLInputElement>, "aria-invalid">) {
  const { id, errorId, hintId, describedBy } = useFieldIds(error, hint);
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
        <span id={hintId} className="text-[12px] text-zinc-400">
          {hint}
        </span>
      )}
      <input
        {...props}
        id={id}
        required={required}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        aria-errormessage={error ? errorId : undefined}
        className="w-full rounded-[10px] border border-zinc-300 bg-white px-3.5 py-3 text-[15px] text-zinc-900 outline-none focus-visible:border-zinc-900 focus-visible:ring-2 focus-visible:ring-zinc-900/20 aria-[invalid=true]:border-red-500"
      />
      {error && (
        <p id={errorId} role="alert" className="m-0 text-[13px] text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}

export function TextArea({
  label,
  error,
  hint,
  required,
  ...props
}: BaseProps & Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, "aria-invalid">) {
  const { id, errorId, hintId, describedBy } = useFieldIds(error, hint);
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
        <span id={hintId} className="text-[12px] text-zinc-400">
          {hint}
        </span>
      )}
      <textarea
        {...props}
        id={id}
        required={required}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        aria-errormessage={error ? errorId : undefined}
        className="w-full rounded-[10px] border border-zinc-300 bg-white px-3.5 py-3 text-[15px] text-zinc-900 outline-none focus-visible:border-zinc-900 focus-visible:ring-2 focus-visible:ring-zinc-900/20 aria-[invalid=true]:border-red-500"
      />
      {error && (
        <p id={errorId} role="alert" className="m-0 text-[13px] text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}
