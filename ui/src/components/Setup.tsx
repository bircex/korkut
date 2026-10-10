import { useState } from "react";
import { api, ApiError } from "../api";
import { Button, Field } from "../a11y";
import { AuthShell, Brand } from "../ui";
import { STRENGTH_COLORS, STRENGTH_LABELS, passwordStrength } from "../passwordStrength";

// İlk kurulum: yeni bir Korkut kurulumunda henüz büro ve hesap yokken giriş ekranının yerine
// gösterilir. Büroyu ve onu yönetecek ilk hesabı (owner) oluşturur, ardından aynı e-posta ve
// parolayla oturumu açar. Sunucu bunu yalnızca bir kez kabul eder; ikinci deneme 409 alır.
export function Setup({ onDone }: { onDone: () => void }) {
  const [officeName, setOfficeName] = useState("");
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const strength = passwordStrength(password);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (password !== confirm) {
      setError("Parolalar eşleşmiyor");
      return;
    }
    setBusy(true);
    try {
      await api.setup(officeName.trim(), fullName.trim(), email.trim(), password);
      await api.login(email.trim(), password);
      onDone();
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        // Başka biri kurulumu bu arada tamamladı: giriş ekranına dön.
        onDone();
        return;
      }
      setError(err instanceof ApiError ? err.message : "Kurulum tamamlanamadı");
      setBusy(false);
    }
  }

  return (
    <AuthShell>
      <div className="mb-4">
        <Brand subtitle="Kurulum" />
      </div>
      <p className="m-0 mb-1 text-[13px] text-zinc-600">
        Korkut'a hoş geldiniz. Büronuzu ve onu yönetecek ilk hesabı oluşturun; diğer üyeleri daha
        sonra ayarlardan davet edebilirsiniz.
      </p>
      <form onSubmit={submit} className="flex flex-col gap-3" aria-label="İlk kurulum">
        <Field label="Büro adı" value={officeName} onChange={(e) => setOfficeName(e.target.value)} required autoFocus />
        <Field label="Ad Soyad" autoComplete="name" value={fullName} onChange={(e) => setFullName(e.target.value)} required />
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
          error={error ?? undefined}
          required
        />
        <Button type="submit" disabled={busy || password.length < 10}>
          {busy ? "Kuruluyor…" : "Kurulumu tamamla"}
        </Button>
      </form>
    </AuthShell>
  );
}
