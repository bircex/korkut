import { useEffect, useState } from "react";
import { api, ApiError, type ClientBrief } from "../api";
import { Modal } from "./Modal";
import { DataTable } from "./DataTable";

// Müvekkiller — a standalone client (müvekkil) management screen: list, create, and edit clients
// directly (not only through a matter). The PII entered here also feeds the redactor's known-entity
// layer, so cloud-bound prompts can mask each client's details. The list is a searchable, paged
// data table; editing happens in a modal.

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
  contactHidden,
  identityHidden,
}: {
  f: Fields;
  set: (k: keyof Fields) => (v: string) => void;
  withName?: boolean;
  /** Details the member may not see are left out of the form; the server keeps their values. */
  contactHidden?: boolean;
  identityHidden?: boolean;
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
      {!identityHidden && Field("TCKN", "national_id")}
      {!contactHidden && Field("Telefon", "phone")}
      {!contactHidden && Field("E-posta", "email")}
      {!identityHidden && Field("Doğum tarihi", "date_of_birth", "date")}
      {!contactHidden && <div className="col-span-2">{Field("Adres", "address")}</div>}
    </div>
  );
}

export function ClientsPanel() {
  const [clients, setClients] = useState<ClientBrief[] | null>(null);
  const [nf, setNf] = useState<Fields>({ ...EMPTY });
  const [adding, setAdding] = useState(false);
  // Düzenleme modalda: seçili müvekkil + form alanları.
  const [editing, setEditing] = useState<ClientBrief | null>(null);
  const [ef, setEf] = useState<Fields>({ ...EMPTY });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const setN = (k: keyof Fields) => (v: string) => setNf((s) => ({ ...s, [k]: v }));
  const setE = (k: keyof Fields) => (v: string) => setEf((s) => ({ ...s, [k]: v }));

  useEffect(() => {
    api
      .clients()
      .then((d) => setClients(d.items))
      .catch(() => setClients([]));
  }, []);

  function openEdit(c: ClientBrief) {
    setEf(toFields(c));
    setError("");
    setEditing(c);
  }

  async function saveEdit() {
    if (!editing) return;
    const name = ef.full_name.trim();
    if (!name) return;
    setBusy(true);
    setError("");
    try {
      const updated = await api.updateClient(editing.id, { full_name: name, ...toPatch(ef) });
      setClients((list) => (list ?? []).map((x) => (x.id === updated.id ? updated : x)));
      setEditing(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Kaydedilemedi");
    } finally {
      setBusy(false);
    }
  }

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
    <div className="flex h-full min-w-0 flex-1 flex-col bg-white">
      <div className="flex items-center gap-3 border-b border-zinc-200 px-4 py-3">
        <h1 className="text-lg font-bold text-zinc-900">Müvekkiller</h1>
        <button
          onClick={() => {
            setNf({ ...EMPTY });
            setError("");
            setAdding(true);
          }}
          className="ml-auto rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90"
        >
          + Yeni müvekkil
        </button>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto max-w-4xl px-4 py-4">
          {clients === null ? (
            <p className="text-[13px] text-zinc-400">Yükleniyor…</p>
          ) : (
            <DataTable
              rows={clients}
              rowKey={(c) => c.id}
              pageSize={10}
              empty="Henüz müvekkil yok."
              searchPlaceholder="Ad, telefon, e-posta ara…"
              searchText={(c) =>
                [c.full_name, c.phone, c.email, c.national_id].filter(Boolean).join(" ")
              }
              columns={[
                {
                  key: "name",
                  label: "Ad Soyad",
                  render: (c) => (
                    <span className="flex items-center gap-2">
                      <span className="font-medium text-zinc-900">{c.full_name}</span>
                      {c.kind === "corporate" && (
                        <span className="shrink-0 rounded-full bg-zinc-100 px-2 py-0.5 text-[11px] font-medium text-zinc-500">
                          Tüzel kişi
                        </span>
                      )}
                    </span>
                  ),
                },
                {
                  key: "phone",
                  label: "Telefon",
                  className: "hidden sm:table-cell",
                  render: (c) => <span className="text-zinc-600">{c.contact_hidden ? "gizli" : (c.phone ?? "-")}</span>,
                },
                {
                  key: "email",
                  label: "E-posta",
                  className: "hidden md:table-cell",
                  render: (c) => <span className="text-zinc-500">{c.contact_hidden ? "gizli" : (c.email ?? "-")}</span>,
                },
                {
                  key: "actions",
                  label: "",
                  className: "w-20 text-right",
                  render: (c) => (
                    <button
                      onClick={() => openEdit(c)}
                      className="rounded-md px-2 py-1 text-[12px] text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900"
                    >
                      Düzenle
                    </button>
                  ),
                },
              ]}
            />
          )}

          <p className="mt-4 text-[12px] text-zinc-400">
            Buradaki bilgiler buluta gönderilen metinlerde otomatik maskelenir.
          </p>
        </div>
      </div>

      {editing && (
        <Modal
          title="Müvekkili düzenle"
          onClose={() => setEditing(null)}
          footer={
            <>
              <button
                onClick={() => setEditing(null)}
                className="rounded-md px-3 py-2 text-sm font-medium text-zinc-600 hover:bg-zinc-100"
              >
                Vazgeç
              </button>
              <button
                onClick={saveEdit}
                disabled={busy || !ef.full_name.trim()}
                className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
              >
                {busy ? "Kaydediliyor…" : "Kaydet"}
              </button>
            </>
          }
        >
          <div className="flex flex-col gap-3">
            <FieldGrid
              f={ef}
              set={setE}
              withName
              contactHidden={editing?.contact_hidden}
              identityHidden={editing?.identity_hidden}
            />
            {error && <span className="text-[13px] text-red-600">{error}</span>}
          </div>
        </Modal>
      )}

      {adding && (
        <Modal
          title="Yeni müvekkil"
          onClose={() => setAdding(false)}
          footer={
            <>
              <button
                onClick={() => setAdding(false)}
                className="rounded-md px-3 py-2 text-sm font-medium text-zinc-600 hover:bg-zinc-100"
              >
                Vazgeç
              </button>
              <button
                onClick={create}
                disabled={busy || !nf.full_name.trim()}
                className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
              >
                {busy ? "Ekleniyor…" : "Ekle"}
              </button>
            </>
          }
        >
          <div className="flex flex-col gap-3">
            <FieldGrid f={nf} set={setN} withName />
            {error && <span className="text-[13px] text-red-600">{error}</span>}
          </div>
        </Modal>
      )}
    </div>
  );
}
