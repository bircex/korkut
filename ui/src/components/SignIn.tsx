import { useEffect, useState } from "react";
import { api } from "../api";
import { Button, Field } from "../a11y";
import { signalAuthChanged } from "../session";
import { LocalAuth } from "./LocalAuth";
import { AuthShell, Brand } from "../ui";

// The callback bounces failures back here with ?error=<code>; map each to a friendly line.
const ERROR_MESSAGES: Record<string, string> = {
  verify: "Google doğrulaması tamamlanamadı. Lütfen tekrar deneyin.",
  no_account: "Bu Google hesabı büroya üye değil. Büro sahibinden davet isteyin.",
  no_office: "Bu Google hesabına bağlı aktif bir büro yok. Büro sahibinden davet isteyin.",
  invite: "Davet kullanılamadı: süresi dolmuş, kullanılmış ya da farklı bir e-posta için olabilir.",
  pending: "Bu büro şu an kapalı. Büro sahibiyle görüşün.",
};

type Mode = "login" | "forgot" | "reset" | "invite";

export function SignIn() {
  // Read the callback's ?error once at mount (a lazy initializer avoids a cascading render).
  const [error] = useState<string | null>(() => {
    const code = new URLSearchParams(window.location.search).get("error");
    if (!code) return null;
    return ERROR_MESSAGES[code] ?? "Giriş yapılamadı. Lütfen tekrar deneyin.";
  });
  // Google sign-in is optional: the button only appears when the install has configured it.
  const [googleConfigured, setGoogleConfigured] = useState(false);
  // E-postadaki sıfırlama bağlantısı /giris?reset=TOKEN, davet bağlantısı /?invite=TOKEN ile
  // gelir → doğrudan ilgili ekran açılır.
  const [resetToken] = useState<string | null>(
    () => new URLSearchParams(window.location.search).get("reset"),
  );
  const [inviteToken] = useState<string | null>(
    () => new URLSearchParams(window.location.search).get("invite"),
  );
  const [mode, setMode] = useState<Mode>(() => {
    const params = new URLSearchParams(window.location.search);
    if (params.get("invite")) return "invite";
    if (params.get("reset")) return "reset";
    return "login";
  });

  useEffect(() => {
    // Drop the ?error / ?reset params so a refresh is clean (side effect only — no setState).
    const params = new URLSearchParams(window.location.search);
    if (params.get("error") || params.get("reset")) {
      window.history.replaceState(null, "", "/giris");
    }
    api
      .config()
      .then((c) => setGoogleConfigured(c.google_configured))
      .catch(() => {});
  }, []);

  const googleClasses =
    "flex items-center justify-center gap-2.5 rounded-[10px] border border-zinc-300 bg-white px-4 py-3 text-[15px] font-semibold text-zinc-800 transition-colors hover:bg-zinc-50";

  return (
    <AuthShell>
      <div className="mb-4">
        <Brand subtitle="Hukuk asistanınız" />
      </div>
      {error && (
        <div className="rounded-[10px] border border-red-200 bg-red-50 px-3 py-2 text-[13px] text-red-700">
          {error}
        </div>
      )}
      {/* Local e-posta/parola + policy-driven TOTP. */}
      {mode === "invite" && (
        <InviteJoin token={inviteToken} onAuthed={signalAuthChanged} />
      )}
      {mode === "forgot" && (
        <ForgotPassword onBack={() => setMode("login")} />
      )}
      {mode === "reset" && (
        <ResetPassword token={resetToken} onDone={() => setMode("login")} />
      )}
      {mode === "login" && (
        <>
          <LocalAuth onAuthed={signalAuthChanged} />
          <button
            type="button"
            onClick={() => setMode("forgot")}
            className="self-start text-[13px] text-zinc-500 underline-offset-2 hover:text-zinc-900 hover:underline"
          >
            Parolamı unuttum
          </button>
        </>
      )}
      {mode === "login" && googleConfigured && (
        <>
          <div className="my-1 flex items-center gap-3 text-[12px] text-zinc-500">
            <span className="h-px flex-1 bg-zinc-200" aria-hidden />
            veya
            <span className="h-px flex-1 bg-zinc-200" aria-hidden />
          </div>
          <a href={api.googleSignInUrl()} className={`mt-1 ${googleClasses}`}>
            <GoogleMark />
            Google ile giriş yap
          </a>
        </>
      )}
    </AuthShell>
  );
}

// "Parolamı unuttum": e-posta al, sıfırlama bağlantısını gönder. Yanıt her zaman aynıdır
// (hesap var/yok sızdırılmaz) — kullanıcıya tek tip bilgi mesajı gösterilir.
function ForgotPassword({ onBack }: { onBack: () => void }) {
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(false);
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      await api.requestPasswordReset(email.trim());
    } catch {
      /* tek tip davranış */
    } finally {
      setBusy(false);
      setSent(true);
    }
  }

  if (sent) {
    return (
      <div className="flex flex-col gap-2 rounded-[10px] border border-emerald-200 bg-emerald-50 px-3 py-3">
        <p className="m-0 text-[13px] text-emerald-700">
          Bu e-posta için bir hesap varsa sıfırlama bağlantısı gönderildi. Gelen kutunuzu kontrol edin.
        </p>
        <button
          onClick={onBack}
          className="self-start text-[13px] font-medium text-emerald-700 underline-offset-2 hover:underline"
        >
          ← Girişe dön
        </button>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-3" aria-labelledby="forgot-heading">
      <h2 id="forgot-heading" className="m-0 text-[15px] font-semibold text-zinc-800">
        Parola sıfırlama
      </h2>
      <Field
        label="E-posta"
        type="email"
        required
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        autoComplete="email"
      />
      <Button type="submit" disabled={busy || !email.trim()}>
        {busy ? "Gönderiliyor…" : "Sıfırlama bağlantısı gönder"}
      </Button>
      <button
        type="button"
        onClick={onBack}
        className="self-start text-[13px] text-zinc-500 underline-offset-2 hover:text-zinc-900 hover:underline"
      >
        ← Girişe dön
      </button>
    </form>
  );
}

// E-postadaki bağlantıdan gelen tek kullanımlık token ile yeni parola belirleme.
function ResetPassword({ token, onDone }: { token: string | null; onDone: () => void }) {
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    if (password.length < 8) {
      setError("Parola en az 8 karakter olmalı.");
      return;
    }
    if (password !== confirm) {
      setError("Parolalar eşleşmiyor.");
      return;
    }
    setBusy(true);
    try {
      await api.resetPassword(token ?? "", password);
      setDone(true);
    } catch {
      setError("Bağlantı geçersiz ya da süresi dolmuş. Yeni bir sıfırlama isteyin.");
    } finally {
      setBusy(false);
    }
  }

  if (done) {
    return (
      <div className="flex flex-col gap-2 rounded-[10px] border border-emerald-200 bg-emerald-50 px-3 py-3">
        <p className="m-0 text-[13px] text-emerald-700">
          Parolanız güncellendi. Yeni parolanızla giriş yapabilirsiniz.
        </p>
        <button
          onClick={onDone}
          className="self-start text-[13px] font-medium text-emerald-700 underline-offset-2 hover:underline"
        >
          Girişe dön
        </button>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-3" aria-labelledby="reset-heading">
      <h2 id="reset-heading" className="m-0 text-[15px] font-semibold text-zinc-800">
        Yeni parola belirleyin
      </h2>
      <Field
        label="Yeni parola"
        type="password"
        required
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        autoComplete="new-password"
      />
      <Field
        label="Yeni parola (tekrar)"
        type="password"
        required
        value={confirm}
        onChange={(e) => setConfirm(e.target.value)}
        autoComplete="new-password"
        error={error || undefined}
      />
      <Button type="submit" disabled={busy || !password || !confirm}>
        {busy ? "Kaydediliyor…" : "Parolayı güncelle"}
      </Button>
    </form>
  );
}

// Davet linkinden katılım: davet bilgisi gösterilir (hangi büro, hangi e-posta), kişi kendi
// parolasını belirler ve doğrudan o büronun üyesi olarak oturum açılır. Bu ekranda "yeni büro
// kaydı" YOKTUR — davetli, davet edenin bürosuna katılır.
function InviteJoin({ token, onAuthed }: { token: string | null; onAuthed: () => void }) {
  const [info, setInfo] = useState<{ office_name: string; email: string; full_name: string | null } | null>(null);
  const [invalid, setInvalid] = useState(() => !token);
  const [fullName, setFullName] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!token) {
      return;
    }
    api
      .peekInvite(token)
      .then((r) => {
        setInfo(r);
        setFullName(r.full_name ?? "");
      })
      .catch(() => setInvalid(true));
  }, [token]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    if (password.length < 10) {
      setError("Parola en az 10 karakter olmalı.");
      return;
    }
    if (password !== confirm) {
      setError("Parolalar eşleşmiyor.");
      return;
    }
    setBusy(true);
    try {
      await api.acceptInviteLocal(token ?? "", password, fullName.trim() || null);
      window.history.replaceState(null, "", "/");
      onAuthed(); // çerezler set edildi → oturum yeniden yoklanır, uygulama açılır
    } catch (err) {
      setError(err instanceof Error ? err.message : "Davet kabul edilemedi.");
      setBusy(false);
    }
  }

  if (invalid) {
    return (
      <div className="flex flex-col gap-2 rounded-[10px] border border-red-200 bg-red-50 px-3 py-3">
        <p className="m-0 text-[13px] text-red-700">
          Davet geçersiz, süresi dolmuş ya da kullanılmış. Büro yöneticinizden yeni bir davet isteyin.
        </p>
      </div>
    );
  }

  if (!info) {
    return <p className="text-[13px] text-zinc-500">Davet doğrulanıyor…</p>;
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-3" aria-labelledby="invite-heading">
      <h2 id="invite-heading" className="m-0 text-[15px] font-semibold text-zinc-800">
        "{info.office_name}" bürosuna katılıyorsunuz
      </h2>
      <p className="m-0 text-[13px] text-zinc-500">
        Hesabınız <span className="font-medium text-zinc-700">{info.email}</span> adresiyle açılacak.
        Parolanızı belirleyin ve devam edin.
      </p>
      <Field
        label="Ad Soyad"
        required
        value={fullName}
        onChange={(e) => setFullName(e.target.value)}
        autoComplete="name"
      />
      <Field
        label="Parola"
        type="password"
        required
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        autoComplete="new-password"
        hint="En az 10 karakter"
      />
      <Field
        label="Parola (tekrar)"
        type="password"
        required
        value={confirm}
        onChange={(e) => setConfirm(e.target.value)}
        autoComplete="new-password"
        error={error || undefined}
      />
      <Button type="submit" disabled={busy || !fullName.trim() || !password || !confirm}>
        {busy ? "Katılıyor…" : "Parolayı belirle ve katıl"}
      </Button>
    </form>
  );
}

function GoogleMark() {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
      <path
        fill="#4285F4"
        d="M17.64 9.2c0-.64-.06-1.25-.16-1.84H9v3.48h4.84a4.14 4.14 0 0 1-1.8 2.72v2.26h2.92c1.71-1.57 2.68-3.89 2.68-6.62z"
      />
      <path
        fill="#34A853"
        d="M9 18c2.43 0 4.47-.8 5.96-2.18l-2.92-2.26c-.8.54-1.84.86-3.04.86-2.34 0-4.32-1.58-5.02-3.7H.96v2.34A9 9 0 0 0 9 18z"
      />
      <path
        fill="#FBBC05"
        d="M3.98 10.72a5.4 5.4 0 0 1 0-3.44V4.94H.96a9 9 0 0 0 0 8.12l3.02-2.34z"
      />
      <path
        fill="#EA4335"
        d="M9 3.58c1.32 0 2.5.45 3.44 1.35l2.58-2.58C13.46.9 11.43 0 9 0A9 9 0 0 0 .96 4.94l3.02 2.34C4.68 5.16 6.66 3.58 9 3.58z"
      />
    </svg>
  );
}
