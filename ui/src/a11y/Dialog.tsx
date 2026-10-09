import { useCallback, useEffect, useId, useRef, type ReactNode } from "react";

// Accessible modal dialog (KDM-029). Implements the WAI-ARIA dialog pattern:
//   * role="dialog" aria-modal, labelled by its title (+ described by its body);
//   * focus moves into the dialog on open and is TRAPPED (Tab/Shift+Tab cycle);
//   * Escape closes; a click on the backdrop closes;
//   * focus is RESTORED to the trigger element on close.
// No focus can leak to the page behind it, so keyboard users are never stranded.

const FOCUSABLE =
  'a[href],button:not([disabled]),textarea:not([disabled]),input:not([disabled]),select:not([disabled]),[tabindex]:not([tabindex="-1"])';

export function Dialog({
  open,
  onClose,
  title,
  children,
  footer,
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
  footer?: ReactNode;
}) {
  const panelRef = useRef<HTMLDivElement>(null);
  const restoreTo = useRef<HTMLElement | null>(null);
  const titleId = useId();
  const bodyId = useId();

  const focusables = useCallback(
    () =>
      Array.from(panelRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? []).filter(
        (el) => !el.hasAttribute("hidden") && el.getAttribute("aria-hidden") !== "true",
      ),
    [],
  );

  useEffect(() => {
    if (!open) return;
    restoreTo.current = document.activeElement as HTMLElement | null;
    // Move focus into the dialog (first focusable, else the panel itself).
    const first = focusables()[0] ?? panelRef.current;
    first?.focus();

    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
        return;
      }
      if (e.key !== "Tab") return;
      const items = focusables();
      if (items.length === 0) {
        e.preventDefault();
        panelRef.current?.focus();
        return;
      }
      const first = items[0];
      const last = items[items.length - 1];
      const active = document.activeElement;
      if (e.shiftKey && (active === first || active === panelRef.current)) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && active === last) {
        e.preventDefault();
        first.focus();
      }
    }

    document.addEventListener("keydown", onKeyDown, true);
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKeyDown, true);
      document.body.style.overflow = prevOverflow;
      restoreTo.current?.focus?.();
    };
  }, [open, onClose, focusables]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={bodyId}
        tabIndex={-1}
        className="flex w-full max-w-md flex-col gap-4 rounded-2xl bg-white p-6 text-zinc-900 shadow-xl outline-none"
      >
        <h2 id={titleId} className="m-0 text-lg font-bold">
          {title}
        </h2>
        <div id={bodyId} className="text-[14px] text-zinc-600">
          {children}
        </div>
        {footer && <div className="flex justify-end gap-2">{footer}</div>}
      </div>
    </div>
  );
}

export function ConfirmDialog({
  open,
  onCancel,
  onConfirm,
  title,
  message,
  confirmLabel = "Onayla",
  cancelLabel = "Vazgeç",
  destructive = false,
}: {
  open: boolean;
  onCancel: () => void;
  onConfirm: () => void;
  title: string;
  message: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
}) {
  return (
    <Dialog
      open={open}
      onClose={onCancel}
      title={title}
      footer={
        <>
          <button
            type="button"
            onClick={onCancel}
            className="rounded-[10px] border border-zinc-300 px-4 py-2.5 text-[14px] font-medium text-zinc-700 outline-none focus-visible:ring-2 focus-visible:ring-zinc-900/20"
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            onClick={onConfirm}
            className={
              destructive
                ? "rounded-[10px] bg-red-600 px-4 py-2.5 text-[14px] font-semibold text-white outline-none focus-visible:ring-2 focus-visible:ring-red-500/40"
                : "rounded-[10px] bg-zinc-900 px-4 py-2.5 text-[14px] font-semibold text-white outline-none focus-visible:ring-2 focus-visible:ring-zinc-900/30"
            }
          >
            {confirmLabel}
          </button>
        </>
      }
    >
      {message}
    </Dialog>
  );
}
