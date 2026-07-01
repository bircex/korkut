import { useEffect, useState } from "react";
import { api, ApiError, type Member, type StyleFields, type User } from "../api";
import { EmailSettings } from "./EmailSettings";
import { IntegrationsSettings } from "./IntegrationsSettings";
import { MemorySettings } from "./MemorySettings";
import { ModelSettings } from "./ModelSettings";
import { PersonaSettings } from "./PersonaSettings";

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-zinc-100 py-2 last:border-0">
      <span className="text-zinc-500">{label}</span>
      <span className="font-medium text-zinc-900">{value}</span>
    </div>
  );
}

// A lawyer's own bar-registry number (baro sicil no). Editable inline; feeds the redactor so the
// number is masked in cloud-bound prompts.
function BarNoField({ initial }: { initial: string | null }) {
  const [value, setValue] = useState(initial ?? "");
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);

  async function save() {
    setBusy(true);
    try {
      await api.updateMe({ bar_registry_no: value.trim() || null });
      setSaved(true);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mt-4 flex items-end gap-2">
      <label className="flex flex-1 flex-col gap-1">
        <span className="text-[13px] text-zinc-500">Baro sicil no</span>
        <input
          value={value}
          onChange={(e) => {
            setValue(e.target.value);
            setSaved(false);
          }}
          placeholder="—"
          className="rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900"
        />
      </label>
      <button
        onClick={save}
        disabled={busy}
        className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
      >
        {busy ? "Kaydediliyor…" : "Kaydet"}
      </button>
      {saved && <span className="pb-2 text-[12px] text-zinc-400">Kaydedildi.</span>}
    </div>
  );
}

function Members({ isOwner }: { isOwner: boolean }) {
  const [members, setMembers] = useState<Member[]>([]);
  const [username, setUsername] = useState("");
  const [fullName, setFullName] = useState("");
  const [password, setPassword] = useState("");
  const [barNo, setBarNo] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.members().then(setMembers).catch(() => {});
  }, []);

  async function add(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      const m = await api.addMember(
        username.trim(),
        fullName.trim(),
        password,
        barNo.trim() || undefined,
      );
      setMembers((list) => [...list, m]);
      setUsername("");
      setFullName("");
      setPassword("");
      setBarNo("");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Üye eklenemedi");
    } finally {
      setBusy(false);
    }
  }

  async function remove(m: Member) {
    if (!confirm(`${m.full_name ?? m.username} ofisten çıkarılsın mı?`)) return;
    try {
      await api.removeMember(m.id);
      setMembers((list) => list.filter((x) => x.id !== m.id));
    } catch {
      /* ignore */
    }
  }

  async function toggle2fa(m: Member) {
    const turnOn = !(m.mfa_required || m.enrolled);
    if (!turnOn && !confirm(`${m.full_name ?? m.username} için 2FA kapatılsın mı?`)) return;
    try {
      const updated = await api.setMember2fa(m.id, turnOn);
      setMembers((list) => list.map((x) => (x.id === m.id ? updated : x)));
    } catch {
      /* ignore */
    }
  }

  function twoFaLabel(m: Member): string {
    if (m.enrolled) return "2FA açık";
    if (m.mfa_required) return "2FA zorunlu · kurulum bekliyor";
    return "2FA kapalı";
  }

  return (
    <div>
      <div className="mb-3 divide-y divide-zinc-100 rounded-lg border border-zinc-200">
        {members.map((m) => (
          <div key={m.id} className="flex items-center gap-2 px-3 py-2 text-sm">
            <span className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-zinc-900 text-[11px] font-bold text-white">
              {(m.full_name ?? m.username).slice(0, 1).toUpperCase()}
            </span>
            <span className="min-w-0 flex-1">
              <span className="block truncate text-zinc-800">
                {m.full_name ?? m.username}
                {m.is_self && <span className="text-zinc-400"> (siz)</span>}
              </span>
              <span className="block truncate text-[12px] text-zinc-400">
                @{m.username} · {m.role === "owner" ? "Sahip" : "Üye"}
                {!m.is_active ? " · pasif" : ` · ${twoFaLabel(m)}`}
              </span>
            </span>
            {isOwner && m.is_active && (
              <button
                onClick={() => toggle2fa(m)}
                className="shrink-0 rounded-md px-2 py-1 text-[12px] text-zinc-600 hover:bg-zinc-100"
              >
                {m.mfa_required || m.enrolled ? "2FA'yı kapat" : "2FA iste"}
              </button>
            )}
            {isOwner && !m.is_self && m.is_active && (
              <button
                onClick={() => remove(m)}
                className="shrink-0 rounded-md px-2 py-1 text-[12px] text-red-600 hover:bg-red-50"
              >
                Çıkar
              </button>
            )}
          </div>
        ))}
      </div>

      {isOwner ? (
        <form onSubmit={add} className="flex flex-col gap-2 rounded-lg border border-zinc-200 bg-zinc-50 p-3">
          <p className="m-0 text-[13px] font-medium text-zinc-700">Yeni üye ekle</p>
          <input
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            placeholder="Ad Soyad"
            className="rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900"
          />
          <input
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            placeholder="Kullanıcı adı"
            autoComplete="off"
            className="rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900"
          />
          <input
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            type="password"
            placeholder="Geçici parola (en az 8 karakter)"
            autoComplete="new-password"
            className="rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900"
          />
          <input
            value={barNo}
            onChange={(e) => setBarNo(e.target.value)}
            placeholder="Baro sicil no (opsiyonel)"
            className="rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900"
          />
          <button
            type="submit"
            disabled={busy || !fullName.trim() || !username.trim() || password.length < 8}
            className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
          >
            {busy ? "Ekleniyor…" : "Üye ekle"}
          </button>
          {error && <p className="m-0 text-[13px] text-red-600">{error}</p>}
          <p className="m-0 text-[12px] text-zinc-400">
            Üye, verdiğiniz kullanıcı adı + geçici parola ile giriş yapar. 2FA varsayılan kapalıdır;
            yukarıdaki listeden bir üye için “2FA iste” diyerek zorunlu kılabilirsiniz.
          </p>
        </form>
      ) : (
        <p className="m-0 text-[13px] text-zinc-400">Üye eklemek/çıkarmak yalnızca ofis sahibine açık.</p>
      )}
    </div>
  );
}

function StyleArea({
  label,
  hint,
  value,
  onChange,
  rows = 3,
  disabled,
}: {
  label: string;
  hint: string;
  value: string;
  onChange: (v: string) => void;
  rows?: number;
  disabled: boolean;
}) {
  return (
    <label className="flex flex-col gap-1">
      <span className="text-[13px] font-medium text-zinc-700">{label}</span>
      <textarea
        value={value}
        rows={rows}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        className="resize-y rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 outline-none focus:border-zinc-900 disabled:bg-zinc-50 disabled:text-zinc-500"
      />
      <span className="text-[12px] text-zinc-400">{hint}</span>
    </label>
  );
}

function StyleForm({
  title,
  subtitle,
  initial,
  disabled,
  onSave,
}: {
  title: string;
  subtitle: string;
  initial: StyleFields;
  disabled: boolean;
  onSave: (fields: StyleFields) => Promise<void>;
}) {
  const [fields, setFields] = useState<StyleFields>(initial);
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);

  const set = (k: keyof StyleFields) => (v: string) => {
    setFields((f) => ({ ...f, [k]: v }));
    setSaved(false);
  };

  async function save() {
    setBusy(true);
    try {
      await onSave(fields);
      setSaved(true);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mb-4 rounded-lg border border-zinc-200 p-3">
      <div className="mb-2">
        <p className="m-0 text-sm font-semibold text-zinc-800">{title}</p>
        <p className="m-0 text-[12px] text-zinc-400">{subtitle}</p>
      </div>
      <div className="flex flex-col gap-3">
        <StyleArea
          label="Antet"
          hint="Büro/avukat kimliği — belgenin en üstüne aynen eklenir (modele gönderilmez)."
          value={fields.letterhead}
          onChange={set("letterhead")}
          disabled={disabled}
        />
        <StyleArea
          label="İmza bloğu"
          hint="Dilekçelerin sonuna eklenir (sözleşmelerde taraf imzaları olduğundan eklenmez)."
          value={fields.signature}
          onChange={set("signature")}
          disabled={disabled}
        />
        <StyleArea
          label="Üslup / ton"
          hint='Serbest yazım talimatı, ör. "Kısa ve öz yaz; resmî dil kullan".'
          value={fields.tone}
          onChange={set("tone")}
          disabled={disabled}
        />
        <StyleArea
          label="Standart kalıplar"
          hint="Sık kullandığın SONUÇ VE İSTEM / vekâlet ücreti / sözleşme maddesi metinleri."
          value={fields.boilerplate}
          onChange={set("boilerplate")}
          disabled={disabled}
          rows={4}
        />
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
    </div>
  );
}

function StyleTab() {
  const [data, setData] = useState<{
    office: StyleFields;
    user: StyleFields;
    canEditOffice: boolean;
  } | null>(null);

  useEffect(() => {
    api
      .getStyle()
      .then((d) => setData({ office: d.office, user: d.user, canEditOffice: d.can_edit_office }))
      .catch(() => {});
  }, []);

  if (!data) return <p className="text-[13px] text-zinc-400">Yükleniyor…</p>;

  return (
    <div>
      <StyleForm
        key="user"
        title="Kişisel stilim"
        subtitle="Sizin belgelerinize uygulanır; ofis stilinin üstüne yazar."
        initial={data.user}
        disabled={false}
        onSave={(f) => api.saveUserStyle(f).then(() => {})}
      />
      <StyleForm
        key="office"
        title="Ofis stili"
        subtitle={
          data.canEditOffice
            ? "Tüm büro için varsayılan; üyeler kendi alanlarıyla ezebilir."
            : "Büro varsayılanı (salt görünüm — yalnızca ofis sahibi düzenler)."
        }
        initial={data.office}
        disabled={!data.canEditOffice}
        onSave={(f) => api.saveOfficeStyle(f).then(() => {})}
      />
    </div>
  );
}

type Tab =
  | "account"
  | "members"
  | "persona"
  | "memory"
  | "style"
  | "integrations"
  | "model"
  | "email";

const TAB_TITLE: Record<Tab, string> = {
  account: "Hesap",
  members: "Ofis üyeleri",
  persona: "Kişilik",
  memory: "Hafıza",
  style: "Yazım stili",
  integrations: "Entegrasyonlar",
  model: "Model",
  email: "E-posta",
};

export function Settings({ user, onClose }: { user: User; onClose: () => void }) {
  const [tab, setTab] = useState<Tab>("account");
  const tabs: { id: Tab; label: string }[] = [
    { id: "account", label: "Hesap" },
    { id: "members", label: "Üyeler" },
    // Persona is office-wide; everyone sees it, only the owner can edit (gated server-side).
    { id: "persona", label: "Kişilik" },
    { id: "memory", label: "Hafıza" },
    { id: "style", label: "Stil" },
    // Every member can link their own account; channel config inside is owner-gated.
    { id: "integrations", label: "Entegrasyonlar" },
    // Model + e-posta are office-wide settings — owner only.
    ...(user.role === "owner"
      ? [
          { id: "model" as Tab, label: "Model" },
          { id: "email" as Tab, label: "E-posta" },
        ]
      : []),
  ];

  return (
    <div className="flex h-dvh bg-zinc-100 text-zinc-900">
      <aside className="flex w-64 shrink-0 flex-col gap-1 border-r border-zinc-200 bg-white p-4">
        <button
          onClick={onClose}
          className="mb-4 rounded-lg border border-zinc-300 px-3 py-2 text-left text-sm font-medium text-zinc-700 hover:border-zinc-900"
        >
          Geri
        </button>
        <p className="px-3 pb-2 text-[11px] font-semibold uppercase tracking-wide text-zinc-400">
          Ayarlar
        </p>
        {tabs.map((t) => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={
              "rounded-lg px-3 py-2.5 text-left text-[15px] font-medium " +
              (tab === t.id ? "bg-zinc-900 text-white" : "text-zinc-700 hover:bg-zinc-100")
            }
          >
            {t.label}
          </button>
        ))}
      </aside>

      <div className="min-w-0 flex-1 overflow-y-auto">
        <div className="mx-auto w-full max-w-2xl px-6 py-8">
          <h1 className="mb-5 text-lg font-bold text-zinc-900">{TAB_TITLE[tab]}</h1>

          {tab === "account" && (
            <div>
              <div className="mb-4 text-sm">
                <Row label="Hukuk bürosu" value={user.office ?? "—"} />
                <Row label="Rol" value={user.role === "owner" ? "Sahip" : "Üye"} />
                <Row label="Kullanıcı adı" value={user.username} />
                <Row label="Ad Soyad" value={user.full_name ?? "—"} />
              </div>
              <BarNoField initial={user.bar_registry_no} />
              <div className="mt-4" />
              <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-3 text-[13px] text-zinc-600">
                İki adımlı doğrulama (2FA){" "}
                <span className="font-medium text-zinc-800">
                  {user.has_2fa ? "hesabınızda açık" : user.require_2fa ? "zorunlu (girişte kurulacak)" : "kapalı"}
                </span>
                .
              </div>
            </div>
          )}
          {tab === "members" && <Members isOwner={user.role === "owner"} />}
          {tab === "persona" && <PersonaSettings />}
          {tab === "memory" && <MemorySettings />}
          {tab === "style" && <StyleTab />}
          {tab === "integrations" && <IntegrationsSettings />}
          {tab === "model" && user.role === "owner" && <ModelSettings />}
          {tab === "email" && user.role === "owner" && <EmailSettings />}
        </div>
      </div>
    </div>
  );
}
