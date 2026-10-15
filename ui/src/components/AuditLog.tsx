import { useEffect, useState } from "react";
import { ApiError, auditApi, type AuditEvent } from "../api";
import { Button } from "../a11y";

// Ayarlar → Erişim kayıtları: kimin, ne zaman, neye eriştiği ya da neyi değiştirdiği. Müvekkil
// bilgisi görüntülemeleri alan adıyla tutulur, değerleriyle değil. Yalnızca audit.read ile görünür.

const PAGE = 50;

const ACTION_LABEL: Record<string, string> = {
  "client.details_viewed": "Müvekkil bilgisi görüntülendi",
  "finance.viewed": "Mali kayıtlar görüntülendi",
  "member.role_changed": "Üyenin rolü değişti",
  "member.removed": "Üye çıkarıldı",
  "matter.team_added": "Dosya ekibine eklendi",
  "matter.team_lead": "Dosya sorumlusu değişti",
  "matter.team_removed": "Dosya ekibinden çıkarıldı",
  "access.denied": "Erişim reddedildi",
  "role.created": "Rol oluşturuldu",
  "role.changed": "Rol izinleri değişti",
  "role.removed": "Rol silindi",
  "2fa.enrolled": "İki adımlı doğrulama açıldı",
  "2fa.reset": "İki adımlı doğrulama sıfırlandı",
  "2fa.recovery_used": "Kurtarma kodu kullanıldı",
};

const FILTERS: { value: string; label: string }[] = [
  { value: "", label: "Tümü" },
  { value: "client", label: "Müvekkil bilgisi" },
  { value: "finance", label: "Mali" },
  { value: "matter", label: "Dosya ekibi" },
  { value: "member", label: "Üyeler" },
  { value: "role", label: "Roller" },
  { value: "access", label: "Reddedilen erişim" },
];

const FIELD_LABEL: Record<string, string> = {
  email: "e-posta",
  phone: "telefon",
  address: "adres",
  national_id: "kimlik no",
  date_of_birth: "doğum tarihi",
};

// Kaydın ayrıntısı (sunucuda maskelenmiş JSON) okunur bir satıra çevrilir.
function describe(e: AuditEvent): string {
  if (!e.details) return "";
  try {
    const d = JSON.parse(e.details) as Record<string, unknown>;
    if (Array.isArray(d.fields)) return (d.fields as string[]).map((f) => FIELD_LABEL[f] ?? f).join(", ");
    if (e.action === "access.denied") return `${String(d.method ?? "")} ${String(d.path ?? "")}`.trim();
    if (typeof d.role === "string") return `Yeni rol: ${d.role}`;
    if (typeof d.what === "string") return { invoices: "Faturalar", expenses: "Masraflar", summary: "Özet" }[d.what] ?? d.what;
    return "";
  } catch {
    return "";
  }
}

export function AuditLog() {
  const [items, setItems] = useState<AuditEvent[]>([]);
  const [total, setTotal] = useState(0);
  const [filter, setFilter] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let live = true;
    auditApi
      .list(PAGE, 0, filter || undefined)
      .then((page) => {
        if (!live) return;
        setItems(page.items);
        setTotal(page.total);
        setError(null);
      })
      .catch((err) => live && setError(err instanceof ApiError ? err.message : "Kayıtlar yüklenemedi."))
      .finally(() => live && setLoading(false));
    return () => {
      live = false;
    };
  }, [filter]);

  async function more() {
    try {
      const page = await auditApi.list(PAGE, items.length, filter || undefined);
      setItems((prev) => [...prev, ...page.items]);
      setTotal(page.total);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Kayıtlar yüklenemedi.");
    }
  }

  return (
    <section aria-labelledby="audit-title">
      <div className="mb-3 flex flex-wrap items-center gap-3">
        <p id="audit-title" className="text-[13px] text-zinc-500">
          Müvekkil bilgisi ve mali kayıt görüntülemeleri, rol ve ekip değişiklikleri ile reddedilen erişimler.
        </p>
        <label className="ml-auto flex items-center gap-2 text-sm text-zinc-600">
          Tür
          <select
            value={filter}
            onChange={(e) => {
              setLoading(true);
              setFilter(e.target.value);
            }}
            className="rounded-lg border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-800 outline-none focus:border-zinc-900"
          >
            {FILTERS.map((f) => (
              <option key={f.value} value={f.value}>
                {f.label}
              </option>
            ))}
          </select>
        </label>
      </div>
      {error && (
        <p role="alert" className="mb-3 text-[13px] text-red-700">
          {error}
        </p>
      )}
      <div className="overflow-x-auto rounded-xl border border-zinc-200">
        <table className="w-full text-left text-sm">
          <thead className="bg-zinc-50 text-[12px] uppercase tracking-wide text-zinc-500">
            <tr>
              <th scope="col" className="px-3 py-2 font-medium">Zaman</th>
              <th scope="col" className="px-3 py-2 font-medium">Kişi</th>
              <th scope="col" className="px-3 py-2 font-medium">Olay</th>
              <th scope="col" className="px-3 py-2 font-medium">Ayrıntı</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-zinc-100">
            {items.map((e) => (
              <tr key={e.id}>
                <td className="whitespace-nowrap px-3 py-2 text-zinc-500">
                  {new Date(e.at).toLocaleString("tr-TR", { dateStyle: "short", timeStyle: "short" })}
                </td>
                <td className="px-3 py-2 text-zinc-800">{e.actor_name ?? "Sistem"}</td>
                <td className={"px-3 py-2 " + (e.action === "access.denied" ? "text-red-700" : "text-zinc-800")}>
                  {ACTION_LABEL[e.action] ?? e.action}
                </td>
                <td className="px-3 py-2 text-zinc-500">{describe(e)}</td>
              </tr>
            ))}
            {!loading && items.length === 0 && (
              <tr>
                <td colSpan={4} className="px-3 py-4 text-center text-zinc-400">
                  Kayıt yok.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      {items.length < total && (
        <Button variant="secondary" className="mt-3" onClick={more}>
          Daha eski kayıtlar · {total - items.length}
        </Button>
      )}
    </section>
  );
}
