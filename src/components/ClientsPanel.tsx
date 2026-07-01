import { useEffect, useState } from "react";
import { api, ApiError, type ClientBrief } from "../api";

// Müvekkiller — a standalone client (müvekkil) management screen: list, create, and edit clients
// directly (not only through a matter). The PII entered here also feeds the redactor's known-entity
// layer, so cloud-bound prompts can mask each client's details.

const input =
  "w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900";

const EMPTY = { full_name: "", national_id: "", phone: "", email: "", address: "", date_of_birth: "" };
type Fields = typeof EMPTY;

function toFields(c: ClientBrief): Fields {
  return {
    full_name: c.full_name,
    national_id: c.national_id ?? "",
    phone: c.phone ?? "",
    email: c.email ?? "",
    address: c.address ?? "",
    date_of_birth: c.date_of_birth ?? "",
  };
}

function toPatch(f: Fields) {
  return {
    national_id: f.national_id.trim() || null,
    phone: f.phone.trim() || null,
    email: f.email.trim() || null,
    address: f.address.trim() || null,
    date_of_birth: f.date_of_birth.trim() || null,
  };
}

function FieldGrid({
  f,
  set,
  withName,
}: {
  f: Fields;
  set: (k: keyof Fields) => (v: string) => void;
  withName?: boolean;
}) {
  const Field = (label: string, k: keyof Fields, type = "text") => (
    <label className="flex flex-col gap-1">
      <span className="text-[12px] text-zinc-500">{label}</span>
      <input type={type} value={f[k]} onChange={(e) => set(k)(e.target.value)} className={input} />
    </label>
  );
  return (
    <div className="grid grid-cols-2 gap-2">
      {withName && <div className="col-span-2">{Field("Ad Soyad", "full_name")}</div>}
      {Field("TCKN", "national_id")}
      {Field("Telefon", "phone")}
      {Field("E-posta", "email")}
      {Field("Doğum tarihi", "date_of_birth", "date")}
      <div className="col-span-2">{Field("Adres", "address")}</div>
    </div>
  );
}

function ClientRow({
  client,
  onSaved,
}: {
  client: ClientBrief;
  onSaved: (c: ClientBrief) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [f, setF] = useState<Fields>(toFields(client));
  const [busy, setBusy] = useState(false);
  const set = (k: keyof Fields) => (v: string) => setF((s) => ({ ...s, [k]: v }));

  async function save() {
    const name = f.full_name.trim();
    if (!name) return;
    setBusy(true);
    try {
      const updated = await api.updateClient(client.id, { full_name: name, ...toPatch(f) });
      onSaved(updated);
      setEditing(false);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="px-4 py-3">
      <div className="flex items-center gap-2">
        <span className="min-w-0 flex-1">
          <span className="block truncate text-sm text-zinc-800">{client.full_name}</span>
          <span className="block truncate text-[12px] text-zinc-400">
            {[client.national_id, client.phone].filter(Boolean).join(" · ") || "—"}
          </span>
        </span>
        <button
          onClick={() => {
            setF(toFields(client));
            setEditing((e) => !e);
          }}
          className="shrink-0 text-[12px] text-zinc-500 hover:text-zinc-900"
        >
          {editing ? "Kapat" : "Düzenle"}
        </button>
      </div>
      {editing && (
        <div className="mt-3 flex flex-col gap-3">
          <FieldGrid f={f} set={set} withName />
          <div className="flex gap-2">
            <button
              onClick={save}
              disabled={busy}
              className="rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
            >
              {busy ? "Kaydediliyor…" : "Kaydet"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

export function ClientsPanel({ onClose }: { onClose: () => void }) {
  const [clients, setClients] = useState<ClientBrief[] | null>(null);
  const [nf, setNf] = useState<Fields>({ ...EMPTY });
  const [adding, setAdding] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const setN = (k: keyof Fields) => (v: string) => setNf((s) => ({ ...s, [k]: v }));

  useEffect(() => {
    api
      .clients()
      .then((d) => setClients(d.items))
      .catch(() => setClients([]));
  }, []);

  async function create() {
    const name = nf.full_name.trim();
    if (!name) return;
    setBusy(true);
    setError("");
    try {
      const c = await api.createClient({ full_name: name, ...toPatch(nf) });
      setClients((list) => [c, ...(list ?? [])]);
      setNf({ ...EMPTY });
      setAdding(false);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Müvekkil eklenemedi");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex h-dvh flex-col bg-white">
      <div className="flex items-center gap-3 border-b border-zinc-200 px-4 py-3">
        <button
          onClick={onClose}
          className="rounded-lg border border-zinc-300 px-3 py-1.5 text-sm text-zinc-700 hover:border-zinc-900"
        >
          Geri
        </button>
        <h1 className="text-lg font-bold text-zinc-900">Müvekkiller</h1>
        <button
          onClick={() => setAdding((a) => !a)}
          className="ml-auto rounded-lg bg-zinc-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-zinc-700"
        >
          {adding ? "Vazgeç" : "Yeni müvekkil"}
        </button>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto max-w-2xl px-4 py-4">
          {adding && (
            <div className="mb-4 rounded-xl border border-zinc-200 p-4">
              <p className="mb-2 text-sm font-semibold text-zinc-700">Yeni müvekkil</p>
              <div className="flex flex-col gap-3">
                <FieldGrid f={nf} set={setN} withName />
                <div className="flex items-center gap-2">
                  <button
                    onClick={create}
                    disabled={busy || !nf.full_name.trim()}
                    className="rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
                  >
                    {busy ? "Ekleniyor…" : "Ekle"}
                  </button>
                  {error && <span className="text-[13px] text-red-600">{error}</span>}
                </div>
              </div>
            </div>
          )}

          {clients === null ? (
            <p className="text-[13px] text-zinc-400">Yükleniyor…</p>
          ) : clients.length === 0 ? (
            <p className="text-[13px] text-zinc-400">Henüz müvekkil yok.</p>
          ) : (
            <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200">
              {clients.map((c) => (
                <ClientRow
                  key={c.id}
                  client={c}
                  onSaved={(u) =>
                    setClients((list) => (list ?? []).map((x) => (x.id === u.id ? u : x)))
                  }
                />
              ))}
            </div>
          )}

          <p className="mt-4 text-[12px] text-zinc-400">
            Buradaki bilgiler buluta gönderilen metinlerde otomatik maskelenir.
          </p>
        </div>
      </div>
    </div>
  );
}
