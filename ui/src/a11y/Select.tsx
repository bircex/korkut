import { useCallback, useEffect, useId, useLayoutEffect, useMemo, useRef, useState, type CSSProperties } from "react";
import { createPortal } from "react-dom";
import { CaretDown, Check } from "../icons";

// Accessible custom select, following the WAI-ARIA "select-only combobox" pattern:
//   * the trigger is a button with role="combobox" (aria-haspopup="listbox", aria-expanded);
//   * focus stays on the trigger, the highlighted option is announced via aria-activedescendant;
//   * ArrowUp/ArrowDown/Home/End/PageUp/PageDown move, Enter/Space choose, Escape closes,
//     typing letters jumps to the next matching option, Tab closes;
//   * the list is rendered in a portal with fixed positioning so scroll containers and
//     dialogs never clip it, and it flips above the trigger when there is no room below.
// A hidden input mirrors the value so native form validation (`required`) and `name` still work.

export type SelectOption = { value: string; label: string; disabled?: boolean };
export type SelectSize = "lg" | "md" | "sm" | "xs";

const SIZE_CLS: Record<SelectSize, string> = {
  lg: "rounded-[10px] px-3.5 py-3 text-[15px]",
  md: "rounded-lg px-3 py-2 text-sm",
  sm: "rounded-lg px-2.5 py-1.5 text-[13px]",
  xs: "rounded-md px-2 py-1 text-[12px]",
};

const MAX_LIST_HEIGHT = 280;
const GAP = 6;

type Placement = { style: CSSProperties; above: boolean };

export function Select({
  value,
  onChange,
  options,
  placeholder,
  size = "md",
  className = "",
  id,
  name,
  disabled,
  required,
  title,
  ...aria
}: {
  value: string;
  onChange: (value: string) => void;
  options: SelectOption[];
  /** Adds a leading empty choice with this label (shown muted while nothing is chosen). */
  placeholder?: string;
  size?: SelectSize;
  /** Layout classes for the trigger (width, flex, margins). */
  className?: string;
  id?: string;
  name?: string;
  disabled?: boolean;
  required?: boolean;
  title?: string;
  "aria-label"?: string;
  "aria-labelledby"?: string;
  "aria-describedby"?: string;
  "aria-errormessage"?: string;
  "aria-invalid"?: boolean;
}) {
  const listId = useId();
  const triggerRef = useRef<HTMLButtonElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const typed = useRef({ text: "", at: 0 });
  const followActive = useRef(true);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const [placement, setPlacement] = useState<Placement | null>(null);

  const items: SelectOption[] = useMemo(
    () => (placeholder !== undefined ? [{ value: "", label: placeholder }, ...options] : options),
    [placeholder, options],
  );
  const selectedIndex = items.findIndex((o) => o.value === value);
  const selected = selectedIndex >= 0 ? items[selectedIndex] : undefined;
  // An empty-valued choice ("Dosya (opsiyonel)", "Seçiniz") reads as a placeholder, not a value.
  const isPlaceholder = !selected || selected.value === "";
  const shown = selected?.label ?? placeholder ?? "";

  const step = useCallback(
    (from: number, dir: 1 | -1) => {
      for (let i = from + dir; i >= 0 && i < items.length; i += dir) if (!items[i].disabled) return i;
      return from;
    },
    [items],
  );
  const edge = useCallback(
    (dir: 1 | -1) => {
      const start = dir === 1 ? -1 : items.length;
      return step(start, dir);
    },
    [items, step],
  );

  const position = useCallback(() => {
    const trigger = triggerRef.current;
    if (!trigger) return;
    const r = trigger.getBoundingClientRect();
    const below = window.innerHeight - r.bottom - GAP - 8;
    const above = r.top - GAP - 8;
    const flip = below < Math.min(MAX_LIST_HEIGHT, 160) && above > below;
    const maxHeight = Math.max(120, Math.min(MAX_LIST_HEIGHT, flip ? above : below));
    const left = Math.max(8, Math.min(r.left, window.innerWidth - r.width - 8));
    setPlacement({
      above: flip,
      style: {
        position: "fixed",
        left,
        minWidth: r.width,
        maxWidth: Math.max(r.width, window.innerWidth - 16),
        maxHeight,
        ...(flip ? { bottom: window.innerHeight - r.top + GAP } : { top: r.bottom + GAP }),
      },
    });
  }, []);

  useLayoutEffect(() => {
    if (!open) return;
    position();
    window.addEventListener("resize", position);
    // Scrolling the list itself must not re-run the positioning (and the scroll-to-active below).
    const onScroll = (e: Event) => {
      if (!listRef.current?.contains(e.target as Node)) position();
    };
    window.addEventListener("scroll", onScroll, true);
    return () => {
      window.removeEventListener("resize", position);
      window.removeEventListener("scroll", onScroll, true);
    };
  }, [open, position]);

  useEffect(() => {
    if (!open) return;
    function onDown(e: MouseEvent) {
      const t = e.target as Node;
      if (!listRef.current?.contains(t) && !triggerRef.current?.contains(t)) setOpen(false);
    }
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, [open]);

  // Keyboard moves (and the first open) scroll the highlighted option into view; hovering must not,
  // or the list would jump while the pointer is on it.
  const hasPlacement = placement !== null;
  useEffect(() => {
    if (!open || !hasPlacement || active < 0) return;
    if (!followActive.current) {
      followActive.current = true;
      return;
    }
    listRef.current?.children[active]?.scrollIntoView?.({ block: "nearest" });
  }, [open, active, hasPlacement]);

  function openList(index?: number) {
    if (disabled) return;
    setActive(index ?? (selectedIndex >= 0 && !items[selectedIndex].disabled ? selectedIndex : edge(1)));
    setOpen(true);
  }

  function choose(index: number) {
    const item = items[index];
    if (!item || item.disabled) return;
    setOpen(false);
    if (item.value !== value) onChange(item.value);
    triggerRef.current?.focus();
  }

  function typeAhead(char: string) {
    const now = Date.now();
    const t = typed.current;
    t.text = now - t.at > 700 ? char : t.text + char;
    t.at = now;
    const q = t.text.toLocaleLowerCase("tr");
    const from = open ? active : selectedIndex;
    // A repeated single letter cycles through the options starting with it.
    const cycle = q.length > 1 && [...q].every((c) => c === q[0]);
    const needle = cycle ? q[0] : q;
    const order = [...items.keys()];
    const start = cycle || q.length === 1 ? from + 1 : from;
    const rotated = [...order.slice(Math.max(start, 0)), ...order.slice(0, Math.max(start, 0))];
    const hit = rotated.find((i) => !items[i].disabled && items[i].label.toLocaleLowerCase("tr").startsWith(needle));
    if (hit === undefined) return;
    if (open) setActive(hit);
    else openList(hit);
  }

  function onKeyDown(e: React.KeyboardEvent) {
    if (disabled) return;
    const k = e.key;
    if (!open) {
      if (k === "ArrowDown" || k === "ArrowUp" || k === "Enter" || k === " ") {
        e.preventDefault();
        openList(k === "ArrowUp" && selectedIndex < 0 ? edge(-1) : undefined);
      } else if (k.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) {
        e.preventDefault();
        typeAhead(k);
      }
      return;
    }
    switch (k) {
      case "Escape":
        // Keep the Escape for the list: an enclosing Dialog must not close with it.
        e.preventDefault();
        e.stopPropagation();
        setOpen(false);
        break;
      case "ArrowDown":
        e.preventDefault();
        setActive((i) => step(i, 1));
        break;
      case "ArrowUp":
        e.preventDefault();
        setActive((i) => step(i, -1));
        break;
      case "Home":
        e.preventDefault();
        setActive(edge(1));
        break;
      case "End":
        e.preventDefault();
        setActive(edge(-1));
        break;
      case "PageDown":
        e.preventDefault();
        setActive((i) => {
          let n = i;
          for (let c = 0; c < 8; c++) n = step(n, 1);
          return n;
        });
        break;
      case "PageUp":
        e.preventDefault();
        setActive((i) => {
          let n = i;
          for (let c = 0; c < 8; c++) n = step(n, -1);
          return n;
        });
        break;
      case "Enter":
      case " ":
        e.preventDefault();
        if (active >= 0) choose(active);
        break;
      case "Tab":
        setOpen(false);
        break;
      default:
        if (k.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) {
          e.preventDefault();
          typeAhead(k);
        }
    }
  }

  const optionId = (i: number) => `${listId}-o${i}`;

  return (
    <div className={`relative min-w-0 ${className}`}>
      <button
        ref={triggerRef}
        type="button"
        id={id}
        role="combobox"
        title={title}
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? listId : undefined}
        aria-activedescendant={open && active >= 0 ? optionId(active) : undefined}
        aria-required={required || undefined}
        {...aria}
        onClick={() => (open ? setOpen(false) : openList())}
        onKeyDown={onKeyDown}
        onBlur={() => setOpen(false)}
        className={
          `flex w-full items-center justify-between gap-2 border border-zinc-300 bg-white text-left shadow-sm outline-none transition-colors hover:border-zinc-400 focus-visible:border-zinc-900 focus-visible:ring-2 focus-visible:ring-zinc-900/20 disabled:cursor-not-allowed disabled:opacity-50 aria-[invalid=true]:border-red-500 ${SIZE_CLS[size]} ` +
          (open ? "border-zinc-900 ring-2 ring-zinc-900/20" : "")
        }
      >
        <span className={`min-w-0 flex-1 truncate ${isPlaceholder ? "text-zinc-400" : "text-zinc-900"}`}>
          {shown || " "}
        </span>
        <CaretDown
          size={14}
          weight="bold"
          aria-hidden
          className={`shrink-0 text-zinc-400 transition-transform ${open ? "rotate-180" : ""}`}
        />
      </button>
      {(required || name) && (
        <input
          tabIndex={-1}
          aria-hidden
          name={name}
          required={required}
          value={value}
          onChange={() => {}}
          disabled={disabled}
          className="pointer-events-none absolute inset-0 h-full w-full opacity-0"
        />
      )}
      {open &&
        placement &&
        createPortal(
          <ul
            ref={listRef}
            id={listId}
            role="listbox"
            aria-label={aria["aria-label"]}
            aria-labelledby={aria["aria-labelledby"]}
            style={placement.style}
            // mousedown would blur the trigger (closing the list) before a click or scrollbar drag lands.
            onMouseDown={(e) => e.preventDefault()}
            className="z-[100] m-0 list-none overflow-y-auto overscroll-contain [scrollbar-color:var(--color-zinc-300)_transparent] [scrollbar-width:thin] rounded-xl border border-zinc-200 bg-white p-1 shadow-lg ring-1 ring-zinc-950/5"
          >
            {items.map((o, i) => (
              <li
                key={`${o.value}-${i}`}
                id={optionId(i)}
                role="option"
                aria-selected={o.value === value}
                aria-disabled={o.disabled || undefined}
                onClick={() => choose(i)}
                onMouseMove={() => {
                  if (o.disabled || active === i) return;
                  followActive.current = false;
                  setActive(i);
                }}
                className={
                  "flex cursor-pointer items-center justify-between gap-3 rounded-lg px-2.5 py-1.5 text-[13.5px] " +
                  (o.disabled
                    ? "cursor-not-allowed text-zinc-400 "
                    : i === active
                      ? "bg-zinc-100 text-zinc-900 "
                      : "text-zinc-700 ") +
                  (o.value === value ? "font-medium" : "")
                }
              >
                <span className={`min-w-0 flex-1 ${o.value === "" ? "text-zinc-400" : ""}`}>{o.label || " "}</span>
                {o.value === value && <Check size={14} weight="bold" aria-hidden className="shrink-0 text-zinc-900" />}
              </li>
            ))}
          </ul>,
          document.body,
        )}
    </div>
  );
}
