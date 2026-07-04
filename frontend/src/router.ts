import { useEffect, useState } from "react";

// Minimal client-side path store — no router library. `navigate()` pushes/replaces browser
// history and notifies every `usePath()` subscriber; the back/forward buttons (`popstate`) do the
// same. Deliberately just a path string, not routes/params: this app only needs top-level pages
// (Ayarlar, Dosyalar, ...) to have real URLs, not deep record-level state (which chat, which
// matter) — see App.tsx / Workspace.tsx for how each screen maps a path to its own state.

type Listener = () => void;

const listeners = new Set<Listener>();

function currentPath(): string {
  return window.location.pathname;
}

function notify() {
  listeners.forEach((l) => l());
}

window.addEventListener("popstate", notify);

export function navigate(path: string, opts: { replace?: boolean } = {}) {
  if (path === window.location.pathname) return;
  if (opts.replace) {
    window.history.replaceState(null, "", path);
  } else {
    window.history.pushState(null, "", path);
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
