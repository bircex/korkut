import { useState } from "react";
import { api, ApiError, type LoginStep, type User } from "../api";
import { AuthShell, Brand, Button, ErrorText, Field } from "../ui";

export function Login({
  onStep,
  onAuthed,
  onRegister,
}: {
  onStep: (step: LoginStep) => void;
  onAuthed: (user: User) => void;
  onRegister: () => void;
}) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      const res = await api.login(username, password);
      if (res.step === "done") onAuthed(res.user);
      else onStep(res.step);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Giriş başarısız");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthShell>
      <div className="mb-4">
        <Brand subtitle="Hukuk asistanınız" />
      </div>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <Field
          label="Kullanıcı adı"
          autoComplete="username"
          autoFocus
          value={username}
          onChange={(e) => setUsername(e.target.value)}
        />
        <Field
          label="Parola"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        <Button type="submit" disabled={busy || !username || !password} className="mt-2">
          {busy ? "Kontrol ediliyor…" : "Giriş yap"}
        </Button>
        <ErrorText>{error}</ErrorText>
        <button
          type="button"
          onClick={onRegister}
          className="mt-1 text-[13px] text-zinc-500 underline-offset-2 hover:text-zinc-900 hover:underline"
        >
          Hesabınız yok mu? Hukuk büronuzu oluşturun
        </button>
      </form>
    </AuthShell>
  );
}
