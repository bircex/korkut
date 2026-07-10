import { useCallback, useEffect, useState } from "react";
import { api, type AgendaItem, type MatterBrief, type MatterDetail, type User } from "../api";
import { ClientDetailsCard } from "./ClientDetailsCard";
import { ArrowLeft, CaretRight } from "../icons";
import { matchPath, navigate, usePath } from "../router";
import { Modal } from "./Modal";

// Minimal "Dosyalar" (matters) workspace: browse the office's case files, open one to see its
// details + hearings, create a matter, add a hearing, and start a chat filed under the matter.
// Full-screen panel like Settings/Customize; reached from the user menu.

const input =
  "w-full rounded-lg border border-zinc-200 px-3 py-2 text-sm outline-none focus:border-zinc-400";

const STATUS_LABEL: Record<string, string> = { open: "Açık", closed: "Kapalı" };

export function MattersPanel({
  user,
  onOpenSession,
}: {
  user: User;
  onOpenSession: (sessionId: number) => void;
}) {
  const [matters, setMatters] = useState<MatterBrief[]>([]);
  const [agenda, setAgenda] = useState<AgendaItem[]>([]);
  const [detail, setDetail] = useState<MatterDetail | null>(null); // /dosyalar/:id yüklenince dolar
  const path = usePath();
  // Açık dosya URL'den okunur: /dosyalar → liste, /dosyalar/:id → detay.
  const midParam = matchPath("/dosyalar/:id", path)?.id;
  const routeMid = midParam !== undefined && Number.isFinite(Number(midParam)) ? Number(midParam) : null;
  const [adding, setAdding] = useState<"matter" | "hearing" | "deadline" | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // New-matter form.
  const [nm, setNm] = useState({ title: "", client_name: "", court: "", case_type: "", counterparty: "" });
  // Add-hearing / add-deadline forms (within a detail).
  const [nh, setNh] = useState({ date: "", time: "", type: "", location: "" });
  const [nd, setNd] = useState({ due_date: "", title: "", type: "" });

  const refresh = useCallback(
    () =>
      Promise.all([api.matters(), api.agenda(30)])
        .then(([m, a]) => {
          setMatters(m.items);
          setAgenda(a.items);
        })
        .catch(() => setError("Dosyalar yüklenemedi.")),
    [],
  );

  useEffect(() => {
    refresh();
  }, [refresh]);

  // Listeye dönüşte (URL /dosyalar olunca) detay kapanır — render sırasında state uyarlama deseni.
  const [viewedMid, setViewedMid] = useState<number | null>(routeMid);
  if (routeMid !== viewedMid) {
    setViewedMid(routeMid);
    if (routeMid === null) setDetail(null);
  }

  // URL'deki dosyayı yükle — tıklama, reload ve back/forward hep buradan geçer.
  useEffect(() => {
    if (routeMid === null || detail?.id === routeMid) return;
    api
      .getMatter(routeMid)
      .then((d) => {
        setDetail(d);
        setError(null);
      })
      .catch(() => setError("Dosya açılamadı."));
  }, [routeMid, detail?.id]);

  function openMatter(id: number) {
    navigate(`/dosyalar/${id}`); // yükleme URL efektinde
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
      setAdding(null);
      await refresh();
      setDetail(d); // navigate'ten önce — URL efekti aynı dosyayı yeniden çekmesin
      navigate(`/dosyalar/${d.id}`);
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
      setAdding(null);
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
      setAdding(null);
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
    navigate("/dosyalar", { replace: true }); // silinen dosyanın URL'i geride kalmasın
    refresh();
  }

  async function openChat() {
    if (!detail) return;
    const sess = await api.createSession(detail.id);
    onOpenSession(sess.id);
  }

  // ---- Detail view ----
  if (detail) {
    return (
      <div className="flex h-full min-w-0 flex-1 flex-col bg-white">
        <header className="flex items-center gap-3 border-b border-zinc-200 px-4 py-3">
          <a
            href="/dosyalar"
            onClick={(e) => {
              e.preventDefault();
              navigate("/dosyalar");
            }}
            className="flex items-center gap-1 text-sm text-zinc-500 hover:text-zinc-900"
          >
            <ArrowLeft size={14} aria-hidden /> Dosyalar
          </a>
          <h1 className="text-base font-semibold text-zinc-900">{detail.title}</h1>
          <span className="ml-auto text-xs text-zinc-400">{user.office ?? ""}</span>
        </header>
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

            {detail.client && (
              <ClientDetailsCard
                client={detail.client}
                onSaved={(c) => setDetail({ ...detail, client: c })}
              />
            )}

            <div className="mb-2 mt-6 flex items-center gap-3">
              <h2 className="text-sm font-semibold text-zinc-700">Duruşmalar</h2>
              <button
                onClick={() => setAdding("hearing")}
                className="ml-auto rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90"
              >
                + Duruşma
              </button>
            </div>
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

            <div className="mb-2 mt-6 flex items-center gap-3">
              <h2 className="text-sm font-semibold text-zinc-700">Süreler</h2>
              <button
                onClick={() => setAdding("deadline")}
                className="ml-auto rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90"
              >
                + Süre
              </button>
            </div>
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
                  <span className="min-w-0 flex-1 truncate text-zinc-800">{d.title}</span>
                  <span className="shrink-0 text-xs text-zinc-400">{d.status}</span>
                  <CaretRight size={14} aria-hidden className="shrink-0 text-zinc-300" />
                </button>
              ))}
            </div>
          </div>
        </div>

        {adding === "hearing" && (
          <Modal
            title="Duruşma ekle"
            onClose={() => setAdding(null)}
            footer={
              <>
                <button
                  onClick={() => setAdding(null)}
                  className="rounded-md px-3 py-2 text-sm font-medium text-zinc-600 hover:bg-zinc-100"
                >
                  Vazgeç
                </button>
                <button
                  onClick={addHearing}
                  disabled={busy || !nh.date.trim()}
                  className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
                >
                  Ekle
                </button>
              </>
            }
          >
            <div className="flex flex-col gap-3">
              <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
                Tarih (YYYY-AA-GG)
                <input
                  autoFocus
                  className={input}
                  value={nh.date}
                  placeholder="2026-07-12"
                  onChange={(e) => setNh({ ...nh, date: e.target.value })}
                />
              </label>
              <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
                Saat
                <input
                  className={input}
                  value={nh.time}
                  placeholder="10:30"
                  onChange={(e) => setNh({ ...nh, time: e.target.value })}
                />
              </label>
              <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
                Tür
                <input
                  className={input}
                  value={nh.type}
                  placeholder="ön inceleme"
                  onChange={(e) => setNh({ ...nh, type: e.target.value })}
                />
              </label>
            </div>
          </Modal>
        )}

        {adding === "deadline" && (
          <Modal
            title="Süre ekle"
            onClose={() => setAdding(null)}
            footer={
              <>
                <button
                  onClick={() => setAdding(null)}
                  className="rounded-md px-3 py-2 text-sm font-medium text-zinc-600 hover:bg-zinc-100"
                >
                  Vazgeç
                </button>
                <button
                  onClick={addDeadline}
                  disabled={busy || !nd.due_date.trim() || !nd.title.trim()}
                  className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
                >
                  Ekle
                </button>
              </>
            }
          >
            <div className="flex flex-col gap-3">
              <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
                Son tarih (YYYY-AA-GG)
                <input
                  autoFocus
                  className={input}
                  value={nd.due_date}
                  placeholder="2026-07-20"
                  onChange={(e) => setNd({ ...nd, due_date: e.target.value })}
                />
              </label>
              <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
                Süre adı
                <input
                  className={input}
                  value={nd.title}
                  placeholder="Cevap süresi"
                  onChange={(e) => setNd({ ...nd, title: e.target.value })}
                />
              </label>
              <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
                Tür
                <input
                  className={input}
                  value={nd.type}
                  placeholder="cevap"
                  onChange={(e) => setNd({ ...nd, type: e.target.value })}
                />
              </label>
            </div>
          </Modal>
        )}
      </div>
    );
  }

  // ---- List view ----
  // uioption varyantlarının paylaştığı parçalar: durum rozeti, ajanda kartı, satır listesi.
  const statusBadge = (m: MatterBrief) => (
    <span
      className={`shrink-0 rounded px-1.5 py-0.5 text-[10px] ${
        m.status === "closed" ? "bg-zinc-200 text-zinc-500" : "bg-emerald-500/15 text-emerald-600"
      }`}
    >
      {STATUS_LABEL[m.status] ?? m.status}
    </span>
  );

  const agendaCard = (limit: number) =>
    agenda.length > 0 && (
      <div className="mb-4 rounded-xl border border-amber-200 bg-amber-50 p-3">
        <div className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-amber-700">
          Yaklaşan ({agenda.length})
        </div>
        <div className="space-y-0.5">
          {agenda.slice(0, limit).map((a) => (
            <button
              key={`${a.kind}-${a.id}`}
              onClick={() => openMatter(a.matter_id)}
              className="flex w-full items-center gap-2 rounded-md px-2 py-1 text-left text-sm hover:bg-amber-100/60"
            >
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
    );

  const errorNote = error && <div className="mb-3 text-xs text-red-600">{error}</div>;

  const matterTable = () =>
    matters.length === 0 ? (
      <div className="px-4 py-12 text-center text-sm text-zinc-400">Henüz dosya yok.</div>
    ) : (
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-zinc-200 text-left text-[11px] font-semibold uppercase tracking-wide text-zinc-400">
            <th className="py-2 pr-3 font-semibold">Dosya</th>
            <th className="hidden py-2 pr-3 font-semibold sm:table-cell">Müvekkil</th>
            <th className="hidden py-2 pr-3 font-semibold md:table-cell">Mahkeme</th>
            <th className="hidden py-2 pr-3 font-semibold lg:table-cell">Tür</th>
            <th className="py-2 font-semibold">Durum</th>
          </tr>
        </thead>
        <tbody>
          {matters.map((m) => (
            <tr
              key={m.id}
              onClick={() => openMatter(m.id)}
              className="cursor-pointer border-b border-zinc-100 hover:bg-zinc-50"
            >
              <td className="py-2.5 pr-3">
                <div className="font-medium text-zinc-800">{m.title}</div>
                {m.file_no && <div className="text-xs text-zinc-400">{m.file_no}</div>}
              </td>
              <td className="hidden py-2.5 pr-3 text-zinc-600 sm:table-cell">
                {m.client_name ?? "—"}
              </td>
              <td className="hidden py-2.5 pr-3 text-zinc-500 md:table-cell">{m.court ?? "—"}</td>
              <td className="hidden py-2.5 pr-3 text-zinc-500 lg:table-cell">
                {m.case_type ?? "—"}
              </td>
              <td className="py-2.5">{statusBadge(m)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    );

  return (
    <div className="flex h-full min-w-0 flex-1 flex-col bg-white">
      <header className="flex items-center gap-3 border-b border-zinc-200 px-4 py-3">
        <h1 className="text-lg font-bold text-zinc-900">Dosyalar</h1>
        <span className="ml-auto text-xs text-zinc-400">{user.office ?? ""}</span>
        <button
          onClick={() => setAdding("matter")}
          className="rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90"
        >
          + Yeni dosya
        </button>
      </header>
      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto flex max-w-5xl items-start gap-6 px-4 py-4">
          <div className="min-w-0 flex-1">
            {errorNote}
            {matterTable()}
          </div>
          <aside className="sticky top-4 hidden w-72 shrink-0 lg:block">
            {agenda.length > 0 ? (
              agendaCard(10)
            ) : (
              <div className="rounded-xl border border-zinc-200 p-3 text-center text-xs text-zinc-400">
                Yaklaşan duruşma/süre yok.
              </div>
            )}
          </aside>
        </div>
      </div>

      {adding === "matter" && (
        <Modal
          title="Yeni dosya"
          onClose={() => setAdding(null)}
          footer={
            <>
              <button
                onClick={() => setAdding(null)}
                className="rounded-md px-3 py-2 text-sm font-medium text-zinc-600 hover:bg-zinc-100"
              >
                Vazgeç
              </button>
              <button
                onClick={createMatter}
                disabled={busy || !nm.title.trim()}
                className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
              >
                Ekle
              </button>
            </>
          }
        >
          <div className="flex flex-col gap-3">
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Başlık *
              <input
                autoFocus
                className={input}
                value={nm.title}
                placeholder="Ahmet Y. – Boşanma"
                onChange={(e) => setNm({ ...nm, title: e.target.value })}
              />
            </label>
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Müvekkil
              <input
                className={input}
                value={nm.client_name}
                onChange={(e) => setNm({ ...nm, client_name: e.target.value })}
              />
            </label>
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Mahkeme
              <input
                className={input}
                value={nm.court}
                onChange={(e) => setNm({ ...nm, court: e.target.value })}
              />
            </label>
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Dava türü
              <input
                className={input}
                value={nm.case_type}
                onChange={(e) => setNm({ ...nm, case_type: e.target.value })}
              />
            </label>
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Karşı taraf
              <input
                className={input}
                value={nm.counterparty}
                onChange={(e) => setNm({ ...nm, counterparty: e.target.value })}
              />
            </label>
          </div>
        </Modal>
      )}
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
