import { useEffect, useState } from "react";

// Minimal client-side router — no router library. `navigate()` pushes/replaces browser history
// and notifies every subscriber; back/forward (`popstate`) does the same. The URL is the source
// of truth for every screen: top-level pages (/dosyalar, /takvim, ...), record-level routes
// (/sohbet/:id, /dosyalar/:id, /sablonlar/:id) and view options in the query string
// (?mod=hafta&g=2026-07-15). Components read it via usePath()/useSearch() + matchPath() and
// write it back via navigate() — state follows the URL, never the other way around.

type Listener = () => void;

const listeners = new Set<Listener>();

function currentPath(): string {
  return window.location.pathname;
}

function currentSearch(): string {
  return window.location.search;
}

function notify() {
  listeners.forEach((l) => l());
}

window.addEventListener("popstate", notify);

export function navigate(to: string, opts: { replace?: boolean } = {}) {
  // `to` may carry a query string; compare the full target so ?a=1 → ?a=2 still navigates.
  const url = new URL(to, window.location.origin);
  const target = url.pathname + url.search;
  if (target === window.location.pathname + window.location.search) return;
  if (opts.replace) {
    window.history.replaceState(null, "", target);
  } else {
    window.history.pushState(null, "", target);
  }
  notify();
}

export function usePath(): string {
  const [path, setPath] = useState(currentPath);

  useEffect(() => {
    const listener = () => setPath(currentPath());
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  }, []);

  return path;
}

export function useSearch(): string {
  const [search, setSearch] = useState(currentSearch);

  useEffect(() => {
    const listener = () => setSearch(currentSearch());
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  }, []);

  return search;
}

// "/sohbet/:id" gibi bir kalıbı yola eşler; eşleşirse param sözlüğü, eşleşmezse null döner.
export function matchPath(pattern: string, path: string): Record<string, string> | null {
  const pat = pattern.split("/").filter(Boolean);
  const seg = path.split("/").filter(Boolean);
  if (pat.length !== seg.length) return null;
  const params: Record<string, string> = {};
  for (let i = 0; i < pat.length; i++) {
    if (pat[i].startsWith(":")) params[pat[i].slice(1)] = decodeURIComponent(seg[i]);
    else if (pat[i] !== seg[i]) return null;
  }
  return params;
}
