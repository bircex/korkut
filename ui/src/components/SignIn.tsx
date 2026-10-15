import { useEffect, useState } from "react";
import { api } from "../api";
import { Button, Field } from "../a11y";
import { signalAuthChanged } from "../session";
import { LocalAuth } from "./LocalAuth";
import { AuthShell, Brand } from "../ui";

type Mode = "login" | "forgot" | "reset" | "invite";

export function SignIn() {
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
    // Drop the ?reset param so a refresh is clean (side effect only — no setState).
    if (new URLSearchParams(window.location.search).get("reset")) {
      window.history.replaceState(null, "", "/giris");
    }
  }, []);

  return (
    <AuthShell>
      <div className="mb-4">
        <Brand subtitle="Hukuk asistanınız" />
      </div>
      {/* E-posta (ya da korkut-admin) ve parola, gerekirse iki adımlı doğrulama. */}
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
