import { useEffect, useState } from "react";
import { api, type User } from "./api";
import { onSessionExpired, onAuthChanged } from "./session";
import { SignIn } from "./components/SignIn";
import { Signup } from "./components/Signup";
import { Workspace } from "./components/Workspace";
import { navigate, usePath } from "./router";

// A deep link visited while signed out (e.g. /dosyalar/12). Google sign-in is a full-page
// round-trip that lands back on "/", so we stash the target and restore it once authed.
const NEXT_KEY = "korkut:next";

export function App() {
  const path = usePath();
  const [status, setStatus] = useState<"loading" | "anon" | "authed">("loading");
  const [user, setUser] = useState<User | null>(null);

  // Probe the session cookie. Runs on mount and again whenever a fresh sign-in
  // (local login / TOTP) signals an auth change — no full-page reload needed.
  useEffect(() => {
    function checkSession() {
      api
        .me()
        .then((u) => {
          setUser(u);
          setStatus("authed");
          let next: string | null = null;
          try {
            next = sessionStorage.getItem(NEXT_KEY);
            sessionStorage.removeItem(NEXT_KEY);
          } catch {
            /* storage kapalı olabilir */
          }
          const here = window.location.pathname + window.location.search;
          if (next && next.startsWith("/") && next !== here) navigate(next, { replace: true });
        })
        .catch(() => {
          setStatus("anon");
          try {
            const here = window.location.pathname + window.location.search;
            if (here && here !== "/" && !here.startsWith("/giris")) {
              sessionStorage.setItem(NEXT_KEY, here);
            }
          } catch {
            /* derin link korunmazsa giriş yine çalışır */
          }
        });
    }
    checkSession();
    return onAuthChanged(checkSession);
  }, []);

  // A hard session expiry (access 401 + refresh exhausted) drops us to anon and the
  // sign-in screen. Because the refresh is single-flight + retry-once in session.ts,
  // this fires at most once per expiry — never in a loop.
  useEffect(() => {
    return onSessionExpired(() => {
      setUser(null);
      setStatus("anon");
      if (!window.location.pathname.startsWith("/giris")) {
        navigate("/giris", { replace: true });
      }
    });
  }, []);

  // Tab title follows the signed-in office; falls back to the product name.
  useEffect(() => {
    document.title = user?.office ? `${user.office} · Korkut` : "Korkut";
  }, [user]);

  async function logout() {
    try {
      await api.logout();
    } catch {
      /* ignore — we drop the session locally regardless */
    }
    setUser(null);
    setStatus("anon");
    navigate("/giris", { replace: true });
  }

  if (status === "loading") {
    return <div className="grid min-h-dvh place-items-center text-sm text-zinc-500">…</div>;
  }
  if (status === "authed" && user) {
    return <Workspace user={user} onLogout={logout} />;
  }
  // Anonymous: the self-serve "create your firm" step lives at /kayit; everything else is sign-in.
  return path.startsWith("/kayit") ? <Signup /> : <SignIn />;
}
