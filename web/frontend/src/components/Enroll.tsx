import { useEffect, useState } from "react";
import { api, ApiError, type Provision, type User } from "../api";
import { AuthShell, Brand, Button, ErrorText, Field } from "../ui";

export function Enroll({ onDone }: { onDone: (user: User) => void }) {
  const [prov, setProv] = useState<Provision | null>(null);
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [codes, setCodes] = useState<string[] | null>(null);
  const [user, setUser] = useState<User | null>(null);

  useEffect(() => {
    api
      .provision()
      .then(setProv)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Kurulum başlatılamadı"));
  }, []);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      const result = await api.enroll(code.trim());
      setCodes(result.recovery_codes);
      setUser({ username: result.username, full_name: result.full_name });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Kod doğrulanamadı");
    } finally {
      setBusy(false);
    }
  }

  if (codes && user) {
    return (
      <AuthShell>
        <div className="mb-2">
          <Brand subtitle="Kurtarma kodları" />
        </div>
        <p className="m-0 text-sm text-zinc-400">
          Bu kodları güvenli bir yere kaydedin. Telefonunuzu kaybederseniz giriş için
          gerekir. <strong className="text-zinc-200">Bir daha gösterilmeyecek.</strong>
        </p>
        <div className="my-2 grid grid-cols-2 gap-2 rounded-xl border border-zinc-800 bg-zinc-900 p-4 font-mono text-sm">
          {codes.map((c) => (
            <span key={c}>{c}</span>
          ))}
        </div>
        <Button type="button" onClick={() => onDone(user)}>
          Kaydettim, devam et
        </Button>
      </AuthShell>
    );
  }

  return (
    <AuthShell>
      <div className="mb-2">
        <Brand subtitle="İki adımlı doğrulama kurulumu" />
      </div>
      <p className="m-0 text-sm text-zinc-400">
        Güvenlik için 2FA zorunludur. Authenticator uygulamanızla (Google Authenticator,
        Authy, 1Password…) QR kodu okutun, sonra çıkan 6 haneli kodu girin.
      </p>
      <div className="my-1 flex justify-center">
        {prov ? (
          <div
            className="h-48 w-48 rounded-lg bg-white p-2 [&>svg]:block [&>svg]:h-full [&>svg]:w-full"
            dangerouslySetInnerHTML={{ __html: prov.qr_svg }}
          />
        ) : (
          <div className="grid h-44 w-44 place-items-center text-sm text-zinc-500">
            {error ? "—" : "QR yükleniyor…"}
          </div>
        )}
      </div>
      {prov && (
        <p className="m-0 break-all text-center text-[11px] text-zinc-500">
          Elle giriş anahtarı: <span className="font-mono text-zinc-400">{prov.secret}</span>
        </p>
      )}
      <form onSubmit={submit} className="mt-1 flex flex-col gap-3">
        <Field
          label="Doğrulama kodu"
          inputMode="numeric"
          autoComplete="one-time-code"
          value={code}
          onChange={(e) => setCode(e.target.value)}
        />
        <Button type="submit" disabled={busy || !prov || !code}>
          {busy ? "Doğrulanıyor…" : "Kur ve devam et"}
        </Button>
        <ErrorText>{error}</ErrorText>
      </form>
    </AuthShell>
  );
}
