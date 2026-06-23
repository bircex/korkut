import { useState } from "react";
import { api, ApiError, type User } from "../api";
import { AuthShell, Brand, Button, ErrorText, Field } from "../ui";

export function Mfa({ onDone }: { onDone: (user: User) => void }) {
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [recovery, setRecovery] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      const user = await api.verify(code.trim());
      onDone(user);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Doğrulama başarısız");
      setBusy(false);
    }
  }

  return (
    <AuthShell>
      <div className="mb-4">
        <Brand subtitle="İki adımlı doğrulama" />
      </div>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <p className="m-0 text-sm text-zinc-400">
          {recovery
            ? "Kurtarma kodunuzu girin."
            : "Authenticator uygulamanızdaki 6 haneli kodu girin."}
        </p>
        <Field
          label={recovery ? "Kurtarma kodu" : "Doğrulama kodu"}
          inputMode={recovery ? "text" : "numeric"}
          autoFocus
          autoComplete="one-time-code"
          value={code}
          onChange={(e) => setCode(e.target.value)}
        />
        <Button type="submit" disabled={busy || !code} className="mt-2">
          {busy ? "Doğrulanıyor…" : "Doğrula"}
        </Button>
        <ErrorText>{error}</ErrorText>
        <button
          type="button"
          onClick={() => {
            setRecovery((v) => !v);
            setCode("");
            setError("");
          }}
          className="mt-1 text-[13px] text-zinc-400 underline-offset-2 hover:text-zinc-200 hover:underline"
        >
          {recovery ? "Authenticator kodu kullan" : "Kurtarma kodu kullan"}
        </button>
      </form>
    </AuthShell>
  );
}
