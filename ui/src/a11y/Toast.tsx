/* eslint-disable react-refresh/only-export-components -- provider + its hook are colocated by design */
import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from "react";

// Accessible toast system (KDM-029). Toasts render into a single polite live region
// so screen readers announce them without stealing focus. Errors use role="alert"
// (assertive) so they are announced promptly.

type ToastKind = "info" | "success" | "error";
type Toast = { id: number; kind: ToastKind; message: string };

type ToastApi = {
  notify: (message: string, kind?: ToastKind) => void;
};

const ToastContext = createContext<ToastApi | null>(null);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const nextId = useRef(1);

  const notify = useCallback((message: string, kind: ToastKind = "info") => {
    const id = nextId.current++;
    setToasts((t) => [...t, { id, kind, message }]);
    // Auto-dismiss; errors linger a little longer.
    window.setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), kind === "error" ? 6000 : 4000);
  }, []);

  const api = useMemo(() => ({ notify }), [notify]);

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="pointer-events-none fixed bottom-4 right-4 z-50 flex flex-col gap-2">
        <div aria-live="polite" aria-atomic="false" className="flex flex-col gap-2">
          {toasts
            .filter((t) => t.kind !== "error")
            .map((t) => (
              <ToastCard key={t.id} toast={t} />
            ))}
        </div>
        <div aria-live="assertive" aria-atomic="false" className="flex flex-col gap-2">
          {toasts
            .filter((t) => t.kind === "error")
            .map((t) => (
              <ToastCard key={t.id} toast={t} />
            ))}
        </div>
      </div>
    </ToastContext.Provider>
  );
}

function ToastCard({ toast }: { toast: Toast }) {
  const tone =
    toast.kind === "error"
      ? "border-red-200 bg-red-50 text-red-700"
      : toast.kind === "success"
        ? "border-green-200 bg-green-50 text-green-700"
        : "border-zinc-200 bg-white text-zinc-700";
  return (
    <div
      role={toast.kind === "error" ? "alert" : "status"}
      className={`pointer-events-auto min-w-56 max-w-sm rounded-[10px] border px-4 py-3 text-[14px] shadow-md ${tone}`}
    >
      {toast.message}
    </div>
  );
}

export function useToast(): ToastApi {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within a ToastProvider");
  return ctx;
}
