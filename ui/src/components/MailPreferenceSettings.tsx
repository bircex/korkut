import { useEffect, useState } from "react";
import { api, type MailPreference } from "../api";

// Ayarlar → Bildirimler (owner only). Korkut, tek operatör hesabından e-posta gönderir;
// burada ofisin bildirim e-postasını ve bildirimlerin açık olup olmadığını ayarlarsınız.
// Güvenlik ve davet e-postaları bu ayardan bağımsız olarak her zaman gönderilir.

export function MailPreferenceSettings() {
  const [data, setData] = useState<MailPreference | null>(null);
  const [enabled, setEnabled] = useState(true);
  const [notifyEmail, setNotifyEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    api
      .getMailPreference()
      .then((d) => {
        setData(d);
        setEnabled(d.notifications_enabled);
        setNotifyEmail(d.notify_email ?? "");
      })
      .catch(() => {});
  }, []);

  if (!data) return <p className="text-[13px] text-zinc-400">Yükleniyor…</p>;
  const disabled = !data.can_edit;

  async function save() {
    setBusy(true);
    try {
      const trimmed = notifyEmail.trim();
      const d = await api.saveMailPreference({
        notifications_enabled: enabled,
        notify_email: trimmed === "" ? null : trimmed,
      });
      setData(d);
      setEnabled(d.notifications_enabled);
      setNotifyEmail(d.notify_email ?? "");
      setSaved(true);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <p className="mb-4 text-[13px] text-zinc-500">
        Korkut, e-postaları tek bir sistem hesabından gönderir. Güvenlik ve davet e-postaları her
        zaman gönderilir; buradaki tercih yalnızca bilgilendirme e-postalarını etkiler. Süre, duruşma
        ve görev hatırlatmaları da bu adrese gider.
      </p>
      <div className="rounded-lg border border-zinc-200 p-3">
        <div className="flex flex-col gap-4">
          <label className="flex items-center justify-between gap-4">
            <span className="flex flex-col">
              <span className="text-[13px] font-medium text-zinc-700">Bildirim e-postaları</span>
              <span className="text-[12px] text-zinc-400">
                Kapalıyken bilgilendirme e-postaları gönderilmez.
              </span>
            </span>
            <input
              type="checkbox"
              checked={enabled}
              disabled={disabled}
              onChange={(e) => {
                setEnabled(e.target.checked);
                setSaved(false);
              }}
              className="size-4 accent-zinc-900 disabled:opacity-50"
            />
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-[13px] font-medium text-zinc-700">Bildirim e-posta adresi</span>
            <input
              type="email"
              value={notifyEmail}
              disabled={disabled}
              placeholder="bos birakilirsa ofis sahibinin adresi kullanilir"
              onChange={(e) => {
                setNotifyEmail(e.target.value);
                setSaved(false);
              }}
              className="rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 outline-none focus:border-zinc-900 disabled:bg-zinc-50 disabled:text-zinc-500"
            />
            <span className="text-[12px] text-zinc-400">
              Ofis bildirimlerinin gönderileceği adres.
            </span>
          </label>
        </div>
        {!disabled && (
          <div className="mt-3 flex items-center gap-3">
            <button
              onClick={save}
              disabled={busy}
              className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
            >
              {busy ? "Kaydediliyor…" : "Kaydet"}
            </button>
            {saved && <span className="text-[12px] text-zinc-400">Kaydedildi.</span>}
          </div>
        )}
        {disabled && (
          <p className="mt-3 text-[12px] text-zinc-400">
            Bu ayarları yalnızca büro sahibi düzenleyebilir.
          </p>
        )}
      </div>
    </div>
  );
}
