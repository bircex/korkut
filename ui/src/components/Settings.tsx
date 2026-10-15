import { useEffect, useState, type ReactNode } from "react";
import { api, ApiError, rolesApi, type Invite, type Member, type Role, type StyleFields, type User } from "../api";
import { navigate, usePath } from "../router";
import { Button, Field } from "../a11y";
import {
  type Icon,
  BookmarkSimple,
  Cpu,
  TelegramLogo,
  ShieldCheck,
  ClockCounterClockwise,
  EnvelopeSimple,
  PencilSimpleLine,
  Sparkle,
  User as UserIcon,
  Users,
} from "../icons";
import { AiSettings } from "./AiSettings";
import { TelegramLinkSection, TelegramSettings } from "./TelegramSettings";
import { RolesSettings } from "./RolesSettings";
import { AuditLog } from "./AuditLog";
import { can, P } from "../permissions";
import { MailPreferenceSettings } from "./MailPreferenceSettings";
import { MemorySettings } from "./MemorySettings";
import { PersonaSettings } from "./PersonaSettings";

function Row({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 py-3 first:pt-0 last:pb-0">
      <dt className="font-medium text-zinc-900">{label}</dt>
      <dd className="text-zinc-500">{value}</dd>
    </div>
  );
}

function RoleBadge({ name }: { name: string }) {
  return (
    <span className="inline-flex items-center rounded-full bg-zinc-100 px-2 py-0.5 text-xs font-medium text-zinc-700">
      {name}
    </span>
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
    <div className="flex items-end gap-2">
      <label className="flex flex-1 flex-col gap-1">
        <span className="text-[13px] text-zinc-500">Baro sicil no</span>
        <input
          name="bar_registry_no"
          value={value}
          onChange={(e) => {
            setValue(e.target.value);
            setSaved(false);
          }}
          placeholder="—"
          className="rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none max-sm:text-base/6 focus:border-zinc-900"
        />
      </label>
      <button
        type="button"
        onClick={save}
        disabled={busy}
        className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-zinc-900 disabled:opacity-50"
      >
        {busy ? "Kaydediliyor…" : "Kaydet"}
      </button>
      {saved && <span className="pb-2 text-[12px] text-zinc-400">Kaydedildi.</span>}
    </div>
  );
}

function Members() {
  const [members, setMembers] = useState<Member[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [inviteRole, setInviteRole] = useState("lawyer");
  const [invites, setInvites] = useState<Invite[]>([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [newLink, setNewLink] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteName, setInviteName] = useState("");

  useEffect(() => {
    api.members().then(setMembers).catch(() => {});
    api.invites().then(setInvites).catch(() => {});
    rolesApi.list().then((r) => setRoles(r.roles)).catch(() => {});
  }, []);

  const roleName = (key: string) => roles.find((r) => r.key === key)?.name ?? key;

  async function changeRole(m: Member, role: string) {
    setError("");
    try {
      await rolesApi.setMemberRole(m.id, role);
      setMembers((list) => list.map((x) => (x.id === m.id ? { ...x, role } : x)));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Rol değiştirilemedi");
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

  async function createInvite() {
    setError("");
    if (!inviteName.trim() || !inviteEmail.trim()) {
      setError("Yeni üye için ad soyad ve e-posta girin");
      return;
    }
    setBusy(true);
    setCopied(false);
    try {
      const inv = await api.createInvite({
        email: inviteEmail.trim(),
        full_name: inviteName.trim(),
        role: inviteRole,
      });
      setInvites((list) => [inv, ...list]);
      setInviteEmail("");
      setInviteName("");
      const link = window.location.origin + inv.path;
      setNewLink(link);
      try {
        await navigator.clipboard.writeText(link);
        setCopied(true);
      } catch {
        /* clipboard blocked — the field is still selectable */
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Davet oluşturulamadı");
    } finally {
      setBusy(false);
    }
  }

  async function revoke(id: number) {
    try {
      await api.revokeInvite(id);
      setInvites((list) => list.filter((x) => x.id !== id));
    } catch {
      /* ignore */
    }
  }

  function memberStatus(m: Member): string {
    if (!m.is_active) return "pasif";
    return m.has_password ? "aktif" : "parola bekliyor";
  }

  const pendingInvites = invites.filter((i) => !i.redeemed);
  const inputCls =
    "rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900";

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
                {m.email} · {memberStatus(m)}
              </span>
            </span>
            {m.is_self ? (
              <span className="shrink-0 text-[12px] text-zinc-500">{roleName(m.role)}</span>
            ) : (
              <select
                aria-label={`${m.full_name ?? m.username} rolü`}
                value={m.role}
                onChange={(e) => changeRole(m, e.target.value)}
                className="shrink-0 rounded-md border border-zinc-300 bg-white px-2 py-1 text-[12px]"
              >
                {roles.map((r) => (
                  <option key={r.key} value={r.key}>
                    {r.name}
                  </option>
                ))}
                {!roles.some((r) => r.key === m.role) && <option value={m.role}>{m.role}</option>}
              </select>
            )}
            {!m.is_self && m.is_active && (
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

      {error && !newLink && <p className="mb-3 text-[13px] text-red-600">{error}</p>}
      <>
          <div className="flex flex-col gap-2 rounded-lg border border-zinc-200 bg-zinc-50 p-3">
            <p className="m-0 text-[13px] font-medium text-zinc-700">Yeni üye ekle</p>
            <input
              value={inviteName}
              onChange={(e) => setInviteName(e.target.value)}
              placeholder="Ad Soyad"
              className={inputCls}
            />
            <select
              aria-label="Yeni üyenin rolü"
              value={inviteRole}
              onChange={(e) => setInviteRole(e.target.value)}
              className={inputCls}
            >
              {roles.map((r) => (
                <option key={r.key} value={r.key}>
                  {r.name}
                </option>
              ))}
            </select>
            <div className="flex items-center gap-2">
              <input
                type="email"
                value={inviteEmail}
                onChange={(e) => setInviteEmail(e.target.value)}
                placeholder="E-posta"
                className={inputCls + " min-w-0 flex-1"}
              />
              <button
                onClick={createInvite}
                disabled={busy || !inviteName.trim() || !inviteEmail.trim()}
                className="shrink-0 rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
              >
                {busy ? "Oluşturuluyor…" : "Davet oluştur"}
              </button>
            </div>
            {error && <p className="m-0 text-[13px] text-red-600">{error}</p>}
            {newLink && (
              <div className="flex flex-col gap-1 rounded-md border border-emerald-200 bg-emerald-50 px-2 py-1.5">
                <div className="flex items-center gap-2">
                  <input
                    readOnly
                    value={newLink}
                    onFocus={(e) => e.target.select()}
                    className="min-w-0 flex-1 bg-transparent text-[12px] text-emerald-800 outline-none"
                  />
                  <button
                    onClick={() => {
                      navigator.clipboard?.writeText(newLink);
                      setCopied(true);
                    }}
                    className="shrink-0 text-[12px] font-medium text-emerald-700 hover:underline"
                  >
                    {copied ? "Kopyalandı" : "Kopyala"}
                  </button>
                </div>
                <p className="m-0 text-[12px] text-emerald-700">
                  Davet e-postası gönderildi; bağlantıyı dilerseniz elle de paylaşabilirsiniz.
                </p>
              </div>
            )}
            {pendingInvites.length > 0 && (
              <div className="divide-y divide-zinc-100">
                {pendingInvites.map((i) => (
                  <div key={i.id} className="flex items-center gap-2 py-1.5 text-[12px]">
                    <span className="min-w-0 flex-1 truncate text-zinc-600">
                      {[i.full_name, i.email].filter(Boolean).join(" · ") || "herkese açık"} ·{" "}
                      {roleName(i.role)} ·{" "}
                      {new Date(i.expires_at).toLocaleDateString("tr-TR")} tarihine kadar
                    </span>
                    <button
                      onClick={() => revoke(i.id)}
                      className="shrink-0 rounded px-1.5 py-0.5 text-red-600 hover:bg-red-50"
                    >
                      İptal
                    </button>
                  </div>
                ))}
              </div>
            )}
            <p className="m-0 text-[12px] text-zinc-400">
              Davet bağlantısı e-postayla gider ve kopyalanabilir; yeni üye tıklayıp kendi parolasını
              belirleyince doğrudan büronuza katılır. Bağlantı tek kullanımlıktır ve 7 gün
              geçerlidir.
            </p>
          </div>
      </>
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
  | "ai"
  | "telegram"
  | "notifications"
  | "roles"
  | "audit";

const TAB_TITLE: Record<Tab, string> = {
  account: "Hesap",
  members: "Ofis üyeleri",
  persona: "Kişilik",
  memory: "Hafıza",
  style: "Yazım stili",
  ai: "Yapay zeka",
  telegram: "Telegram",
  notifications: "Bildirimler",
  roles: "Roller",
  audit: "Erişim kayıtları",
};

// URL slug per tab, e.g. /ayarlar/model — lets a tab be bookmarked, shared, or survive a reload.
const TAB_SLUG: Record<Tab, string> = {
  account: "hesap",
  members: "uyeler",
  persona: "kisilik",
  memory: "hafiza",
  style: "stil",
  ai: "yapay-zeka",
  telegram: "telegram",
  notifications: "bildirimler",
  roles: "roller",
  audit: "erisim-kayitlari",
};

function tabFromPath(path: string): Tab {
  const slug = path.replace(/^\/ayarlar\/?/, "");
  const hit = (Object.entries(TAB_SLUG) as [Tab, string][]).find(([, s]) => s === slug);
  return hit ? hit[0] : "account";
}

// Groups the left-nav tabs for the "Gruplu liste" picker option below.
const TAB_GROUPS: { label: string; ids: Tab[] }[] = [
  { label: "Hesap", ids: ["account", "members", "roles"] },
  { label: "Kişiselleştirme", ids: ["persona", "memory", "style"] },
  { label: "Sistem", ids: ["ai", "telegram", "notifications", "audit"] },
];

// Sekme ikonları — Phosphor set'inden (bkz. ../icons.ts).
const TAB_ICON: Record<Tab, Icon> = {
  account: UserIcon,
  members: Users,
  persona: Sparkle,
  memory: BookmarkSimple,
  style: PencilSimpleLine,
  ai: Cpu,
  telegram: TelegramLogo,
  notifications: EnvelopeSimple,
  roles: ShieldCheck,
  audit: ClockCounterClockwise,
};

function TabIcon({ id }: { id: Tab }) {
  const Ikon = TAB_ICON[id];
  return <Ikon size={17} aria-hidden className="shrink-0" />;
}

/** The signed-in member changes their own password (the installation admin starts with a generated one). */
export function ChangePassword() {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);
  const mismatch = confirm.length > 0 && next !== confirm;

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setDone(false);
    if (next.length < 10) {
      setError("Yeni parola en az 10 karakter olmalıdır.");
      return;
    }
    setBusy(true);
    try {
      await api.changePassword(current, next);
      setCurrent("");
      setNext("");
      setConfirm("");
      setDone(true);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Parola değiştirilemedi.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} aria-labelledby="change-password" className="mt-6 rounded-lg border border-zinc-200 p-4">
      <h2 id="change-password" className="mb-3 text-[15px] font-semibold text-zinc-900">
        Parolayı değiştir
      </h2>
      <div className="flex flex-col gap-3">
        <Field label="Mevcut parola" type="password" autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} required />
        <Field label="Yeni parola" type="password" autoComplete="new-password" value={next} onChange={(e) => setNext(e.target.value)} hint="En az 10 karakter." required />
        <Field
          label="Yeni parola (tekrar)"
          type="password"
          autoComplete="new-password"
          value={confirm}
          onChange={(e) => setConfirm(e.target.value)}
          error={mismatch ? "Parolalar aynı değil." : undefined}
          required
        />
      </div>
      {error && (
        <p role="alert" className="mt-3 text-[13px] text-red-700">
          {error}
        </p>
      )}
      {done && (
        <p role="status" className="mt-3 text-[13px] text-emerald-800">
          Parolanız değişti. Diğer cihazlardaki oturumlar kapatıldı.
        </p>
      )}
      <Button type="submit" className="mt-4" disabled={busy || !current || !next || mismatch}>
        {busy ? "Değiştiriliyor…" : "Parolayı değiştir"}
      </Button>
    </form>
  );
}

export function Settings({ user }: { user: User }) {
  const path = usePath();
  const tab = tabFromPath(path);
  // Each tab shows only for the permission its screen needs (the server checks every call too).
  const tabs: { id: Tab; label: string }[] = (
    [
      { id: "account", label: "Hesap", show: true },
      { id: "members", label: "Üyeler", show: can(user, P.membersManage) },
      { id: "roles", label: "Roller", show: can(user, P.rolesManage) },
      { id: "persona", label: "Kişilik", show: can(user, P.chatUse) },
      { id: "memory", label: "Hafıza", show: can(user, P.chatUse) },
      { id: "style", label: "Stil", show: can(user, P.chatUse) },
      { id: "ai", label: "Yapay zeka", show: can(user, P.integrationsManage) },
      { id: "telegram", label: "Telegram", show: can(user, P.integrationsManage) },
      { id: "notifications", label: "Bildirimler", show: can(user, P.officeSettings) },
      { id: "audit", label: "Erişim kayıtları", show: can(user, P.auditRead) },
    ] as { id: Tab; label: string; show: boolean }[]
  ).filter((t) => t.show);

  return (
    <div className="flex h-full min-w-0 flex-1 flex-col bg-white text-zinc-900 lg:flex-row">
      <div className="flex items-center gap-2 border-b border-zinc-950/10 bg-white p-3 lg:hidden">
        <div className="flex min-w-0 flex-1 items-center gap-1 overflow-x-auto">
          {tabs.map((t) => (
            <button
              key={t.id}
              type="button"
              onClick={() => navigate(`/ayarlar/${TAB_SLUG[t.id]}`)}
              className={
                "shrink-0 rounded-full px-3 py-1.5 text-sm font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-zinc-900 " +
                (tab === t.id
                  ? "bg-zinc-100 text-zinc-900"
                  : "text-zinc-600 hover:bg-zinc-50 hover:text-zinc-900")
              }
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>

      <aside className="hidden w-64 shrink-0 flex-col gap-1 border-r border-zinc-950/10 bg-white p-4 lg:flex">
        <p className="px-3 pb-2 text-[11px] font-semibold uppercase tracking-wide text-zinc-400">
          Ayarlar
        </p>

        <nav className="flex flex-col gap-4">
          {TAB_GROUPS.map((g) => {
            const items = tabs.filter((t) => g.ids.includes(t.id));
            if (!items.length) return null;
            return (
              <div key={g.label} className="flex flex-col gap-0.5">
                <p className="px-3 pb-1 text-[10px] font-semibold uppercase tracking-wide text-zinc-400">
                  {g.label}
                </p>
                {items.map((t) => (
                  <button
                    key={t.id}
                    type="button"
                    onClick={() => navigate(`/ayarlar/${TAB_SLUG[t.id]}`)}
                    className={
                      "flex items-center gap-2.5 rounded-r-lg border-l-2 py-2 pr-3 pl-2.5 text-left text-[15px] font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-zinc-900 " +
                      (tab === t.id
                        ? "border-zinc-900 bg-zinc-100 text-zinc-900"
                        : "border-transparent text-zinc-600 hover:bg-zinc-50 hover:text-zinc-900")
                    }
                  >
                    <span className={tab === t.id ? "text-zinc-700" : "text-zinc-400"}>
                      <TabIcon id={t.id} />
                    </span>
                    {t.label}
                  </button>
                ))}
              </div>
            );
          })}
        </nav>
      </aside>

      <div className="min-w-0 flex-1 overflow-y-auto bg-white">
        <div className="mx-auto w-full max-w-2xl px-6 py-8">
          <h1 className="mb-5 text-lg font-semibold text-zinc-900">{TAB_TITLE[tab]}</h1>

          {tab === "account" && (
            <div>
              <dl className="mb-6 divide-y divide-zinc-950/5 text-sm">
                <Row label="Hukuk bürosu" value={user.office ?? "—"} />
                <Row label="Rol" value={<RoleBadge name={user.role_name} />} />
                <Row label="Kullanıcı adı" value={user.username} />
                {/* The installation admin signs in with a user name and has no e-mail. */}
                {user.email.includes("@") && <Row label="E-posta" value={user.email} />}
                <Row label="Ad Soyad" value={user.full_name ?? "—"} />
              </dl>
              <BarNoField initial={user.bar_registry_no} />
              <TelegramLinkSection />
              <ChangePassword />
            </div>
          )}
          {tab === "members" && can(user, P.membersManage) && <Members />}
          {tab === "roles" && can(user, P.rolesManage) && <RolesSettings />}
          {tab === "persona" && can(user, P.chatUse) && <PersonaSettings />}
          {tab === "memory" && can(user, P.chatUse) && <MemorySettings />}
          {tab === "style" && can(user, P.chatUse) && <StyleTab />}
          {tab === "ai" && can(user, P.integrationsManage) && <AiSettings />}
          {tab === "telegram" && can(user, P.integrationsManage) && <TelegramSettings />}
          {tab === "notifications" && can(user, P.officeSettings) && <MailPreferenceSettings />}
          {tab === "audit" && can(user, P.auditRead) && <AuditLog />}
        </div>
      </div>
    </div>
  );
}
