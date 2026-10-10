import { useEffect, useState } from "react";
import {
  aiApi,
  ApiError,
  type AiModel,
  type AiProvider,
  type AiProviderType,
  type AiRouteSet,
  type AiTestResult,
  type AiUsage,
} from "../api";
import { Button, Field, SelectField } from "../a11y";

// Ayarlar → Yapay zeka (yalnızca ofis sahibi). Korkut'un bütün model çağrıları yerel model
// geçidinden (llmr) geçer: sağlayıcı anahtarları orada şifreli saklanır, burada eklenir ve test
// edilir. Korkut iki adı kullanır: sohbet için "korkut-chat", belge araması için "korkut-embed".
// Bu ekran o adların hangi modele gideceğini seçer.

export const CHAT_ROUTE = "korkut-chat";
export const EMBED_ROUTE = "korkut-embed";

const REACH_OPTIONS = [
  { value: "self-hosted", label: "Bu makinede veya büronun kendi sunucusunda" },
  { value: "private-endpoint", label: "Büroya özel bir bulut uç noktası" },
  { value: "first-party-api", label: "Model sağlayıcısının kendi servisi" },
];

const ACCESS_TEXT: Record<AiTestResult["access"], string> = {
  ready: "Anahtar çalışıyor",
  denied: "Sağlayıcı reddetti",
  unknown: "Doğrulanamadı (bağlantı veya geçici bir sorun)",
};

function message(err: unknown, fallback: string) {
  return err instanceof ApiError ? err.message : fallback;
}

type Choice = { route: string; describe: boolean };

export function AiSettings() {
  const [types, setTypes] = useState<AiProviderType[]>([]);
  const [providers, setProviders] = useState<AiProvider[] | null>(null);
  const [models, setModels] = useState<Record<string, AiModel[]>>({});
  const [routes, setRoutes] = useState<AiRouteSet[]>([]);
  const [usage, setUsage] = useState<AiUsage | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Bumped after any change; reloads everything from the gateway.
  const [version, setVersion] = useState(0);
  const reload = () => setVersion((v) => v + 1);

  useEffect(() => {
    let current = true;
    (async () => {
      try {
        const [t, p, r] = await Promise.all([aiApi.providerTypes(), aiApi.providers(), aiApi.routes()]);
        const usable = p.filter((x) => x.enabled && !x.problem);
        const lists = await Promise.all(
          usable.map((x) => aiApi.models(x.id).then((m) => [x.id, m.data] as const).catch(() => [x.id, []] as const)),
        );
        if (!current) return;
        setTypes(t);
        setProviders(p);
        setRoutes(r);
        setModels(Object.fromEntries(lists));
        setLoadError(null);
      } catch (err) {
        if (current) setLoadError(message(err, "Model geçidine ulaşılamadı."));
      }
      try {
        const u = await aiApi.usage(Math.floor(Date.now() / 1000) - 30 * 24 * 3600);
        if (current) setUsage(u);
      } catch {
        if (current) setUsage(null);
      }
    })();
    return () => {
      current = false;
    };
  }, [version]);

  if (loadError) {
    return (
      <p role="alert" className="text-[13px] text-red-700">
        {loadError}
      </p>
    );
  }
  if (!providers) return <p className="text-[13px] text-zinc-400">Yükleniyor…</p>;

  return (
    <div className="flex flex-col gap-8">
      <p className="text-[13px] text-zinc-500">
        Korkut'un bütün yapay zeka çağrıları bu bilgisayarda çalışan model geçidinden geçer.
        Sağlayıcı anahtarları orada şifreli saklanır ve tarayıcıya geri gönderilmez.
      </p>
      <Providers types={types} providers={providers} onChanged={reload} />
      <ModelChoice providers={providers} models={models} routes={routes} onChanged={reload} />
      <Usage usage={usage} />
    </div>
  );
}

function Providers({
  types,
  providers,
  onChanged,
}: {
  types: AiProviderType[];
  providers: AiProvider[];
  onChanged: () => void;
}) {
  const [tests, setTests] = useState<Record<string, string>>({});
  const [adding, setAdding] = useState(providers.length === 0);

  async function test(id: string) {
    setTests((t) => ({ ...t, [id]: "Test ediliyor…" }));
    try {
      const r = await aiApi.testProvider(id);
      setTests((t) => ({ ...t, [id]: ACCESS_TEXT[r.access] + (r.detail ? `: ${r.detail}` : "") }));
    } catch (err) {
      setTests((t) => ({ ...t, [id]: message(err, "Test yapılamadı") }));
    }
  }

  async function remove(id: string) {
    try {
      await aiApi.removeProvider(id);
      onChanged();
    } catch (err) {
      setTests((t) => ({ ...t, [id]: message(err, "Kaldırılamadı") }));
    }
  }

  return (
    <section aria-labelledby="ai-providers">
      <h2 id="ai-providers" className="mb-3 text-[15px] font-semibold text-zinc-900">
        Sağlayıcılar
      </h2>
      {providers.length === 0 && (
        <p className="mb-3 text-[13px] text-zinc-500">
          Henüz bir sağlayıcı yok. Sohbetin çalışması için en az bir sağlayıcı ekleyin.
        </p>
      )}
      <ul className="flex flex-col gap-2">
        {providers.map((p) => (
          <li key={p.id} className="rounded-lg border border-zinc-200 p-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="text-[13px] font-medium text-zinc-800">
                {p.id}
                <span className="ml-2 font-normal text-zinc-400">
                  {p.type}
                  {p.credential ? ` · anahtar ${p.credential}` : ""}
                </span>
              </span>
              <span className="flex gap-2">
                <Button variant="secondary" onClick={() => test(p.id)}>
                  Test et
                </Button>
                <Button variant="secondary" onClick={() => remove(p.id)}>
                  Kaldır
                </Button>
              </span>
            </div>
            {p.problem && <p className="mt-2 text-[12px] text-amber-700">{p.problem}</p>}
            {tests[p.id] && (
              <p className="mt-2 text-[12px] text-zinc-600" role="status">
                {tests[p.id]}
              </p>
            )}
          </li>
        ))}
      </ul>
      {adding ? (
        <AddProvider
          types={types}
          taken={providers.map((p) => p.id)}
          onAdded={() => {
            setAdding(false);
            onChanged();
          }}
        />
      ) : (
        <Button variant="secondary" className="mt-3" onClick={() => setAdding(true)}>
          Sağlayıcı ekle
        </Button>
      )}
    </section>
  );
}

function AddProvider({
  types,
  taken,
  onAdded,
}: {
  types: AiProviderType[];
  taken: string[];
  onAdded: () => void;
}) {
  const [typeId, setTypeId] = useState("");
  const [credential, setCredential] = useState("");
  const [baseUrl, setBaseUrl] = useState("");
  const [reach, setReach] = useState("self-hosted");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const type = types.find((t) => t.id === typeId);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!type) return;
    setError(null);
    setBusy(true);
    // The provider's name in Korkut is its type, numbered if that one is already in use.
    let id = type.id;
    for (let n = 2; taken.includes(id); n++) id = `${type.id}-${n}`;
    try {
      await aiApi.addProvider({
        id,
        type: type.id,
        ...(credential.trim() ? { credential: credential.trim() } : {}),
        ...(baseUrl.trim() ? { base_url: baseUrl.trim() } : {}),
        ...(type.reach_required ? { reach } : {}),
      });
      onAdded();
    } catch (err) {
      setError(message(err, "Sağlayıcı eklenemedi"));
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} className="mt-3 flex flex-col gap-3 rounded-lg border border-zinc-200 p-3" aria-label="Sağlayıcı ekle">
      <SelectField
        label="Sağlayıcı"
        value={typeId}
        onChange={(e) => setTypeId(e.target.value)}
        options={types.filter((t) => t.kinds.includes("chat")).map((t) => ({ value: t.id, label: t.name }))}
        required
      />
      {type && type.credential !== "none" && (
        <Field
          label="API anahtarı"
          type="password"
          autoComplete="off"
          value={credential}
          onChange={(e) => setCredential(e.target.value)}
          required={type.credential === "required"}
        />
      )}
      {type && (!type.default_base_url || type.reach_required) && (
        <Field
          label="Adres (base URL)"
          type="url"
          placeholder="http://host.docker.internal:11434/v1"
          value={baseUrl}
          onChange={(e) => setBaseUrl(e.target.value)}
          required={!type.default_base_url}
        />
      )}
      {type?.reach_required && (
        <SelectField
          label="Veri nereye gidiyor?"
          value={reach}
          onChange={(e) => setReach(e.target.value)}
          options={REACH_OPTIONS}
          hint="Yerel modeller için 'Bu makinede' seçin; bu bilgi verinin büro dışına çıkıp çıkmadığını belirler."
          required
        />
      )}
      {error && (
        <p role="alert" className="text-[12px] text-red-700">
          {error}
        </p>
      )}
      <Button type="submit" disabled={busy || !type}>
        {busy ? "Ekleniyor…" : "Ekle"}
      </Button>
    </form>
  );
}

function ModelChoice({
  providers,
  models,
  routes,
  onChanged,
}: {
  providers: AiProvider[];
  models: Record<string, AiModel[]>;
  routes: AiRouteSet[];
  onChanged: () => void;
}) {
  const current = (name: string) => routes.find((r) => r.name === name)?.routes[0] ?? "";
  const [chat, setChat] = useState(current(CHAT_ROUTE));
  const [embed, setEmbed] = useState(current(EMBED_ROUTE));
  const [status, setStatus] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const choices: Record<string, Choice> = {};
  for (const p of providers) {
    for (const m of models[p.id] ?? []) {
      choices[m.route] = { route: m.route, describe: !m.capabilities_source };
    }
  }
  const options = Object.keys(choices)
    .sort()
    .map((r) => ({ value: r, label: r }));

  async function apply(route: string, kind: "chat" | "embedding", name: string) {
    const slash = route.indexOf("/");
    await aiApi.enableModel(route.slice(0, slash), route.slice(slash + 1), kind, choices[route]?.describe ?? true);
    await aiApi.setRoute(name, [route]);
  }

  async function save(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setStatus(null);
    try {
      if (chat && chat !== current(CHAT_ROUTE)) await apply(chat, "chat", CHAT_ROUTE);
      if (embed && embed !== current(EMBED_ROUTE)) await apply(embed, "embedding", EMBED_ROUTE);
      setStatus("Kaydedildi.");
      onChanged();
    } catch (err) {
      setStatus(message(err, "Kaydedilemedi"));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section aria-labelledby="ai-models">
      <h2 id="ai-models" className="mb-3 text-[15px] font-semibold text-zinc-900">
        Modeller
      </h2>
      {options.length === 0 ? (
        <p className="text-[13px] text-zinc-500">Modelleri seçmek için önce çalışan bir sağlayıcı ekleyin.</p>
      ) : (
        <form onSubmit={save} className="flex flex-col gap-3" aria-label="Model seçimi">
          <SelectField
            label="Sohbet modeli"
            value={chat}
            onChange={(e) => setChat(e.target.value)}
            options={options}
            hint="Sohbet, dilekçe taslakları ve araç çağrıları bu modelle yapılır."
            required
          />
          <SelectField
            label="Belge arama modeli (embedding, isteğe bağlı)"
            value={embed}
            onChange={(e) => setEmbed(e.target.value)}
            options={options}
            hint="Yüklenen belgelerin aranabilmesi için bir embedding modeli seçin. Sonradan değiştirirseniz belgelerin yeniden indekslenmesi gerekir."
          />
          {status && (
            <p role="status" className="text-[12px] text-zinc-600">
              {status}
            </p>
          )}
          <Button type="submit" disabled={busy || !chat}>
            {busy ? "Kaydediliyor…" : "Kaydet"}
          </Button>
        </form>
      )}
    </section>
  );
}

function Usage({ usage }: { usage: AiUsage | null }) {
  const cost = (c: { currency: string; amount: string }[]) =>
    c.length === 0 ? "Yok" : c.map((x) => `${Number(x.amount).toFixed(2)} ${x.currency}`).join(" + ");
  return (
    <section aria-labelledby="ai-usage">
      <h2 id="ai-usage" className="mb-3 text-[15px] font-semibold text-zinc-900">
        Son 30 gün
      </h2>
      {!usage || usage.data.length === 0 ? (
        <p className="text-[13px] text-zinc-500">Henüz kullanım yok.</p>
      ) : (
        <>
          <table className="w-full text-left text-[13px]">
            <thead className="text-zinc-500">
              <tr>
                <th className="py-1 font-medium">Model</th>
                <th className="py-1 font-medium">İstek</th>
                <th className="py-1 font-medium">Token</th>
                <th className="py-1 font-medium">Maliyet</th>
              </tr>
            </thead>
            <tbody>
              {usage.data.map((row) => (
                <tr key={row.model ?? "none"} className="border-t border-zinc-100">
                  <td className="py-1">{row.model ?? "Yanıtlanamayan istekler"}</td>
                  <td className="py-1">{row.requests}</td>
                  <td className="py-1">{row.tokens.total.toLocaleString("tr-TR")}</td>
                  <td className="py-1">{cost(row.cost)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {!usage.cost_complete && (
            <p className="mt-2 text-[12px] text-zinc-500">
              Bazı istekler fiyatlandırılamadı (örneğin fiyatı bilinmeyen bir model); gerçek tutar
              daha yüksek olabilir.
            </p>
          )}
        </>
      )}
    </section>
  );
}
