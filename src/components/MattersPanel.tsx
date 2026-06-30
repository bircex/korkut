import { useEffect, useState } from "react";
import { api, type AgendaItem, type MatterBrief, type MatterDetail, type User } from "../api";

// Minimal "Dosyalar" (matters) workspace: browse the office's case files, open one to see its
// details + hearings, create a matter, add a hearing, and start a chat filed under the matter.
// Full-screen panel like Settings/Customize; reached from the user menu.

const input =
  "w-full rounded-lg border border-zinc-200 px-3 py-2 text-sm outline-none focus:border-zinc-400";

const STATUS_LABEL: Record<string, string> = { open: "Açık", closed: "Kapalı" };

export function MattersPanel({
  user,
  onClose,
  onOpenSession,
}: {
  user: User;
  onClose: () => void;
  onOpenSession: (sessionId: number) => void;
}) {
  const [matters, setMatters] = useState<MatterBrief[]>([]);
  const [agenda, setAgenda] = useState<AgendaItem[]>([]);
  const [detail, setDetail] = useState<MatterDetail | null>(null); // null → list view
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // New-matter form.
  const [nm, setNm] = useState({ title: "", client_name: "", court: "", case_type: "", counterparty: "" });
  // Add-hearing / add-deadline forms (within a detail).
  const [nh, setNh] = useState({ date: "", time: "", type: "", location: "" });
  const [nd, setNd] = useState({ due_date: "", title: "", type: "" });

  async function refresh() {
    try {
      const [m, a] = await Promise.all([api.matters(), api.agenda(30)]);
      setMatters(m.items);
      setAgenda(a.items);
    } catch {
      setError("Dosyalar yüklenemedi.");
    }
  }

  useEffect(() => {
    refresh();
  }, []);

  async function openMatter(id: number) {
    setError(null);
    try {
      setDetail(await api.getMatter(id));
    } catch {
      setError("Dosya açılamadı.");
    }
  }

  async function createMatter() {
    if (!nm.title.trim()) return;
    setBusy(true);
    setError(null);
    try {
      const d = await api.createMatter({
        title: nm.title.trim(),
        client_name: nm.client_name.trim() || null,
        court: nm.court.trim() || null,
        case_type: nm.case_type.trim() || null,
        counterparty: nm.counterparty.trim() || null,
      });
      setNm({ title: "", client_name: "", court: "", case_type: "", counterparty: "" });
      setCreating(false);
      await refresh();
      setDetail(d);
    } catch {
      setError("Dosya oluşturulamadı.");
    } finally {
      setBusy(false);
    }
  }

  async function toggleStatus() {
    if (!detail) return;
    const next = detail.status === "open" ? "closed" : "open";
    setDetail(await api.updateMatter(detail.id, { status: next }));
    refresh();
  }

  async function addHearing() {
    if (!detail || !nh.date.trim()) return;
    setBusy(true);
    setError(null);
    try {
      await api.addHearing(detail.id, {
        date: nh.date.trim(),
        time: nh.time.trim() || null,
        type: nh.type.trim() || null,
        location: nh.location.trim() || null,
      });
      setNh({ date: "", time: "", type: "", location: "" });
      setDetail(await api.getMatter(detail.id));
    } catch {
      setError("Duruşma eklenemedi (tarih biçimi YYYY-AA-GG olmalı).");
    } finally {
      setBusy(false);
    }
  }

  async function addDeadline() {
    if (!detail || !nd.due_date.trim() || !nd.title.trim()) return;
    setBusy(true);
    setError(null);
    try {
      await api.addDeadline(detail.id, {
        due_date: nd.due_date.trim(),
        title: nd.title.trim(),
        type: nd.type.trim() || null,
      });
      setNd({ due_date: "", title: "", type: "" });
      setDetail(await api.getMatter(detail.id));
      refresh();
    } catch {
      setError("Süre eklenemedi (tarih biçimi YYYY-AA-GG olmalı).");
    } finally {
      setBusy(false);
    }
  }

  async function completeDeadline(did: number) {
    await api.completeDeadline(did);
    if (detail) setDetail(await api.getMatter(detail.id));
    refresh();
  }

  async function removeMatter() {
    if (!detail) return;
    if (!confirm(`'${detail.title}' dosyası silinsin mi? Bu işlem geri alınamaz.`)) return;
    await api.deleteMatter(detail.id);
    setDetail(null);
    refresh();
  }

  async function openChat() {
    if (!detail) return;
    const sess = await api.createSession(detail.id);
    onOpenSession(sess.id);
  }

  const header = (title: string, back: () => void) => (
    <header className="flex items-center gap-3 border-b border-zinc-200 px-4 py-3">
      <button onClick={back} className="text-sm text-zinc-500 hover:text-zinc-900">
        ← Geri
      </button>
      <h1 className="text-base font-semibold text-zinc-900">{title}</h1>
      <span className="ml-auto text-xs text-zinc-400">{user.office ?? ""}</span>
    </header>
  );

  // ---- Detail view ----
  if (detail) {
    return (
      <div className="flex h-dvh flex-col bg-white">
        {header(detail.title, () => setDetail(null))}
        <div className="min-h-0 flex-1 overflow-y-auto">
          <div className="mx-auto max-w-3xl px-4 py-4">
            {error && <div className="mb-3 text-xs text-red-600">{error}</div>}

            <div className="rounded-xl border border-zinc-200 p-4">
              <div className="grid grid-cols-2 gap-3 text-sm">
                <Field label="Müvekkil" value={detail.client?.full_name} />
                <Field label="Mahkeme" value={detail.court} />
                <Field label="Esas no" value={detail.file_no} />
                <Field label="Dava türü" value={detail.case_type} />
                <Field label="Karşı taraf" value={detail.counterparty} />
                <div>
                  <div className="text-[11px] uppercase tracking-wide text-zinc-400">Durum</div>
                  <button
                    onClick={toggleStatus}
                    className="mt-0.5 rounded-md bg-zinc-100 px-2 py-0.5 text-sm text-zinc-700 hover:bg-zinc-200"
                  >
                    {STATUS_LABEL[detail.status] ?? detail.status} ↺
                  </button>
                </div>
              </div>
              <div className="mt-4 flex gap-2">
                <button
                  onClick={openChat}
                  className="rounded-lg bg-zinc-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-zinc-700"
                >
                  Bu dosyada sohbet aç
                </button>
                <button
                  onClick={removeMatter}
                  className="ml-auto rounded-lg border border-zinc-200 px-3 py-1.5 text-sm text-red-600 hover:bg-red-50"
                >
                  Sil
                </button>
              </div>
            </div>

            <h2 className="mb-2 mt-6 text-sm font-semibold text-zinc-700">Duruşmalar</h2>
            <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200">
              {detail.hearings.length === 0 && (
                <div className="px-4 py-3 text-sm text-zinc-400">Henüz duruşma yok.</div>
              )}
              {detail.hearings.map((h) => (
                <div key={h.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                  <span className="font-medium text-zinc-800">
                    {h.date}
                    {h.time ? ` ${h.time}` : ""}
                  </span>
                  <span className="text-zinc-500">{h.type ?? ""}</span>
                  <span className="ml-auto text-xs text-zinc-400">{h.location ?? ""}</span>
                </div>
              ))}
            </div>

            <div className="mt-3 flex flex-wrap items-end gap-2 rounded-xl border border-zinc-200 p-3">
              <Labeled label="Tarih (YYYY-AA-GG)">
                <input
                  className={input}
                  value={nh.date}
                  placeholder="2026-07-12"
                  onChange={(e) => setNh({ ...nh, date: e.target.value })}
                />
              </Labeled>
              <Labeled label="Saat">
                <input className={input} value={nh.time} placeholder="10:30" onChange={(e) => setNh({ ...nh, time: e.target.value })} />
              </Labeled>
              <Labeled label="Tür">
                <input className={input} value={nh.type} placeholder="ön inceleme" onChange={(e) => setNh({ ...nh, type: e.target.value })} />
              </Labeled>
              <button
                onClick={addHearing}
                disabled={busy || !nh.date.trim()}
                className="rounded-lg bg-zinc-900 px-3 py-2 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-40"
              >
                Duruşma ekle
              </button>
            </div>

            <h2 className="mb-2 mt-6 text-sm font-semibold text-zinc-700">Süreler</h2>
            <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200">
              {detail.deadlines.length === 0 && (
                <div className="px-4 py-3 text-sm text-zinc-400">Henüz süre yok.</div>
              )}
              {detail.deadlines.map((d) => (
                <div key={d.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                  <span
                    className={`font-medium ${
                      d.status === "done" ? "text-zinc-400 line-through" : "text-zinc-800"
                    }`}
                  >
                    {d.due_date}
                  </span>
                  <span className="text-zinc-600">{d.title}</span>
                  <span className="text-xs text-zinc-400">{d.type ?? ""}</span>
                  {d.status !== "done" && (
                    <button
                      onClick={() => completeDeadline(d.id)}
                      className="ml-auto rounded-md border border-zinc-200 px-2 py-0.5 text-xs text-zinc-600 hover:bg-zinc-50"
                    >
                      Tamamlandı
                    </button>
                  )}
                </div>
              ))}
            </div>

            <div className="mt-3 flex flex-wrap items-end gap-2 rounded-xl border border-zinc-200 p-3">
              <Labeled label="Son tarih (YYYY-AA-GG)">
                <input className={input} value={nd.due_date} placeholder="2026-07-20" onChange={(e) => setNd({ ...nd, due_date: e.target.value })} />
              </Labeled>
              <Labeled label="Süre adı">
                <input className={input} value={nd.title} placeholder="Cevap süresi" onChange={(e) => setNd({ ...nd, title: e.target.value })} />
              </Labeled>
              <Labeled label="Tür">
                <input className={input} value={nd.type} placeholder="cevap" onChange={(e) => setNd({ ...nd, type: e.target.value })} />
              </Labeled>
              <button
                onClick={addDeadline}
                disabled={busy || !nd.due_date.trim() || !nd.title.trim()}
                className="rounded-lg bg-zinc-900 px-3 py-2 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-40"
              >
                Süre ekle
              </button>
            </div>

            <h2 className="mb-2 mt-6 text-sm font-semibold text-zinc-700">Belgeler</h2>
            <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200">
              {detail.documents.length === 0 && (
                <div className="px-4 py-3 text-sm text-zinc-400">Bu dosyaya bağlı belge yok.</div>
              )}
              {detail.documents.map((d) => (
                <button
                  key={d.id}
                  onClick={() => onOpenSession(d.conversation_id)}
                  className="flex w-full items-center gap-3 px-4 py-2.5 text-left text-sm hover:bg-zinc-50"
                >
                  <span className="text-xs">📄</span>
                  <span className="min-w-0 flex-1 truncate text-zinc-800">{d.title}</span>
                  <span className="shrink-0 text-xs text-zinc-400">{d.status}</span>
                  <span className="shrink-0 text-zinc-300">›</span>
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>
    );
  }

  // ---- List view ----
  return (
    <div className="flex h-dvh flex-col bg-white">
      {header("Dosyalar", onClose)}
      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto max-w-3xl px-4 py-4">
          {error && <div className="mb-3 text-xs text-red-600">{error}</div>}

          {agenda.length > 0 && (
            <div className="mb-4 rounded-xl border border-amber-200 bg-amber-50 p-3">
              <div className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-amber-700">
                Yaklaşan ({agenda.length})
              </div>
              <div className="space-y-0.5">
                {agenda.slice(0, 6).map((a) => (
                  <button
                    key={`${a.kind}-${a.id}`}
                    onClick={() => openMatter(a.matter_id)}
                    className="flex w-full items-center gap-2 rounded-md px-2 py-1 text-left text-sm hover:bg-amber-100/60"
                  >
                    <span className="text-xs">{a.kind === "hearing" ? "⚖️" : "⏰"}</span>
                    <span className="font-medium text-amber-900">
                      {a.date}
                      {a.time ? ` ${a.time}` : ""}
                    </span>
                    <span className="truncate text-amber-800">
                      {a.kind === "deadline" ? a.title : a.type ?? "Duruşma"}
                    </span>
                  </button>
                ))}
              </div>
            </div>
          )}

          {!creating ? (
            <button
              onClick={() => setCreating(true)}
              className="mb-4 rounded-lg bg-zinc-900 px-4 py-2 text-sm font-semibold text-white hover:bg-zinc-700"
            >
              + Yeni dosya
            </button>
          ) : (
            <div className="mb-4 rounded-xl border border-zinc-200 p-4">
              <div className="grid grid-cols-2 gap-3">
                <Labeled label="Başlık *">
                  <input className={input} value={nm.title} placeholder="Ahmet Y. – Boşanma" onChange={(e) => setNm({ ...nm, title: e.target.value })} />
                </Labeled>
                <Labeled label="Müvekkil">
                  <input className={input} value={nm.client_name} onChange={(e) => setNm({ ...nm, client_name: e.target.value })} />
                </Labeled>
                <Labeled label="Mahkeme">
                  <input className={input} value={nm.court} onChange={(e) => setNm({ ...nm, court: e.target.value })} />
                </Labeled>
                <Labeled label="Dava türü">
                  <input className={input} value={nm.case_type} onChange={(e) => setNm({ ...nm, case_type: e.target.value })} />
                </Labeled>
                <Labeled label="Karşı taraf">
                  <input className={input} value={nm.counterparty} onChange={(e) => setNm({ ...nm, counterparty: e.target.value })} />
                </Labeled>
              </div>
              <div className="mt-3 flex gap-2">
                <button
                  onClick={createMatter}
                  disabled={busy || !nm.title.trim()}
                  className="rounded-lg bg-zinc-900 px-4 py-1.5 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-40"
                >
                  Oluştur
                </button>
                <button onClick={() => setCreating(false)} className="rounded-lg border border-zinc-200 px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-50">
                  Vazgeç
                </button>
              </div>
            </div>
          )}

          <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200">
            {matters.length === 0 && (
              <div className="px-4 py-12 text-center text-sm text-zinc-400">Henüz dosya yok.</div>
            )}
            {matters.map((m) => (
              <button
                key={m.id}
                onClick={() => openMatter(m.id)}
                className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-zinc-50"
              >
                <div className="min-w-0 flex-1">
                  <div className="truncate text-sm font-medium text-zinc-800">{m.title}</div>
                  <div className="truncate text-xs text-zinc-400">
                    {[m.client_name, m.court].filter(Boolean).join(" · ") || "—"}
                  </div>
                </div>
                <span
                  className={`shrink-0 rounded px-1.5 py-0.5 text-[10px] ${
                    m.status === "closed" ? "bg-zinc-200 text-zinc-500" : "bg-emerald-500/15 text-emerald-600"
                  }`}
                >
                  {STATUS_LABEL[m.status] ?? m.status}
                </span>
                <span className="shrink-0 text-zinc-300">›</span>
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function Field({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <div>
      <div className="text-[11px] uppercase tracking-wide text-zinc-400">{label}</div>
      <div className="mt-0.5 text-zinc-800">{value || "—"}</div>
    </div>
  );
}

function Labeled({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block flex-1">
      <span className="mb-1 block text-xs font-medium text-zinc-500">{label}</span>
      {children}
    </label>
  );
}
