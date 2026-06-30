import { useEffect, useState } from "react";
import { api, ApiError, type IntegrationChannel, type IntegrationKind } from "../api";

// Ayarlar → Entegrasyonlar. Two-way chat over Telegram / WhatsApp / e-mail (korkut.integrations).
// Every member links THEIR OWN account to a channel with a one-time code; the channel's connector
// config (bot token / sidecar secret) is owner-only. See docs/INTEGRATIONS.md.

const META: Record<
  IntegrationKind,
  { label: string; blurb: string; linkVerb: string }
> = {
  telegram: {
    label: "Telegram",
    blurb: "BotFather'dan bir bot açın, token'ı aşağıya girin. Bot ayrı bir kimliktir; numara gerekmez.",
    linkVerb: "Telegram'da botunuza şu mesajı gönderin:",
  },
  whatsapp: {
    label: "WhatsApp",
    blurb:
      "Node/Baileys sidecar'ı çalıştırıp QR ile eşleştirin; paylaşılan anahtarı aşağıya girin. Eşleşen numara botun kendisidir.",
    linkVerb: "Bot numarasına WhatsApp'tan şu mesajı yazın:",
  },
  email: {
    label: "E-posta",
    blurb:
      "Gelen mailleri yanıtlar. Ofisin Gmail ayarlarını (adres + uygulama şifresi) “E-posta” sekmesinden açın; aynı bilgiler IMAP için de kullanılır.",
    linkVerb: "Ofis adresine, konuya veya gövdeye şunu yazıp e-posta atın:",
  },
};

const card = "rounded-xl border border-zinc-200 p-4";
const input =
  "w-full rounded-lg border border-zinc-200 px-3 py-2 text-sm outline-none focus:border-zinc-400 disabled:bg-zinc-50 disabled:text-zinc-400";
const btn =
  "rounded-lg bg-zinc-900 px-4 py-1.5 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-40";

function Badge({ on, label }: { on: boolean; label: string }) {
  return (
    <span
      className={
        "rounded-full px-2 py-0.5 text-[11px] font-medium " +
        (on ? "bg-emerald-50 text-emerald-700" : "bg-zinc-100 text-zinc-400")
      }
    >
      {label}
    </span>
  );
}

function LinkBox({ ch, onChange }: { ch: IntegrationChannel; onChange: () => void }) {
  const [code, setCode] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function link() {
    setBusy(true);
    try {
      const r = await api.createIntegrationLink(ch.kind);
      setCode(r.code);
    } finally {
      setBusy(false);
    }
  }

  async function unlink() {
    if (!confirm("Bu kanaldaki bağlantınız kaldırılsın mı?")) return;
    setBusy(true);
    try {
      await api.deleteIntegrationLink(ch.kind);
      setCode(null);
      onChange();
    } finally {
      setBusy(false);
    }
  }

  if (ch.linked) {
    return (
      <div className="mt-3 flex items-center gap-3 rounded-lg bg-emerald-50 px-3 py-2 text-[13px] text-emerald-800">
        <span className="flex-1">
          Hesabınız bağlı{ch.linked_id && <span className="text-emerald-600"> · {ch.linked_id}</span>}
        </span>
        <button onClick={unlink} disabled={busy} className="text-[12px] text-red-600 hover:underline disabled:opacity-40">
          Bağlantıyı kaldır
        </button>
      </div>
    );
  }

  return (
    <div className="mt-3">
      {code ? (
        <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-3">
          <p className="m-0 text-[13px] text-zinc-600">{META[ch.kind].linkVerb}</p>
          <code className="mt-1 block select-all rounded-md bg-white px-3 py-2 text-sm font-bold tracking-wider text-zinc-900">
            BAGLAN {code}
          </code>
          <div className="mt-2 flex items-center gap-3">
            <span className="text-[12px] text-zinc-400">Kod 15 dakika geçerli.</span>
            <button onClick={onChange} className="ml-auto text-[12px] text-zinc-700 hover:underline">
              Bağladım, yenile
            </button>
          </div>
        </div>
      ) : (
        <button onClick={link} disabled={busy} className={btn}>
          {busy ? "…" : "Hesabımı bağla"}
        </button>
      )}
    </div>
  );
}

function TelegramConfig({ ch, onSaved }: { ch: IntegrationChannel; onSaved: () => void }) {
  const [enabled, setEnabled] = useState(ch.enabled);
  const [token, setToken] = useState("");
  const [touched, setTouched] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    setBusy(true);
    setError(null);
    try {
      await api.saveTelegramChannel({ enabled, bot_token: touched ? token : null });
      setToken("");
      setTouched(false);
      onSaved();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Kaydedilemedi.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mt-3 border-t border-zinc-100 pt-3">
      <label className="mb-2 flex items-center gap-2 text-sm text-zinc-700">
        <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} className="h-4 w-4 accent-zinc-900" />
        Telegram kanalını aç
      </label>
      <label className="mb-1 block text-xs font-medium text-zinc-500">Bot token (BotFather)</label>
      <input
        type="password"
        className={input}
        value={token}
        autoComplete="new-password"
        placeholder={ch.configured ? "•••••••• (kayıtlı — değiştirmek için yazın)" : "123456:ABC-..."}
        onChange={(e) => {
          setToken(e.target.value);
          setTouched(true);
        }}
      />
      {error && <div className="mt-2 text-xs text-red-600">{error}</div>}
      <button onClick={save} disabled={busy} className={btn + " mt-3"}>
        {busy ? "…" : "Kaydet"}
      </button>
    </div>
  );
}

function WhatsAppConfig({ ch, onSaved }: { ch: IntegrationChannel; onSaved: () => void }) {
  const [enabled, setEnabled] = useState(ch.enabled);
  const [baseUrl, setBaseUrl] = useState(ch.base_url || "http://127.0.0.1:8099");
  const [token, setToken] = useState("");
  const [touched, setTouched] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    setBusy(true);
    setError(null);
    try {
      await api.saveWhatsAppChannel({ enabled, base_url: baseUrl, token: touched ? token : null });
      setToken("");
      setTouched(false);
      onSaved();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Kaydedilemedi.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mt-3 border-t border-zinc-100 pt-3">
      <label className="mb-2 flex items-center gap-2 text-sm text-zinc-700">
        <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} className="h-4 w-4 accent-zinc-900" />
        WhatsApp kanalını aç
      </label>
      <label className="mb-1 block text-xs font-medium text-zinc-500">Sidecar adresi</label>
      <input className={input} value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} placeholder="http://127.0.0.1:8099" />
      <label className="mb-1 mt-3 block text-xs font-medium text-zinc-500">Paylaşılan anahtar (KORKUT_WA_TOKEN)</label>
      <input
        type="password"
        className={input}
        value={token}
        autoComplete="new-password"
        placeholder={ch.configured ? "•••••••• (kayıtlı — değiştirmek için yazın)" : "sidecar ile aynı değer"}
        onChange={(e) => {
          setToken(e.target.value);
          setTouched(true);
        }}
      />
      {error && <div className="mt-2 text-xs text-red-600">{error}</div>}
      <button onClick={save} disabled={busy} className={btn + " mt-3"}>
        {busy ? "…" : "Kaydet"}
      </button>
    </div>
  );
}

export function IntegrationsSettings() {
  const [channels, setChannels] = useState<IntegrationChannel[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  function reload() {
    api
      .listIntegrations()
      .then((r) => setChannels(r.channels))
      .catch(() => setError("Entegrasyonlar yüklenemedi."));
  }
  useEffect(reload, []);

  if (error) return <div className="text-xs text-red-600">{error}</div>;
  if (!channels) return <p className="text-[13px] text-zinc-400">Yükleniyor…</p>;

  return (
    <div className="flex flex-col gap-4">
      <p className="m-0 text-[13px] text-zinc-500">
        Korkut'la Telegram, WhatsApp veya e-postadan konuşun. Her avukat kendi hesabını tek seferlik
        bir kodla bağlar; kanalı ofis sahibi yapılandırır.
      </p>

      {channels.map((ch) => {
        const meta = META[ch.kind];
        return (
          <div key={ch.kind} className={card}>
            <div className="flex items-center gap-2">
              <span className="text-sm font-semibold text-zinc-900">{meta.label}</span>
              <Badge on={ch.enabled} label={ch.enabled ? "Açık" : "Kapalı"} />
              <Badge on={ch.configured} label={ch.configured ? "Yapılandırıldı" : "Eksik"} />
              {ch.linked && <Badge on label="Bağlı" />}
            </div>
            <p className="mt-1 text-[12px] text-zinc-400">{meta.blurb}</p>

            {ch.configured || ch.kind === "email" ? (
              <LinkBox ch={ch} onChange={reload} />
            ) : (
              <p className="mt-3 text-[12px] text-amber-600">
                Bağlanmadan önce ofis sahibinin bu kanalı yapılandırması gerekiyor.
              </p>
            )}

            {ch.can_edit && ch.kind === "telegram" && <TelegramConfig ch={ch} onSaved={reload} />}
            {ch.can_edit && ch.kind === "whatsapp" && <WhatsAppConfig ch={ch} onSaved={reload} />}
            {ch.can_edit && ch.kind === "email" && (
              <p className="mt-3 border-t border-zinc-100 pt-3 text-[12px] text-zinc-400">
                E-posta kanalının yapılandırması <span className="font-medium text-zinc-600">“E-posta”</span>{" "}
                sekmesinde. Orada açık olduğunda gelen maillere de yanıt verir.
              </p>
            )}
          </div>
        );
      })}
    </div>
  );
}
