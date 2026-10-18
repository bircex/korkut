import { P, useCan } from "../permissions";
import { useState } from "react";
import { api, type ClientBrief } from "../api";

// Editable client (müvekkil) PII shown inside a matter detail. These values feed the redactor's
// known-entity layer, so entering them here is what lets Korkut mask the client's TCKN, phone,
// e-mail, address and birth date in cloud-bound prompts. Birth date uses a native date picker so
// it round-trips as ISO (the backend parses ISO); it is masked as dd.mm.yyyy.

function fmtDate(iso: string | null): string {
  if (!iso) return "—";
  const [y, m, d] = iso.split("-");
  return d && m && y ? `${d}.${m}.${y}` : iso;
}

function Ro({ label, value, hidden }: { label: string; value: string | null; hidden?: boolean }) {
  return (
    <div>
      <div className="text-[11px] uppercase tracking-wide text-zinc-400">{label}</div>
      {hidden ? (
        <div className="mt-0.5 text-[12px] text-zinc-400">Görme yetkiniz yok</div>
      ) : (
        <div className="mt-0.5 text-zinc-800">{value || "—"}</div>
      )}
    </div>
  );
}

function Ed({
  label,
  value,
  onChange,
  type = "text",
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  type?: string;
}) {
  return (
    <label className="flex flex-col gap-1">
      <span className="text-[12px] text-zinc-500">{label}</span>
      <input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-md border border-zinc-300 bg-white px-2.5 py-1.5 text-sm outline-none focus:border-zinc-900"
      />
    </label>
  );
}

export function ClientDetailsCard({
  client,
  onSaved,
}: {
  client: ClientBrief;
  onSaved: (updated: ClientBrief) => void;
}) {
  const canWrite = useCan(P.clientsWrite);
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [f, setF] = useState({
    national_id: client.national_id ?? "",
    phone: client.phone ?? "",
    email: client.email ?? "",
    address: client.address ?? "",
    date_of_birth: client.date_of_birth ?? "",
  });

  const set = (k: keyof typeof f) => (v: string) => setF((s) => ({ ...s, [k]: v }));

  async function save() {
    setBusy(true);
    try {
      const updated = await api.updateClient(client.id, {
        national_id: f.national_id.trim() || null,
        phone: f.phone.trim() || null,
        email: f.email.trim() || null,
        address: f.address.trim() || null,
        date_of_birth: f.date_of_birth.trim() || null,
      });
      onSaved(updated);
      setEditing(false);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mt-4 rounded-xl border border-zinc-200 p-4">
      <div className="mb-2 flex items-center justify-between">
        <h2 className="text-sm font-semibold text-zinc-700">Müvekkil bilgileri</h2>
        {!editing && canWrite && (
          <button
            onClick={() => setEditing(true)}
            className="text-[12px] text-zinc-500 hover:text-zinc-900"
          >
            Düzenle
          </button>
        )}
      </div>
      <p className="mb-3 text-sm font-medium text-zinc-800">{client.full_name}</p>

      {!editing ? (
        <div className="grid grid-cols-2 gap-3 text-sm">
          <Ro label="TCKN" value={client.national_id} hidden={client.identity_hidden} />
          <Ro label="Telefon" value={client.phone} hidden={client.contact_hidden} />
          <Ro label="E-posta" value={client.email} hidden={client.contact_hidden} />
          <Ro label="Doğum tarihi" value={fmtDate(client.date_of_birth)} hidden={client.identity_hidden} />
          <Ro label="Adres" value={client.address} hidden={client.contact_hidden} />
        </div>
      ) : (
        <div className="flex flex-col gap-2">
          {/* Fields the member may not see are left out; the server keeps their values. */}
          {!client.identity_hidden && <Ed label="TCKN" value={f.national_id} onChange={set("national_id")} />}
          {!client.contact_hidden && <Ed label="Telefon" value={f.phone} onChange={set("phone")} />}
          {!client.contact_hidden && <Ed label="E-posta" value={f.email} onChange={set("email")} />}
          {!client.identity_hidden && (
            <Ed label="Doğum tarihi" type="date" value={f.date_of_birth} onChange={set("date_of_birth")} />
          )}
          {!client.contact_hidden && <Ed label="Adres" value={f.address} onChange={set("address")} />}
          <div className="mt-1 flex gap-2">
            <button
              onClick={save}
              disabled={busy}
              className="rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
            >
              {busy ? "Kaydediliyor…" : "Kaydet"}
            </button>
            <button
              onClick={() => setEditing(false)}
              className="rounded-md border border-zinc-300 px-3 py-1.5 text-sm text-zinc-600 hover:border-zinc-900"
            >
              Vazgeç
            </button>
          </div>
        </div>
      )}

      <p className="mt-3 text-[11px] text-zinc-400">
        Bu bilgiler buluta gönderilen metinlerde otomatik maskelenir.
      </p>
    </div>
  );
}
