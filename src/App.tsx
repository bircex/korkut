import { useEffect, useState } from "react";
import { api, type User } from "./api";
import { Login } from "./components/Login";
import { Register } from "./components/Register";
import { Enroll } from "./components/Enroll";
import { Mfa } from "./components/Mfa";
import { Workspace } from "./components/Workspace";
import { navigate, usePath } from "./router";

type AuthStage = "login" | "register" | "enroll" | "mfa";

// Auth-flow stages have real URLs so a reload mid-flow (e.g. an in-progress MFA ticket, which the
// server keeps alive for a few minutes in a cookie) lands back on the right screen instead of
// bouncing to the login form, and so back/forward moves between them like any other page.
const STAGE_PATH: Record<AuthStage, string> = {
  login: "/giris",
  register: "/kayit",
  enroll: "/kurulum",
  mfa: "/mfa",
};

const AUTH_PATHS: string[] = Object.values(STAGE_PATH);

// Girişten önce hedeflenen derin link (ör. /dosyalar/12) — login sonrası oraya dönmek için.
const NEXT_KEY = "korkut:next";

function stageFromPath(path: string): AuthStage {
  const hit = (Object.entries(STAGE_PATH) as [AuthStage, string][]).find(([, p]) => p === path);
  return hit ? hit[0] : "login";
}

export function App() {
  const path = usePath();
  const [status, setStatus] = useState<"loading" | "anon" | "authed">("loading");
  const [user, setUser] = useState<User | null>(null);

  useEffect(() => {
    api
      .me()
      .then((u) => {
        setUser(u);
        setStatus("authed");
        // A stale bookmark to an auth screen while already signed in makes no sense — send them in.
        if (AUTH_PATHS.includes(window.location.pathname)) {
          navigate("/", { replace: true });
        }
      })
      .catch(() => {
        setStatus("anon");
        if (!AUTH_PATHS.includes(window.location.pathname)) {
          // Hedefi sakla ki giriş yaptıktan sonra kaldığı yere dönsün.
          try {
            sessionStorage.setItem(NEXT_KEY, window.location.pathname + window.location.search);
          } catch {
            /* storage kapalıysa derin link korunmaz, giriş yine çalışır */
          }
          navigate("/giris", { replace: true });
        }
      });
  }, []);

  // Tab title follows the signed-in office; falls back to the product name.
  useEffect(() => {
    document.title = user?.office ? `${user.office} · Korkut` : "Korkut";
  }, [user]);

  function authed(u: User) {
    setUser(u);
    setStatus("authed");
    let next: string | null = null;
    try {
      next = sessionStorage.getItem(NEXT_KEY);
      sessionStorage.removeItem(NEXT_KEY);
    } catch {
      /* yoksay */
    }
    // Yalnızca uygulama içi mutlak yollar — auth ekranına geri dönmek anlamsız.
    const safe = next && next.startsWith("/") && !AUTH_PATHS.includes(next.split("?")[0]);
    navigate(safe && next ? next : "/", { replace: true });
  }

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

  // Not authenticated — which screen shows is driven entirely by the URL, so browser back/forward
  // moves between login/register/mfa/enroll like any other page.
  switch (stageFromPath(path)) {
    case "login":
      return (
        <Login
          onStep={(step) => navigate(STAGE_PATH[step])}
          onAuthed={authed}
          onRegister={() => navigate(STAGE_PATH.register)}
        />
      );
    case "register":
      return (
        <Register
          onStep={(step) => navigate(STAGE_PATH[step])}
          onAuthed={authed}
          onBack={() => navigate(STAGE_PATH.login)}
        />
      );
    case "enroll":
      return <Enroll onDone={authed} onBack={() => navigate(STAGE_PATH.login)} />;
    case "mfa":
      return <Mfa onDone={authed} onBack={() => navigate(STAGE_PATH.login)} />;
  }
}
