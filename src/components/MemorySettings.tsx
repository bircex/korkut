import { useEffect, useState } from "react";
import { api, type MemoryItem } from "../api";

// Ayarlar → Hafıza: the office's durable notes Korkut keeps in mind across chats. Shared office
// knowledge — any member can add or remove. Manually curated (nothing is auto-remembered); the
// text is injected into prompts as context and masked by the redactor on cloud calls.

const KIND_LABEL: Record<string, string> = {
  note: "Not",
  fact: "Bilgi",
  preference: "Tercih",
};

export function MemorySettings() {
  const [items, setItems] = useState<MemoryItem[] | null>(null);
  const [text, setText] = useState("");
  const [kind, setKind] = useState("note");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api
      .listMemory()
      .then((d) => setItems(d.items))
      .catch(() => {});
  }, []);

  async function add() {
    const t = text.trim();
    if (!t) return;
    setBusy(true);
    try {
      const created = await api.addMemory(t, kind);
      setItems((list) => (list ? [created, ...list] : [created]));
      setText("");
      setKind("note");
    } finally {
      setBusy(false);
    }
  }

  async function remove(id: number) {
    await api.deleteMemory(id);
    setItems((list) => (list ? list.filter((x) => x.id !== id) : list));
  }

  if (!items) return <p className="text-[13px] text-zinc-400">Yükleniyor…</p>;

  return (
    <div>
      <p className="mb-4 text-[13px] text-zinc-500">
        Korkut'un her sohbette akılda tutmasını istediğiniz kalıcı notlar. Büro geneli ve
        paylaşımlıdır; elle eklenir (hiçbir şey otomatik hatırlanmaz).
      </p>

      <div className="mb-5 rounded-lg border border-zinc-200 p-3">
        <div className="flex flex-col gap-2">
          <textarea
            value={text}
            rows={2}
            placeholder="Örn. Bu büro icra dosyalarında X şablonunu tercih eder."
            onChange={(e) => setText(e.target.value)}
            className="resize-y rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 outline-none focus:border-zinc-900"
          />
          <div className="flex items-center gap-2">
            <select
              value={kind}
              onChange={(e) => setKind(e.target.value)}
              className="rounded-md border border-zinc-300 bg-white px-2 py-2 text-sm text-zinc-700 outline-none focus:border-zinc-900"
            >
              <option value="note">Not</option>
              <option value="fact">Bilgi</option>
              <option value="preference">Tercih</option>
            </select>
            <button
              onClick={add}
              disabled={busy || !text.trim()}
              className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
            >
              {busy ? "Ekleniyor…" : "Ekle"}
            </button>
          </div>
        </div>
      </div>

      {items.length === 0 ? (
        <p className="text-[13px] text-zinc-400">Henüz not yok.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((m) => (
            <li
              key={m.id}
              className="flex items-start justify-between gap-3 rounded-lg border border-zinc-200 p-3"
            >
              <div className="min-w-0">
                <p className="m-0 whitespace-pre-wrap text-sm text-zinc-800">{m.text}</p>
                <p className="m-0 mt-1 text-[12px] text-zinc-400">
                  {KIND_LABEL[m.kind] ?? m.kind}
                  {m.author ? ` · ${m.author}` : ""}
                </p>
              </div>
              <button
                onClick={() => remove(m.id)}
                className="shrink-0 rounded-md border border-zinc-300 px-2 py-1 text-[12px] text-zinc-600 hover:border-red-400 hover:text-red-600"
              >
                Sil
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
