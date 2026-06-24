import { useEffect, useState } from "react";
import { api, type User } from "./api";
import { Login } from "./components/Login";
import { Register } from "./components/Register";
import { Enroll } from "./components/Enroll";
import { Mfa } from "./components/Mfa";
import { Workspace } from "./components/Workspace";

type Stage = "loading" | "login" | "register" | "enroll" | "mfa" | "chat";

export function App() {
  const [stage, setStage] = useState<Stage>("loading");
  const [user, setUser] = useState<User | null>(null);

  useEffect(() => {
    api
      .me()
      .then((u) => {
        setUser(u);
        setStage("chat");
      })
      .catch(() => setStage("login"));
  }, []);

  function authed(u: User) {
    setUser(u);
    setStage("chat");
  }

  async function logout() {
    try {
      await api.logout();
    } catch {
      /* ignore — we drop the session locally regardless */
    }
    setUser(null);
    setStage("login");
  }

  switch (stage) {
    case "loading":
      return <div className="grid min-h-dvh place-items-center text-sm text-zinc-500">…</div>;
    case "login":
      return <Login onStep={(step) => setStage(step)} onRegister={() => setStage("register")} />;
    case "register":
      return <Register onStep={(step) => setStage(step)} onBack={() => setStage("login")} />;
    case "enroll":
      return <Enroll onDone={authed} />;
    case "mfa":
      return <Mfa onDone={authed} />;
    case "chat":
      return user ? (
        <Workspace user={user} onLogout={logout} />
      ) : (
        <Login onStep={(s) => setStage(s)} onRegister={() => setStage("register")} />
      );
  }
}
