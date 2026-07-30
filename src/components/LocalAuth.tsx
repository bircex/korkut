import { useState } from "react";
import { api, ApiError } from "../api";
import { Button, Field } from "../a11y";

// Local password sign-in + policy-driven TOTP challenge for the .NET auth contract
// (KDM-030). Cookie-based: no token is ever read or stored in JS. On success the
// caller re-probes the session (onAuthed) to enter the app. Also hosts the local
// e-mail/parola registration, which creates a DISABLED office pending admin approval.

type Step = "credentials" | "totp" | "register" | "registered";

export function LocalAuth({ onAuthed }: { onAuthed: () => void }) {
  const [step, setStep] = useState<Step>("credentials");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [officeName, setOfficeName] = useState("");
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

  async function submitRegister(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await api.register(email.trim(), password, officeName.trim());
      setStep("registered");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Kayıt oluşturulamadı");
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

  function switchTo(next: Step) {
    setError(null);
    setPassword("");
    setStep(next);
  }

  if (step === "registered") {
    return (
      <div className="flex flex-col gap-2 rounded-[10px] border border-emerald-200 bg-emerald-50 px-3 py-3">
        <h2 className="m-0 text-[15px] font-semibold text-emerald-800">Kaydınız alındı</h2>
        <p className="m-0 text-[13px] text-emerald-700">
          Büronuz yönetici onayı bekliyor. Onaylandığında bu e-posta ve parolayla giriş yapabilirsiniz.
        </p>
        <button
          type="button"
          onClick={() => switchTo("credentials")}
          className="self-start text-[13px] font-medium text-emerald-700 underline-offset-2 hover:underline"
        >
          Girişe dön
        </button>
      </div>
    );
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

  if (step === "register") {
    return (
      <form onSubmit={submitRegister} className="flex flex-col gap-3" aria-labelledby="register-heading">
        <h2 id="register-heading" className="m-0 text-[15px] font-semibold text-zinc-800">
          Yeni büro kaydı
        </h2>
        <Field
          label="Büro adı"
          value={officeName}
          onChange={(e) => setOfficeName(e.target.value)}
          required
        />
        <Field
          label="E-posta"
          type="email"
          autoComplete="username"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
        />
        <Field
          label="Parola (en az 10 karakter)"
          type="password"
          autoComplete="new-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          error={error ?? undefined}
          required
        />
        <Button type="submit" disabled={busy || !email.trim() || password.length < 10 || !officeName.trim()}>
          {busy ? "Kaydediliyor…" : "Kayıt ol"}
        </Button>
        <button
          type="button"
          onClick={() => switchTo("credentials")}
          className="text-[13px] text-zinc-500 underline-offset-2 hover:text-zinc-900 hover:underline"
        >
          Zaten hesabınız var mı? Giriş yapın
        </button>
      </form>
    );
  }

  return (
    <form onSubmit={submitCredentials} className="flex flex-col gap-3" aria-labelledby="login-heading">
      <h2 id="login-heading" className="m-0 text-[15px] font-semibold text-zinc-800">
        E-posta ile giriş
      </h2>
      <Field
        label="E-posta"
        type="email"
        autoComplete="username"
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
      <button
        type="button"
        onClick={() => switchTo("register")}
        className="text-[13px] text-zinc-500 underline-offset-2 hover:text-zinc-900 hover:underline"
      >
        Hesabınız yok mu? Yeni büro kaydı
      </button>
    </form>
  );
}
