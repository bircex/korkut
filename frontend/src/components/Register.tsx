import { useState } from "react";
import { api, ApiError, type LoginStep } from "../api";
import { AuthShell, Brand, Button, ErrorText, Field } from "../ui";

export function Register({
  onStep,
  onBack,
}: {
  onStep: (step: LoginStep) => void;
  onBack: () => void;
}) {
  const [officeName, setOfficeName] = useState("");
  const [fullName, setFullName] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      const { step } = await api.register(officeName, fullName, username, password);
      onStep(step);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Kayıt başarısız");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthShell>
      <div className="mb-4">
        <Brand subtitle="Hukuk büronuzu oluşturun" />
      </div>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <Field
          label="Hukuk bürosu adı"
          autoFocus
          value={officeName}
          onChange={(e) => setOfficeName(e.target.value)}
        />
        <Field
          label="Ad Soyad"
          autoComplete="name"
          value={fullName}
          onChange={(e) => setFullName(e.target.value)}
        />
        <Field
          label="Kullanıcı adı"
          autoComplete="username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
        />
        <Field
          label="Parola (en az 8 karakter)"
          type="password"
          autoComplete="new-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        <Button
          type="submit"
          disabled={busy || !officeName || !fullName || !username || password.length < 8}
          className="mt-2"
        >
          {busy ? "Oluşturuluyor…" : "Büroyu oluştur"}
        </Button>
        <ErrorText>{error}</ErrorText>
        <button
          type="button"
          onClick={onBack}
          className="mt-1 text-[13px] text-zinc-500 underline-offset-2 hover:text-zinc-900 hover:underline"
        >
          Zaten hesabınız var mı? Giriş yapın
        </button>
      </form>
    </AuthShell>
  );
}
