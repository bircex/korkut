import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { X } from "../icons";

// Sağ drawer (bkz. docs/DRAWER_ROADMAP.md): geniş ekranda içeriği İTEREK açılan kalıcı panel
// (push modu, role="complementary"), `overlayBelow` altındaki ekranlarda üstte açılan overlay
// (dialog semantiği: focus trap, Esc, backdrop). Tek bileşen iki modu da taşır ki çağıran
// yalnızca open/onClose ve içerikle ilgilensin.
//
// Başlık isteğe bağlıdır: `title` verilirse drawer kendi header'ını (başlık + kapat) çizer;
// verilmezse chrome'suz kalır ve içerik kendi header'ını getirir (ör. DocumentEditor'ün
// Geri/Kaydet/İndir şeridi). `label` her iki modda erişilebilir ad olarak zorunludur.

const FOCUSABLE =
  'a[href],button:not([disabled]),textarea:not([disabled]),input:not([disabled]),select:not([disabled]),[tabindex]:not([tabindex="-1"])';

// Kesme noktası takibi: matchMedia jsdom'da olmadığı için resize dinleyicisiyle.
function useIsNarrow(breakpoint: number): boolean {
  const [narrow, setNarrow] = useState(
    () => typeof window !== "undefined" && window.innerWidth < breakpoint,
  );
  useEffect(() => {
    const update = () => setNarrow(window.innerWidth < breakpoint);
    update();
    window.addEventListener("resize", update);
    return () => window.removeEventListener("resize", update);
  }, [breakpoint]);
  return narrow;
}

export function RightDrawer({
  open,
  onClose,
  label,
  title,
  width = "440px",
  overlayWidth,
  overlayBelow = 1024,
  headerExtra,
  children,
}: {
  open: boolean;
  onClose: () => void;
  /** Erişilebilir ad (aria-label) — başlıksız (chrome'suz) kullanımda da zorunlu. */
  label: string;
  /** Verilirse drawer kendi header'ını çizer; verilmezse içerik kendi header'ını getirir. */
  title?: string;
  /** Push modunda panel genişliği; CSS ifadesi olabilir (ör. "clamp(680px, 55vw, 900px)"). */
  width?: string;
  /** Overlay modunda azami genişlik; verilmezse `width` kullanılır. */
  overlayWidth?: string;
  /** Bu viewport genişliğinin altında push yerine overlay açılır. */
  overlayBelow?: number;
  headerExtra?: ReactNode;
  children: ReactNode;
}) {
  const overlay = useIsNarrow(overlayBelow);
  const panelRef = useRef<HTMLDivElement>(null);
  const restoreTo = useRef<HTMLElement | null>(null);

  const focusables = useCallback(
    () =>
      Array.from(panelRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? []).filter(
        (el) => !el.hasAttribute("hidden") && el.getAttribute("aria-hidden") !== "true",
      ),
    [],
  );

  // Overlay modu dialog gibi davranır: focus içeri taşınır ve hapsedilir, Esc kapatır,
  // kapanışta focus tetikleyene döner. Push modunda Esc BİLEREK bağlanmaz — CKEditor gibi
  // içerikler Esc'i kendi balonları için kullanır; kalıcı panel X/Geri ile kapanır.
  useEffect(() => {
    if (!open || !overlay) return;
    restoreTo.current = document.activeElement as HTMLElement | null;
    const first = focusables()[0] ?? panelRef.current;
    first?.focus();

    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape" && !e.defaultPrevented) {
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
      const firstItem = items[0];
      const last = items[items.length - 1];
      const active = document.activeElement;
      if (e.shiftKey && (active === firstItem || active === panelRef.current)) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && active === last) {
        e.preventDefault();
        firstItem.focus();
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
  }, [open, overlay, onClose, focusables]);

  if (!open) return null;

  const header = title ? (
    <header className="flex shrink-0 items-center gap-2 border-b border-zinc-200 px-4 py-2.5">
      <h2 className="min-w-0 flex-1 truncate text-[15px] font-semibold text-zinc-900">{title}</h2>
      {headerExtra}
      <button
        onClick={onClose}
        aria-label="Kapat"
        className="rounded-md p-1 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
      >
        <X size={18} aria-hidden />
      </button>
    </header>
  ) : null;

  if (overlay) {
    return (
      <div
        className="fixed inset-0 z-40 flex justify-end bg-black/30"
        onMouseDown={(e) => {
          if (e.target === e.currentTarget) onClose();
        }}
      >
        <div
          ref={panelRef}
          role="dialog"
          aria-modal="true"
          aria-label={label}
          tabIndex={-1}
          style={{ maxWidth: overlayWidth ?? width }}
          className="drawer-anim flex h-full w-full min-w-0 flex-col border-l border-zinc-200 bg-white shadow-xl outline-none"
        >
          {header}
          <div className="flex min-h-0 min-w-0 flex-1 flex-col overflow-y-auto">{children}</div>
        </div>
      </div>
    );
  }

  return (
    <aside
      role="complementary"
      aria-label={label}
      style={{ width }}
      className="drawer-anim flex min-h-0 shrink-0 flex-col overflow-hidden border-l border-zinc-200 bg-white"
    >
      {header}
      <div className="flex min-h-0 min-w-0 flex-1 flex-col overflow-y-auto">{children}</div>
    </aside>
  );
}
