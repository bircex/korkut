import { Fragment, useEffect, useState } from "react";
import { ApiError, rolesApi, type Role, type RolesResponse } from "../api";
import { Button, Field } from "../a11y";

// Ayarlar → Roller: satırlarda izinler (gruplu), sütunlarda roller. Bir rolün izinleri burada
// işaretlenir; kaydedilen değişiklik o roldeki herkes için bir sonraki istekte geçerli olur.
// Yönetici rolü her zaman her şeye yetkilidir ve değiştirilemez.

function message(err: unknown, fallback: string) {
  return err instanceof ApiError ? err.message : fallback;
}

export function RolesSettings() {
  const [data, setData] = useState<RolesResponse | null>(null);
  const [draft, setDraft] = useState<Record<string, string[]>>({});
  const [newName, setNewName] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    rolesApi
      .list()
      .then((d) => {
        setData(d);
        setDraft(Object.fromEntries(d.roles.map((r) => [r.key, r.permissions])));
      })
      .catch((err) => setError(message(err, "Roller yüklenemedi.")));
  }, [reload]);

  if (!data) {
    return error ? (
      <p role="alert" className="text-[13px] text-red-700">
        {error}
      </p>
    ) : (
      <p className="text-[13px] text-zinc-400">Yükleniyor…</p>
    );
  }

  const dirty = data.roles.filter((r) => r.editable && !sameSet(r.permissions, draft[r.key] ?? []));

  function toggle(role: Role, permission: string) {
    setSaved(false);
    setDraft((d) => {
      const current = d[role.key] ?? [];
      const next = current.includes(permission) ? current.filter((p) => p !== permission) : [...current, permission];
      return { ...d, [role.key]: next };
    });
  }

  async function save() {
    setBusy(true);
    setError(null);
    try {
      for (const role of dirty) {
        await rolesApi.update(role.key, { name: role.name, description: role.description, permissions: draft[role.key] ?? [] });
      }
      setSaved(true);
      setReload((n) => n + 1);
    } catch (err) {
      setError(message(err, "Roller kaydedilemedi."));
    } finally {
      setBusy(false);
    }
  }

  async function addRole() {
    setBusy(true);
    setError(null);
    try {
      await rolesApi.create({ name: newName.trim(), permissions: [] });
      setNewName("");
      setReload((n) => n + 1);
    } catch (err) {
      setError(message(err, "Rol eklenemedi."));
    } finally {
      setBusy(false);
    }
  }

  async function removeRole(role: Role) {
    if (!confirm(`"${role.name}" rolü silinsin mi?`)) return;
    setBusy(true);
    setError(null);
    try {
      await rolesApi.remove(role.key);
      setReload((n) => n + 1);
    } catch (err) {
      setError(message(err, "Rol silinemedi."));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col gap-5">
      <p className="text-[13px] text-zinc-500">
        Her üyenin bir rolü vardır; rol, üyenin neleri görüp yapabileceğini belirler. Üyeler yalnızca ekibinde oldukları
        dosyaları görür, Yönetici bütün dosyaları. Rolü Ayarlar → Üyeler bölümünden verirsiniz.
      </p>

      <div className="overflow-x-auto rounded-lg border border-zinc-200">
        <table className="w-full min-w-[640px] border-collapse text-[13px]">
          <thead>
            <tr className="bg-zinc-50">
              <th scope="col" className="sticky left-0 bg-zinc-50 px-3 py-2 text-left font-medium text-zinc-500">
                İzin
              </th>
              {data.roles.map((r) => (
                <th key={r.key} scope="col" className="px-2 py-2 text-center align-bottom font-semibold text-zinc-800">
                  <span className="block">{r.name}</span>
                  <span className="block text-[11px] font-normal text-zinc-500">{r.member_count} üye</span>
                  {!r.is_system && r.member_count === 0 && (
                    <button
                      type="button"
                      onClick={() => removeRole(r)}
                      disabled={busy}
                      className="mt-1 text-[11px] font-normal text-red-600 hover:underline"
                    >
                      Sil
                    </button>
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.catalog.map((group) => (
              <Fragment key={group.name}>
                <tr>
                  <th
                    scope="colgroup"
                    colSpan={data.roles.length + 1}
                    className="bg-white px-3 pt-3 pb-1 text-left text-[11px] font-semibold uppercase tracking-wide text-zinc-400"
                  >
                    {group.name}
                  </th>
                </tr>
                {group.permissions.map((p) => (
                  <tr key={p.key} className="border-t border-zinc-100">
                    <th scope="row" className="sticky left-0 bg-white px-3 py-1.5 text-left font-normal text-zinc-700">
                      {p.label}
                      {p.hint && <span className="block text-[11px] text-zinc-400">{p.hint}</span>}
                    </th>
                    {data.roles.map((r) => (
                      <td key={r.key} className="px-2 py-1.5 text-center">
                        <input
                          type="checkbox"
                          aria-label={`${r.name}: ${p.label}`}
                          checked={(draft[r.key] ?? []).includes(p.key)}
                          disabled={!r.editable || busy}
                          onChange={() => toggle(r, p.key)}
                          className="size-4 accent-zinc-900 disabled:opacity-60"
                        />
                      </td>
                    ))}
                  </tr>
                ))}
              </Fragment>
            ))}
          </tbody>
        </table>
      </div>

      {error && (
        <p role="alert" className="text-[13px] text-red-700">
          {error}
        </p>
      )}
      {saved && (
        <p role="status" className="text-[13px] text-emerald-800">
          Roller kaydedildi.
        </p>
      )}
      <div className="flex flex-wrap items-center gap-3">
        <Button onClick={save} disabled={busy || dirty.length === 0}>
          {busy ? "Kaydediliyor…" : dirty.length > 0 ? `Değişiklikleri kaydet (${dirty.length} rol)` : "Değişiklik yok"}
        </Button>
      </div>

      <section aria-labelledby="new-role" className="rounded-lg border border-zinc-200 p-4">
        <h2 id="new-role" className="mb-3 text-[15px] font-semibold text-zinc-900">
          Yeni rol
        </h2>
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <div className="min-w-0 flex-1">
            <Field label="Rol adı" value={newName} placeholder="Örn. Dış danışman" onChange={(e) => setNewName(e.target.value)} />
          </div>
          <Button variant="secondary" onClick={addRole} disabled={busy || newName.trim().length < 2}>
            Rol ekle
          </Button>
        </div>
        <p className="mt-2 text-[12px] text-zinc-500">Yeni rol hiçbir izinle başlamaz; tablodan işaretleyip kaydedin.</p>
      </section>
    </div>
  );
}

function sameSet(a: string[], b: string[]) {
  return a.length === b.length && a.every((x) => b.includes(x));
}
