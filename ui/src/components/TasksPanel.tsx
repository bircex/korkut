import { P, useCan } from "../permissions";
import { useEffect, useState } from "react";
import { api, type Member, type Task, type User } from "../api";
import { navigate, useSearch } from "../router";
import { Modal } from "./Modal";

// Görevler — to-dos assigned to office members, optionally due-dated. A first slice: create,
// assign, mark done, delete, and filter open vs. all. The list is already only what the member may
// see (their matters' tasks and the ones naming them); "Kapsam" narrows it further.

type Scope = "" | "bana" | "benim" | "dosya";

const SCOPES: { value: Scope; label: string }[] = [
  { value: "", label: "Görebildiklerim" },
  { value: "bana", label: "Bana atananlar" },
  { value: "benim", label: "Açtıklarım" },
  { value: "dosya", label: "Dosyalarımdakiler" },
];

function inScope(t: Task, scope: Scope, me: string | undefined): boolean {
  if (scope === "bana") return me !== undefined && String(t.assignee_id) === me;
  if (scope === "benim") return me !== undefined && t.created_by === me;
  if (scope === "dosya") return t.matter_id != null;
  return true;
}

const input =
  "rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900";

function fmtDate(iso: string | null): string {
  if (!iso) return "";
  const [y, m, d] = iso.split("-");
  return d && m && y ? `${d}.${m}.${y}` : iso;
}

export function TasksPanel({ user }: { user: User }) {
  const canWrite = useCan(P.agendaWrite);
  // Filtreler URL'de yaşar: /gorevler?tum=1&kapsam=bana.
  const search = useSearch();
  const params = new URLSearchParams(search);
  const showAll = params.has("tum");
  const scope = (SCOPES.find((s) => s.value === params.get("kapsam"))?.value ?? "") as Scope;
  const go = (all: boolean, sc: Scope) => {
    const q = new URLSearchParams();
    if (all) q.set("tum", "1");
    if (sc) q.set("kapsam", sc);
    navigate(q.toString() ? `/gorevler?${q}` : "/gorevler", { replace: true });
  };
  const setShowAll = (v: boolean) => go(v, scope);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [adding, setAdding] = useState(false);
  const [title, setTitle] = useState("");
  const [assignee, setAssignee] = useState("");
  const [due, setDue] = useState("");
  const [time, setTime] = useState("");
  const [location, setLocation] = useState("");

  useEffect(() => {
    api.members().then(setMembers).catch(() => {});
  }, []);

  useEffect(() => {
    api
      .tasks(showAll ? undefined : "open", "gorev")
      .then((d) => setTasks(d.items))
      .catch(() => {});
  }, [showAll]);

  function reload() {
    api
      .tasks(showAll ? undefined : "open", "gorev")
      .then((d) => setTasks(d.items))
      .catch(() => {});
  }

  const shown = tasks.filter((t) => inScope(t, scope, user.id));

  async function add() {
    const t = title.trim();
    if (!t) return;
    const allDay = !due || !time;
    await api.createTask({
      category: "gorev",
      title: t,
      assignee_id: assignee ? Number(assignee) : null,
      due_date: due ? (allDay ? due : `${due}T${time}:00`) : null,
      all_day: allDay,
      location: location.trim() || null,
    });
    setTitle("");
    setAssignee("");
    setDue("");
    setTime("");
    setLocation("");
    setAdding(false);
    reload();
  }

  async function toggleDone(task: Task) {
    await api.updateTask(task.id, { status: task.status === "done" ? "open" : "done" });
    reload();
  }

  async function remove(id: number) {
    await api.deleteTask(id);
    reload();
  }

  return (
    <div className="flex h-full min-w-0 flex-1 flex-col bg-white">
      <div className="flex items-center gap-3 border-b border-zinc-200 px-4 py-3">
        <h1 className="text-lg font-bold text-zinc-900">Görevler</h1>
        <label className="ml-auto flex items-center gap-1.5 text-[13px] text-zinc-600">
          Kapsam
          <select
            value={scope}
            onChange={(e) => go(showAll, e.target.value as Scope)}
            className="rounded-md border border-zinc-300 bg-white px-2 py-1 text-[13px] text-zinc-800 outline-none focus:border-zinc-900"
          >
            {SCOPES.map((s) => (
              <option key={s.value} value={s.value}>
                {s.label}
              </option>
            ))}
          </select>
        </label>
        <label className="flex items-center gap-1.5 text-[13px] text-zinc-600">
          <input type="checkbox" checked={showAll} onChange={(e) => setShowAll(e.target.checked)} />
          Tamamlananları da göster
        </label>
        {canWrite && (
          <button
            onClick={() => setAdding(true)}
            className="rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90"
          >
            + Yeni görev
          </button>
        )}
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto max-w-2xl px-4 py-4">
          {shown.length === 0 ? (
            <p className="text-[13px] text-zinc-400">Görev yok.</p>
          ) : (
            <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200">
              {shown.map((t) => (
                <div key={t.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                  <input
                    type="checkbox"
                    checked={t.status === "done"}
                    onChange={() => toggleDone(t)}
                    disabled={!canWrite}
                    aria-label={`${t.title}: tamamlandı`}
                    className="shrink-0"
                  />
                  <div className="min-w-0 flex-1">
                    <div
                      className={
                        "truncate " +
                        (t.status === "done" ? "text-zinc-400 line-through" : "text-zinc-800")
                      }
                    >
                      {t.title}
                    </div>
                    <div className="truncate text-[12px] text-zinc-400">
                      {[t.assignee_name, fmtDate(t.due_date)].filter(Boolean).join(" · ") || "—"}
                    </div>
                  </div>
                  {canWrite && (
                    <button
                      onClick={() => remove(t.id)}
                      className="shrink-0 rounded-md px-2 py-1 text-[12px] text-red-600 hover:bg-red-50"
                    >
                      Sil
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {adding && (
        <Modal
          title="Yeni görev"
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
                onClick={add}
                disabled={!title.trim()}
                className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
              >
                Ekle
              </button>
            </>
          }
        >
          <div className="flex flex-col gap-3">
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Görev
              <input
                autoFocus
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="ör. cevap dilekçesi yaz"
                className={input}
              />
            </label>
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Atanan (opsiyonel)
              <select
                value={assignee}
                onChange={(e) => setAssignee(e.target.value)}
                className={`${input} text-zinc-700`}
              >
                <option value="">—</option>
                {members.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.full_name ?? m.username}
                  </option>
                ))}
              </select>
            </label>
            <div className="flex gap-3">
              <label className="flex flex-1 flex-col gap-1 text-[13px] text-zinc-600">
                Son tarih (opsiyonel)
                <input
                  type="date"
                  value={due}
                  onChange={(e) => setDue(e.target.value)}
                  className={`${input} text-zinc-700`}
                />
              </label>
              <label className="flex w-28 flex-col gap-1 text-[13px] text-zinc-600">
                Saat
                <input
                  type="time"
                  value={time}
                  onChange={(e) => setTime(e.target.value)}
                  disabled={!due}
                  className={`${input} text-zinc-700 disabled:opacity-50`}
                />
              </label>
            </div>
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Yer (opsiyonel)
              <input
                value={location}
                onChange={(e) => setLocation(e.target.value)}
                placeholder="ör. adliye, ofis, online"
                className={input}
              />
            </label>
          </div>
        </Modal>
      )}
    </div>
  );
}
