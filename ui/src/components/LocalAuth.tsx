import { useState } from "react";
import { api, ApiError } from "../api";
import { Button, Field } from "../a11y";

// Local password sign-in for the .NET auth contract (KDM-030): the credentials, then the TOTP step
// when the account or office requires it; onAuthed on success. Cookie-based: no token is ever
// read or stored in JS.

type Step = "credentials" | "totp";

export function LocalAuth({ onAuthed }: { onAuthed: () => void }) {
  const [step, setStep] = useState<Step>("credentials");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [code, setCode] = useState("");
  const [useRecovery, setUseRecovery] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submitCredentials(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const r = await api.login(email.trim(), password);
      if (r.totp_required) {
        setStep("totp");
      } else {
        onAuthed();
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Giriş yapılamadı");
    } finally {
      setBusy(false);
    }
  }

  async function submitTotp(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await api.verifyTotp(code.trim(), useRecovery);
      onAuthed();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Kod doğrulanamadı");
    } finally {
      setBusy(false);
    }
  }

  if (step === "totp") {
    return (
      <form onSubmit={submitTotp} className="flex flex-col gap-3" aria-labelledby="totp-heading">
        <h2 id="totp-heading" className="m-0 text-[15px] font-semibold text-zinc-800">
          İki adımlı doğrulama
        </h2>
        <Field
          label={useRecovery ? "Kurtarma kodu" : "Doğrulama kodu"}
          inputMode={useRecovery ? "text" : "numeric"}
          autoComplete="one-time-code"
          value={code}
          onChange={(e) => setCode(e.target.value)}
          error={error ?? undefined}
          required
          autoFocus
        />
        <Button type="submit" disabled={busy || !code.trim()}>
          {busy ? "Doğrulanıyor…" : "Doğrula"}
        </Button>
        <button
          type="button"
          onClick={() => {
            setUseRecovery((v) => !v);
            setCode("");
            setError(null);
          }}
          className="text-[13px] text-zinc-500 underline-offset-2 hover:text-zinc-900 hover:underline"
        >
          {useRecovery ? "Doğrulama kodu kullan" : "Kurtarma kodu kullan"}
        </button>
      </form>
    );
  }

  return (
    <form onSubmit={submitCredentials} className="flex flex-col gap-3" aria-label="Giriş">
      <Field
        label="E-posta ya da kullanıcı adı"
        type="text"
        autoComplete="username"
        autoCapitalize="none"
        spellCheck={false}
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        required
      />
      <Field
        label="Parola"
        type="password"
        autoComplete="current-password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        error={error ?? undefined}
        required
      />
      <Button type="submit" disabled={busy || !email.trim() || !password}>
        {busy ? "Giriş yapılıyor…" : "Giriş yap"}
      </Button>
    </form>
  );
}
