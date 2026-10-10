import { useEffect, useState } from "react";
import { ApiError, telegramApi, type TelegramLink, type TelegramLinkCode, type TelegramSettings as Settings } from "../api";
import { Button, CheckboxField, Field } from "../a11y";

// Ayarlar → Telegram (yalnızca ofis sahibi). Büro kendi botunu BotFather'dan alır, anahtarını
// buraya girer ve açar. Üyeler hesaplarını Hesap sekmesinden bağlar; bağlı bir üyenin bota
// yazdığı her mesaj, o üyenin Korkut'taki yetkileriyle bir sohbet turu olarak yanıtlanır.

function message(err: unknown, fallback: string) {
  return err instanceof ApiError ? err.message : fallback;
}

const date = (iso: string) => new Date(iso).toLocaleDateString("tr-TR", { day: "numeric", month: "long", year: "numeric" });

export function TelegramSettings() {
  const [data, setData] = useState<Settings | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [token, setToken] = useState("");
  const [consent, setConsent] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    telegramApi
      .settings()
      .then(setData)
      .catch((err) => setLoadError(message(err, "Telegram ayarları yüklenemedi.")));
  }, []);

  if (loadError) {
    return (
      <p role="alert" className="text-[13px] text-red-700">
        {loadError}
      </p>
    );
  }
  if (!data) return <p className="text-[13px] text-zinc-400">Yükleniyor…</p>;

  async function run(work: () => Promise<Settings>) {
    setBusy(true);
    setError(null);
    try {
      setData(await work());
      setToken("");
    } catch (err) {
      setError(message(err, "Kaydedilemedi."));
    } finally {
      setBusy(false);
    }
  }

  const consented = data.consent_at !== null;
  const save = (enabled: boolean) =>
    run(() => telegramApi.saveSettings({ token: token.trim() || undefined, enabled, consent: consented || consent }));

  return (
    <div className="flex flex-col gap-6">
      <p className="text-[13px] text-zinc-500">
        Büro üyeleri Korkut'a Telegram'dan da yazabilir. Bunun için Telegram'da{" "}
        <a href="https://t.me/BotFather" target="_blank" rel="noreferrer" className="font-medium text-zinc-900 underline">
          @BotFather
        </a>{" "}
        ile büroya ait bir bot oluşturun ve verdiği anahtarı aşağıya yapıştırın. Bot, bu bilgisayardan Telegram'a bağlanır;
        dışarıya bir adres açmanız gerekmez.
      </p>

      <section aria-labelledby="telegram-bot" className="rounded-lg border border-zinc-200 p-4">
        <div className="mb-3 flex items-center justify-between gap-3">
          <h2 id="telegram-bot" className="text-[15px] font-semibold text-zinc-900">
            Büro botu
          </h2>
          <span className={"rounded-full px-2 py-0.5 text-[12px] font-medium " + (data.enabled ? "bg-emerald-50 text-emerald-800" : "bg-zinc-100 text-zinc-600")}>
            {data.enabled ? "Açık" : "Kapalı"}
          </span>
        </div>
        {data.problem && (
          <p role="status" className="mb-3 rounded-md bg-amber-50 px-3 py-2 text-[13px] text-amber-900">
            {data.problem}
          </p>
        )}
        {data.bot_username && (
          <p className="mb-3 text-[13px] text-zinc-600">
            Bağlı bot:{" "}
            <a href={`https://t.me/${data.bot_username}`} target="_blank" rel="noreferrer" className="font-medium text-zinc-900 underline">
              @{data.bot_username}
            </a>
          </p>
        )}
        <div className="flex flex-col gap-4">
          <Field
            label={data.configured ? "Yeni bot anahtarı" : "Bot anahtarı"}
            type="password"
            autoComplete="off"
            value={token}
            placeholder="123456789:AA..."
            hint={data.configured ? "Boş bırakırsanız mevcut anahtar kalır. Başka bir bota geçerseniz üyelerin bağlantıları silinir." : undefined}
            onChange={(e) => setToken(e.target.value)}
          />
          {!consented && (
            <CheckboxField
              label="Mesajların Telegram sunucularından geçeceğini biliyorum"
              checked={consent}
              onChange={setConsent}
              hint="Telegram üzerinden yazılan sorular ve Korkut'un yanıtları, müvekkil bilgileri dahil, Telegram'ın yurt dışındaki sunucularından geçer. Büronuzun KVKK yükümlülüklerini buna göre değerlendirin."
            />
          )}
          {consented && <p className="text-[12px] text-zinc-500">Telegram aktarımı {date(data.consent_at!)} tarihinde onaylandı.</p>}
        </div>
        {error && (
          <p role="alert" className="mt-3 text-[13px] text-red-700">
            {error}
          </p>
        )}
        <div className="mt-4 flex flex-wrap items-center gap-3">
          {data.enabled ? (
            <>
              {token.trim() && (
                <Button onClick={() => save(true)} disabled={busy}>
                  Anahtarı değiştir
                </Button>
              )}
              <Button variant="secondary" onClick={() => save(false)} disabled={busy}>
                Botu kapat
              </Button>
            </>
          ) : (
            <Button onClick={() => save(true)} disabled={busy || (!data.configured && !token.trim()) || (!consented && !consent)}>
              {busy ? "Denetleniyor…" : "Botu aç"}
            </Button>
          )}
          {data.configured && (
            <Button variant="secondary" onClick={() => run(telegramApi.removeBot)} disabled={busy}>
              Botu kaldır
            </Button>
          )}
        </div>
      </section>

      <section aria-labelledby="telegram-members">
        <h2 id="telegram-members" className="mb-2 text-[15px] font-semibold text-zinc-900">
          Bağlı üyeler
        </h2>
        {data.members.length === 0 ? (
          <p className="text-[13px] text-zinc-500">
            Henüz bağlanan üye yok. Üyeler Ayarlar → Hesap bölümünden "Telegram'ı bağla" düğmesiyle bağlanır.
          </p>
        ) : (
          <ul className="divide-y divide-zinc-950/5 rounded-lg border border-zinc-200">
            {data.members.map((m) => (
              <li key={m.user_id} className="flex items-center justify-between gap-3 px-4 py-3 text-sm">
                <span className="min-w-0">
                  <span className="block font-medium text-zinc-900">{m.name}</span>
                  <span className="block text-[12px] text-zinc-500">
                    {m.telegram_username ? `@${m.telegram_username} · ` : ""}
                    {date(m.linked_at)}
                  </span>
                </span>
                <Button variant="secondary" className="px-3 py-1.5 text-[13px]" disabled={busy} onClick={() => run(() => telegramApi.unlinkMember(m.user_id))}>
                  Bağlantıyı kaldır
                </Button>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

// Hesap sekmesindeki bölüm: her üye kendi Telegram hesabını bağlar. Kod tek kullanımlıktır ve
// 15 dakika geçerlidir; bağlantı, kodu botun kendisine gönderen düğmeyle tek dokunuşta kurulur.
export function TelegramLinkSection() {
  const [link, setLink] = useState<TelegramLink | null>(null);
  const [code, setCode] = useState<TelegramLinkCode | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    telegramApi.link().then(setLink).catch(() => {});
  }, []);

  // Nothing to offer until the owner turns the bot on, unless this member is still linked.
  if (!link || (!link.available && !link.linked)) return null;

  async function issue() {
    setBusy(true);
    setError(null);
    try {
      setCode(await telegramApi.createCode());
    } catch (err) {
      setError(message(err, "Bağlantı kodu alınamadı."));
    } finally {
      setBusy(false);
    }
  }

  async function unlink() {
    setBusy(true);
    setError(null);
    try {
      setLink(await telegramApi.unlink());
      setCode(null);
    } catch (err) {
      setError(message(err, "Bağlantı kaldırılamadı."));
    } finally {
      setBusy(false);
    }
  }

  async function refresh() {
    const next = await telegramApi.link().catch(() => null);
    if (next) {
      setLink(next);
      if (next.linked) setCode(null);
    }
  }

  return (
    <section aria-labelledby="telegram-link" className="mt-6 rounded-lg border border-zinc-200 p-4">
      <h2 id="telegram-link" className="mb-1 text-[15px] font-semibold text-zinc-900">
        Telegram
      </h2>
      {link.linked ? (
        <>
          <p className="mb-3 text-[13px] text-zinc-600">
            Telegram hesabınız bağlı{link.telegram_username ? ` (@${link.telegram_username})` : ""}.{" "}
            {link.bot_username && (
              <>
                <a href={`https://t.me/${link.bot_username}`} target="_blank" rel="noreferrer" className="font-medium text-zinc-900 underline">
                  @{link.bot_username}
                </a>{" "}
                botuna yazdıklarınız, Korkut'taki yetkilerinizle yanıtlanır.
              </>
            )}
          </p>
          <Button variant="secondary" onClick={unlink} disabled={busy}>
            Bağlantıyı kaldır
          </Button>
        </>
      ) : code ? (
        <>
          <p className="mb-3 text-[13px] text-zinc-600">
            Aşağıdaki düğme Telegram'da büro botunu açar; orada <strong>Başlat</strong>'a basmanız yeterli. Bağlantı 15 dakika
            geçerlidir ve yalnızca bir kez kullanılabilir.
          </p>
          <div className="flex flex-wrap items-center gap-3">
            <a
              href={code.url}
              target="_blank"
              rel="noreferrer"
              className="rounded-[10px] bg-zinc-900 px-4 py-3 text-[15px] font-semibold text-white hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-zinc-900"
            >
              Telegram'da aç
            </a>
            <Button variant="secondary" onClick={refresh}>
              Bağladım
            </Button>
          </div>
          <p className="mt-3 text-[12px] text-zinc-500">
            Telegram bu cihazda yoksa bota şu mesajı gönderin: <code className="rounded bg-zinc-100 px-1">/start {code.code}</code>
          </p>
        </>
      ) : (
        <>
          <p className="mb-3 text-[13px] text-zinc-600">
            Hesabınızı büronun Telegram botuna bağlayın; sorularınızı oradan da sorabilirsiniz.
          </p>
          <Button onClick={issue} disabled={busy}>
            Telegram'ı bağla
          </Button>
        </>
      )}
      {error && (
        <p role="alert" className="mt-3 text-[13px] text-red-700">
          {error}
        </p>
      )}
    </section>
  );
}
