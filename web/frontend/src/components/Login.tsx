import { useState } from "react";
import { api, ApiError, type LoginStep } from "../api";
import { AuthShell, Brand, Button, ErrorText, Field } from "../ui";

export function Login({ onStep }: { onStep: (step: LoginStep) => void }) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      const { step } = await api.login(username, password);
      onStep(step);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Giriş başarısız");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthShell>
      <div className="mb-4">
        <Brand subtitle="Korkut — hukuk asistanınız" />
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
      </form>
    </AuthShell>
  );
}
