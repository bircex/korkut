import { useEffect, useState } from "react";
import { api, type Member, type Task } from "../api";
import { Modal } from "./Modal";

// Görevler — to-dos assigned to office members, optionally due-dated. A first slice: create,
// assign, mark done, delete, and filter open vs. all.

const input =
  "rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900";

function fmtDate(iso: string | null): string {
  if (!iso) return "";
  const [y, m, d] = iso.split("-");
  return d && m && y ? `${d}.${m}.${y}` : iso;
}

export function TasksPanel() {
  const [showAll, setShowAll] = useState(false);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [adding, setAdding] = useState(false);
  const [title, setTitle] = useState("");
  const [assignee, setAssignee] = useState("");
  const [due, setDue] = useState("");

  useEffect(() => {
    api.members().then(setMembers).catch(() => {});
  }, []);

  useEffect(() => {
    api
      .tasks(showAll ? undefined : "open")
      .then((d) => setTasks(d.items))
      .catch(() => {});
  }, [showAll]);

  function reload() {
    api
      .tasks(showAll ? undefined : "open")
      .then((d) => setTasks(d.items))
      .catch(() => {});
  }

  async function add() {
    const t = title.trim();
    if (!t) return;
    await api.createTask({
      title: t,
      assignee_id: assignee ? Number(assignee) : null,
      due_date: due || null,
    });
    setTitle("");
    setAssignee("");
    setDue("");
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
          <input type="checkbox" checked={showAll} onChange={(e) => setShowAll(e.target.checked)} />
          Tamamlananları da göster
        </label>
        <button
          onClick={() => setAdding(true)}
          className="rounded-md bg-zinc-900 px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90"
        >
          + Yeni görev
        </button>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto max-w-2xl px-4 py-4">
          {tasks.length === 0 ? (
            <p className="text-[13px] text-zinc-400">Görev yok.</p>
          ) : (
            <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200">
              {tasks.map((t) => (
                <div key={t.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                  <input
                    type="checkbox"
                    checked={t.status === "done"}
                    onChange={() => toggleDone(t)}
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
                  <button
                    onClick={() => remove(t.id)}
                    className="shrink-0 rounded-md px-2 py-1 text-[12px] text-red-600 hover:bg-red-50"
                  >
                    Sil
                  </button>
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
            <label className="flex flex-col gap-1 text-[13px] text-zinc-600">
              Son tarih (opsiyonel)
              <input
                type="date"
                value={due}
                onChange={(e) => setDue(e.target.value)}
                className={`${input} text-zinc-700`}
              />
            </label>
          </div>
        </Modal>
      )}
    </div>
  );
}
