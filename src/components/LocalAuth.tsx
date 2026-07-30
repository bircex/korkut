import { useState } from "react";
import { api, ApiError } from "../api";
import { Button, Field } from "../a11y";

// Local password sign-in / registration for the .NET auth contract (KDM-030). Cookie-based:
// no token is ever read or stored in JS. Driven by `mode`: "login" shows the credentials +
// TOTP flow (onAuthed on success); "register" shows the new-office form (Ad Soyad, büro, e-posta,
// parola + confirm + strength), which creates a DISABLED office pending platform-admin approval.

type Step = "credentials" | "totp" | "register" | "registered";

const STRENGTH_LABELS = ["Çok zayıf", "Zayıf", "Orta", "İyi", "Güçlü"];
const STRENGTH_COLORS = ["#ef4444", "#f97316", "#eab308", "#84cc16", "#16a34a"];

// A lightweight password-quality heuristic: length + character-class variety, 0..4.
function passwordStrength(pw: string): number {
  if (!pw) return 0;
  let score = 0;
  if (pw.length >= 10) score++;
  if (pw.length >= 14) score++;
  if (/[a-z]/.test(pw) && /[A-Z]/.test(pw)) score++;
  if (/\d/.test(pw)) score++;
  if (/[^A-Za-z0-9]/.test(pw)) score++;
  return Math.min(score, 4);
}

export function LocalAuth({
  mode,
  onAuthed,
  onSwitchToLogin,
}: {
  mode: "login" | "register";
  onAuthed: () => void;
  onSwitchToLogin: () => void;
}) {
  const [step, setStep] = useState<Step>(mode === "register" ? "register" : "credentials");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [fullName, setFullName] = useState("");
  const [officeName, setOfficeName] = useState("");
  const [code, setCode] = useState("");
  const [useRecovery, setUseRecovery] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const strength = passwordStrength(password);

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
    if (password !== confirm) {
      setError("Parolalar eşleşmiyor");
      return;
    }
    setBusy(true);
    try {
      await api.register(fullName.trim(), email.trim(), password, officeName.trim());
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

  if (step === "registered") {
    return (
      <div className="flex flex-col gap-2 rounded-[10px] border border-emerald-200 bg-emerald-50 px-3 py-3">
        <h2 className="m-0 text-[15px] font-semibold text-emerald-800">Kaydınız alındı</h2>
        <p className="m-0 text-[13px] text-emerald-700">
          Büronuz yönetici onayı bekliyor. Onaylandığında bu e-posta ve parolayla giriş yapabilirsiniz.
        </p>
        <button
          type="button"
          onClick={onSwitchToLogin}
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

  if (mode === "register") {
    return (
      <form onSubmit={submitRegister} className="flex flex-col gap-3" aria-labelledby="register-heading">
        <h2 id="register-heading" className="m-0 text-[15px] font-semibold text-zinc-800">
          Yeni büro kaydı
        </h2>
        <Field
          label="Ad Soyad"
          autoComplete="name"
          value={fullName}
          onChange={(e) => setFullName(e.target.value)}
          required
        />
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
          required
        />
        {password.length > 0 && (
          <div className="flex flex-col gap-1">
            <div className="flex gap-1" aria-hidden>
              {[0, 1, 2, 3].map((i) => (
                <span
                  key={i}
                  className="h-1 flex-1 rounded-full"
                  style={{ backgroundColor: i < strength ? STRENGTH_COLORS[strength] : "#e4e4e7" }}
                />
              ))}
            </div>
            <span className="text-[12px]" style={{ color: STRENGTH_COLORS[strength] }}>
              Parola gücü: {STRENGTH_LABELS[strength]}
            </span>
          </div>
        )}
        <Field
          label="Parola (tekrar)"
          type="password"
          autoComplete="new-password"
          value={confirm}
          onChange={(e) => setConfirm(e.target.value)}
          error={
            error ??
            (confirm.length > 0 && confirm !== password ? "Parolalar eşleşmiyor" : undefined)
          }
          required
        />
        <Button
          type="submit"
          disabled={
            busy ||
            !fullName.trim() ||
            !email.trim() ||
            password.length < 10 ||
            strength < 2 ||
            confirm !== password ||
            !officeName.trim()
          }
        >
          {busy ? "Kaydediliyor…" : "Kayıt ol"}
        </Button>
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
    </form>
  );
}
