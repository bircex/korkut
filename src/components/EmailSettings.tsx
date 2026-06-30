import { useEffect, useState } from "react";
import { api, ApiError, type EmailChannel } from "../api";

// Office e-mail channel (Ayarlar → E-posta, owner-only). Send-only Gmail over SMTP with an
// App Password — no OAuth, no Google Cloud project. Mail leaves from the office's own account
// (a copy lands in Gmail "Gönderilenler"). The App Password is write-only — the server never
// returns it. Korkut never sends on its own: it drafts, the lawyer confirms.

export function EmailSettings() {
  const [data, setData] = useState<EmailChannel | null>(null);
  const [enabled, setEnabled] = useState(false);
  const [fromAddress, setFromAddress] = useState("");
  const [fromName, setFromName] = useState("");
  const [appPassword, setAppPassword] = useState("");
  const [pwTouched, setPwTouched] = useState(false);
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function hydrate(d: EmailChannel) {
    setData(d);
    setEnabled(d.enabled);
    setFromAddress(d.from_address);
    setFromName(d.from_name);
    setAppPassword("");
    setPwTouched(false);
    setSaved(true);
  }

  useEffect(() => {
    api
      .getEmailChannel()
      .then(hydrate)
      .catch(() => setError("E-posta ayarları yüklenemedi."));
  }, []);

  const canEdit = data?.can_edit ?? false;
  const dirty = () => setSaved(false);

  async function save() {
    setBusy(true);
    setError(null);
    try {
      hydrate(
        await api.saveEmailChannel({
          enabled,
          from_address: fromAddress,
          from_name: fromName,
          app_password: pwTouched ? appPassword : null,
        }),
      );
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Kaydedilemedi.");
    } finally {
      setBusy(false);
    }
  }

  const input =
    "w-full rounded-lg border border-zinc-200 px-3 py-2 text-sm outline-none focus:border-zinc-400 disabled:bg-zinc-50 disabled:text-zinc-400";

  return (
    <div>
      {!canEdit && (
        <div className="mb-4 rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-700">
          E-posta ayarlarını yalnızca ofis sahibi değiştirebilir.
        </div>
      )}

      <label className="flex items-center gap-3 rounded-xl border border-zinc-200 px-4 py-3">
        <input
          type="checkbox"
          checked={enabled}
          disabled={!canEdit}
          onChange={(e) => {
            setEnabled(e.target.checked);
            dirty();
          }}
          className="h-4 w-4 accent-zinc-900"
        />
        <div>
          <div className="text-sm font-medium text-zinc-900">E-posta gönderimini aç</div>
          <div className="text-xs text-zinc-400">
            Korkut e-postaları kendiliğinden göndermez — taslağı hazırlar, siz onaylayıp
            gönderirsiniz. Posta ofisin kendi Gmail hesabından çıkar.
          </div>
        </div>
      </label>

      <fieldset
        disabled={!canEdit || !enabled}
        className="mt-4 transition-opacity disabled:opacity-50"
      >
        <div>
          <label className="mb-1 block text-xs font-medium text-zinc-500">
            Gönderen adresi (Gmail)
          </label>
          <input
            className={input}
            value={fromAddress}
            placeholder="ofis@gmail.com"
            autoComplete="off"
            onChange={(e) => {
              setFromAddress(e.target.value);
              dirty();
            }}
          />
        </div>

        <div className="mt-3">
          <label className="mb-1 block text-xs font-medium text-zinc-500">
            Görünen ad (isteğe bağlı)
          </label>
          <input
            className={input}
            value={fromName}
            placeholder="Örnek Hukuk Bürosu"
            onChange={(e) => {
              setFromName(e.target.value);
              dirty();
            }}
          />
        </div>

        <div className="mt-3">
          <label className="mb-1 block text-xs font-medium text-zinc-500">
            Uygulama şifresi (App Password)
          </label>
          <input
            type="password"
            className={input}
            value={appPassword}
            autoComplete="new-password"
            placeholder={
              data?.hasPassword ? "•••••••• (kayıtlı — değiştirmek için yazın)" : "16 haneli şifre"
            }
            onChange={(e) => {
              setAppPassword(e.target.value);
              setPwTouched(true);
              dirty();
            }}
          />
          <p className="mt-1 text-[11px] text-zinc-400">
            Google Hesabı → Güvenlik → 2 Adımlı Doğrulama → Uygulama Şifreleri'nden alınır. Normal
            Gmail şifreniz değil.
          </p>
        </div>
      </fieldset>

      {error && <div className="mt-4 text-xs text-red-600">{error}</div>}

      {canEdit && (
        <div className="mt-5 flex items-center gap-3">
          {saved && <span className="text-xs text-emerald-600">Kaydedildi ✓</span>}
          <button
            onClick={save}
            disabled={busy}
            className="ml-auto rounded-lg bg-zinc-900 px-4 py-1.5 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-40"
          >
            {busy ? "…" : "Kaydet"}
          </button>
        </div>
      )}
    </div>
  );
}
