import type { ReactNode } from "react";

// Loading / error feedback primitives (KDM-029). Both expose a live region so state
// changes are announced, not just shown.

export function Spinner({ label = "Yükleniyor" }: { label?: string }) {
  return (
    <span role="status" aria-live="polite" className="inline-flex items-center gap-2 text-[13px] text-zinc-500">
      <span
        aria-hidden
        className="h-4 w-4 animate-spin rounded-full border-2 border-zinc-300 border-t-zinc-800 motion-reduce:animate-none"
      />
      {label}
    </span>
  );
}

export function LoadingOverlay({ label = "Yükleniyor" }: { label?: string }) {
  return (
    <div className="flex min-h-40 items-center justify-center" aria-busy="true">
      <Spinner label={label} />
    </div>
  );
}

export function ErrorState({
  title = "Bir şeyler ters gitti",
  children,
  onRetry,
}: {
  title?: string;
  children?: ReactNode;
  onRetry?: () => void;
}) {
  return (
    <div role="alert" className="flex flex-col items-start gap-2 rounded-[10px] border border-red-200 bg-red-50 p-4">
      <p className="m-0 text-[14px] font-semibold text-red-700">{title}</p>
      {children && <p className="m-0 text-[13px] text-red-600">{children}</p>}
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-1 rounded-[8px] border border-red-300 px-3 py-1.5 text-[13px] font-medium text-red-700 outline-none focus-visible:ring-2 focus-visible:ring-red-500/40"
        >
          Tekrar dene
        </button>
      )}
    </div>
  );
}
