import { useEffect, useState } from "react";

// Tema modu: kullanıcı seçimi localStorage'da yaşar; "system" OS tercihini izler.
// Uygulanan tema <html data-theme="light|dark"> attribute'udur — styles.css'teki koyu palet
// ve Tailwind'in dark: variant'ı bu attribute'a bakar. İlk boyamada FOUC olmasın diye aynı
// mantığın minik bir kopyası index.html'de inline script olarak da çalışır.

export type ThemeMode = "light" | "dark" | "system";

const KEY = "korkut:theme";
const listeners = new Set<() => void>();

function stored(): ThemeMode {
  try {
    const v = localStorage.getItem(KEY);
    return v === "light" || v === "dark" ? v : "system";
  } catch {
    return "system";
  }
}

let mode: ThemeMode = stored();

const media = window.matchMedia("(prefers-color-scheme: dark)");

function apply() {
  const dark = mode === "dark" || (mode === "system" && media.matches);
  document.documentElement.dataset.theme = dark ? "dark" : "light";
  // Tarayıcı çubuğu rengi de uygulanan temayı izlesin.
  document
    .querySelectorAll('meta[name="theme-color"]')
    .forEach((m) => m.setAttribute("content", dark ? "#17171a" : "#f4f4f5"));
}

function notify() {
  listeners.forEach((l) => l());
}

// "Sistem" modundayken OS tercihi değişirse anında geçiş.
media.addEventListener("change", () => {
  if (mode === "system") {
    apply();
    notify();
  }
});

export function setThemeMode(next: ThemeMode) {
  mode = next;
  try {
    localStorage.setItem(KEY, next);
  } catch {
    /* storage kapalıysa seçim oturumla sınırlı kalır */
  }
  apply();
  notify();
}

export function useThemeMode(): ThemeMode {
  const [m, setM] = useState(mode);
  useEffect(() => {
    const l = () => setM(mode);
    listeners.add(l);
    return () => {
      listeners.delete(l);
    };
  }, []);
  return m;
}

apply();
