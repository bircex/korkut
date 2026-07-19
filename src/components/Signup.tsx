import { useEffect, useState } from "react";
import { api, ApiError } from "../api";
import { navigate } from "../router";
import { AuthShell, Brand } from "../ui";

const inputCls =
  "rounded-[10px] border border-zinc-300 bg-white px-3 py-2.5 text-sm outline-none focus:border-zinc-900";

// Reached after a Google-verified sign-in with no existing account (callback → /kayit). The signup
// ticket cookie carries the verified email; here the firm requests a demo. The office it creates is
// disabled until a superadmin approves it, so there is no session — we show a "pending" screen.
export function Signup() {
  const [email, setEmail] = useState<string | null>(null);
  const [checking, setChecking] = useState(true);
  const [officeName, setOfficeName] = useState("");
  const [fullName, setFullName] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);

  useEffect(() => {
    api
      .signupPending()
      .then((r) => {
        setEmail(r.email);
        setChecking(false);
      })
      .catch(() => navigate("/giris", { replace: true })); // no/expired ticket, or demo closed
  }, []);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      await api.demoRequest(officeName.trim(), fullName.trim() || undefined);
      setDone(true);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Talep gönderilemedi");
      setBusy(false);
    }
  }

  if (checking) {
    return <div className="grid min-h-dvh place-items-center text-sm text-zinc-500">…</div>;
  }

  if (done) {
    return (
      <AuthShell>
        <div className="mb-1">
          <Brand subtitle="Demo talebiniz alındı" />
        </div>
        <div className="rounded-[10px] border border-emerald-200 bg-emerald-50 px-3 py-3 text-[13px] text-emerald-800">
          Talebiniz alındı. Ekibimiz onayladığında <span className="font-medium">{email}</span> ile
          Google’dan giriş yapabilirsiniz.
        </div>
        <button
          onClick={() => navigate("/giris")}
          className="mt-1 text-[13px] text-zinc-500 underline-offset-2 hover:text-zinc-900 hover:underline"
        >
          ← Girişe dön
        </button>
      </AuthShell>
    );
  }

  return (
    <AuthShell>
      <div className="mb-1">
        <Brand subtitle="Demo talep edin" />
      </div>
      <p className="m-0 text-[13px] text-zinc-500">
        <span className="font-medium text-zinc-700">{email}</span> ile giriş yaptınız. Büronuz için
        demo talep edin; onaylandığında giriş yapabilirsiniz.
      </p>
      <form onSubmit={submit} className="mt-1 flex flex-col gap-2">
        <input
          value={officeName}
          onChange={(e) => setOfficeName(e.target.value)}
          placeholder="Büro adı"
          aria-label="Büro adı"
          autoFocus
          className={inputCls}
        />
        <input
          value={fullName}
          onChange={(e) => setFullName(e.target.value)}
          placeholder="Ad Soyad (opsiyonel)"
          aria-label="Ad Soyad (opsiyonel)"
          className={inputCls}
        />
        <button
          type="submit"
          disabled={busy || !officeName.trim()}
          className="mt-1 rounded-[10px] bg-zinc-900 px-4 py-3 text-[15px] font-semibold text-white transition-opacity hover:opacity-90 disabled:opacity-50"
        >
          {busy ? "Gönderiliyor…" : "Demo talep et"}
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
