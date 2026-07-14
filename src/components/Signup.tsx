import { useEffect, useState } from "react";
import { api, ApiError } from "../api";
import { navigate } from "../router";
import { AuthShell, Brand } from "../ui";

const inputCls =
  "rounded-[10px] border border-zinc-300 bg-white px-3 py-2.5 text-sm outline-none focus:border-zinc-900";

// Reached after a Google-verified sign-in with no existing account (callback → /kayit). The signup
// ticket cookie carries the verified email; here we collect the office name + signup code.
export function Signup() {
  const [email, setEmail] = useState<string | null>(null);
  const [checking, setChecking] = useState(true);
  const [officeName, setOfficeName] = useState("");
  const [fullName, setFullName] = useState("");
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api
      .signupPending()
      .then((r) => {
        setEmail(r.email);
        setChecking(false);
      })
      .catch(() => navigate("/giris", { replace: true })); // no/expired ticket, or signup closed
  }, []);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      await api.signup(code.trim(), officeName.trim(), fullName.trim() || undefined);
      window.location.href = "/"; // office + owner created + session set → land authed
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Kayıt tamamlanamadı");
      setBusy(false);
    }
  }

  if (checking) {
    return <div className="grid min-h-dvh place-items-center text-sm text-zinc-500">…</div>;
  }

  return (
    <AuthShell>
      <div className="mb-1">
        <Brand subtitle="Firmanızı oluşturun" />
      </div>
      <p className="m-0 text-[13px] text-zinc-500">
        <span className="font-medium text-zinc-700">{email}</span> ile giriş yaptınız. Büronuzu
        oluşturmak için bilgileri doldurun.
      </p>
      <form onSubmit={submit} className="mt-1 flex flex-col gap-2">
        <input
          value={officeName}
          onChange={(e) => setOfficeName(e.target.value)}
          placeholder="Büro adı"
          autoFocus
          className={inputCls}
        />
        <input
          value={fullName}
          onChange={(e) => setFullName(e.target.value)}
          placeholder="Ad Soyad (opsiyonel)"
          className={inputCls}
        />
        <input
          value={code}
          onChange={(e) => setCode(e.target.value)}
          placeholder="Kayıt kodu"
          autoComplete="off"
          className={inputCls}
        />
        <button
          type="submit"
          disabled={busy || !officeName.trim() || !code.trim()}
          className="mt-1 rounded-[10px] bg-zinc-900 px-4 py-3 text-[15px] font-semibold text-white transition-opacity hover:opacity-90 disabled:opacity-50"
        >
          {busy ? "Oluşturuluyor…" : "Büromu oluştur"}
        </button>
        {error && <p className="m-0 text-[13px] text-red-600">{error}</p>}
      </form>
      <button
        onClick={() => navigate("/giris")}
        className="mt-1 text-[13px] text-zinc-500 underline-offset-2 hover:text-zinc-900 hover:underline"
      >
        ← Girişe dön
      </button>
    </AuthShell>
  );
}
