import { useEffect, useId, useRef, useState, type ReactNode } from "react";

// Accessible menu button (KDM-029), following the WAI-ARIA menu-button pattern:
//   * the trigger has aria-haspopup + aria-expanded;
//   * ArrowDown/ArrowUp move a roving focus through items, Home/End jump to ends;
//   * Enter/Space/click activate; Escape closes and returns focus to the trigger;
//   * a click outside closes.

export type MenuItem = { label: string; onSelect: () => void; destructive?: boolean };

export function Menu({ label, items }: { label: ReactNode; items: MenuItem[] }) {
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const menuId = useId();

  useEffect(() => {
    if (!open) return;
    const itemEls = listRef.current?.querySelectorAll<HTMLElement>('[role="menuitem"]');
    itemEls?.[active]?.focus();
  }, [open, active]);

  useEffect(() => {
    if (!open) return;
    function onDocClick(e: MouseEvent) {
      if (!listRef.current?.contains(e.target as Node) && !triggerRef.current?.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", onDocClick);
    return () => document.removeEventListener("mousedown", onDocClick);
  }, [open]);

  function close(restore = true) {
    setOpen(false);
    if (restore) triggerRef.current?.focus();
  }

  return (
    <div className="relative inline-block">
      <button
        ref={triggerRef}
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={() => {
          setActive(0);
          setOpen((v) => !v);
        }}
        onKeyDown={(e) => {
          if (e.key === "ArrowDown" || e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            setActive(0);
            setOpen(true);
          } else if (e.key === "ArrowUp") {
            e.preventDefault();
            setActive(items.length - 1);
            setOpen(true);
          }
        }}
        className="inline-flex items-center gap-1 rounded-[10px] border border-zinc-300 px-3 py-2 text-[14px] font-medium text-zinc-700 outline-none focus-visible:ring-2 focus-visible:ring-zinc-900/20"
      >
        {label}
      </button>
      {open && (
        <ul
          ref={listRef}
          id={menuId}
          role="menu"
          className="absolute left-0 z-40 mt-1 min-w-44 rounded-[10px] border border-zinc-200 bg-white p-1 shadow-lg"
          onKeyDown={(e) => {
            if (e.key === "Escape") {
              e.preventDefault();
              close();
            } else if (e.key === "ArrowDown") {
              e.preventDefault();
              setActive((i) => (i + 1) % items.length);
            } else if (e.key === "ArrowUp") {
              e.preventDefault();
              setActive((i) => (i - 1 + items.length) % items.length);
            } else if (e.key === "Home") {
              e.preventDefault();
              setActive(0);
            } else if (e.key === "End") {
              e.preventDefault();
              setActive(items.length - 1);
            } else if (e.key === "Tab") {
              close(false);
            }
          }}
        >
          {items.map((item, i) => (
            <li key={item.label} role="none">
              <button
                type="button"
                role="menuitem"
                tabIndex={i === active ? 0 : -1}
                onClick={() => {
                  item.onSelect();
                  close();
                }}
                className={
                  "block w-full rounded-[7px] px-3 py-2 text-left text-[14px] outline-none focus-visible:bg-zinc-100 " +
                  (item.destructive ? "text-red-600" : "text-zinc-700")
                }
              >
                {item.label}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
