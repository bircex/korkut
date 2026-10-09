import { useCallback, useEffect, useRef, type ReactNode } from "react";
import { X } from "../icons";

// Sağ drawer (bkz. docs/DRAWER_ROADMAP.md Faz 7): tüm ikincil içerikler (karar/mevzuat
// önizlemesi, dilekçe editörü, İlgili Kararlar) TEK drawer davranışını paylaşır — içerik
// alanını (sol menü hariç) uçtan uca kaplayan, dialog semantiğiyle (focus trap, Esc, X) açılan
// tek bir panel. İçerik türüne göre genişlik farkı yoktur.
//
// Konumlama: `absolute inset-0` ile en yakın `position: relative` atayı kaplar — Workspace'te
// bu, sol menünün DIŞINDAKİ içerik sarmalayıcısıdır, yani sol menü her zaman görünür/tıklanabilir
// kalır. Aynı DOM konumu mobilde de doğru sonucu verir: dar ekranda sol menü akıştan çıkıp
// (fixed) kendi katmanında açıldığından içerik sarmalayıcısı zaten tüm viewport'u kaplar.
//
// Başlık isteğe bağlıdır: `title` verilirse drawer kendi header'ını (başlık + kapat) çizer;
// verilmezse chrome'suz kalır ve içerik kendi header'ını getirir (ör. DocumentEditor'ün
// Geri/Kaydet/İndir şeridi — "Geri" düğmesi kapatma görevi görür). `label` her durumda
// erişilebilir ad olarak zorunludur.

const FOCUSABLE =
  'a[href],button:not([disabled]),textarea:not([disabled]),input:not([disabled]),select:not([disabled]),[tabindex]:not([tabindex="-1"])';

export function RightDrawer({
  open,
  onClose,
  label,
  title,
  headerExtra,
  children,
}: {
  open: boolean;
  onClose: () => void;
  /** Erişilebilir ad (aria-label) — başlıksız (chrome'suz) kullanımda da zorunlu. */
  label: string;
  /** Verilirse drawer kendi header'ını çizer; verilmezse içerik kendi header'ını getirir. */
  title?: string;
  headerExtra?: ReactNode;
  children: ReactNode;
}) {
  const panelRef = useRef<HTMLDivElement>(null);
  const restoreTo = useRef<HTMLElement | null>(null);

  const focusables = useCallback(
    () =>
      Array.from(panelRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? []).filter(
        (el) => !el.hasAttribute("hidden") && el.getAttribute("aria-hidden") !== "true",
      ),
    [],
  );

  // Dialog gibi davranır: açılışta focus içeri taşınır ve hapsedilir, Esc kapatır, kapanışta
  // focus tetikleyene döner. Esc, yalnız `defaultPrevented` değilse kapatır — CKEditor gibi
  // içerikler kendi balonlarını (link/tablo araç çubuğu) kapatırken Esc'i tüketir, bu da drawer'ı
  // erken kapatmaz.
  useEffect(() => {
    if (!open) return;
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
  }, [open, onClose, focusables]);

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

  return (
    <div
      ref={panelRef}
      role="dialog"
      aria-modal="true"
      aria-label={label}
      tabIndex={-1}
      className="drawer-anim absolute inset-0 z-30 flex min-h-0 min-w-0 flex-col overflow-hidden bg-white outline-none"
    >
      {header}
      <div className="flex min-h-0 min-w-0 flex-1 flex-col overflow-y-auto">{children}</div>
    </div>
  );
}
