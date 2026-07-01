import { useCallback, useEffect, useState } from "react";
import {
  api,
  type CalendarEvent,
  type ClientBrief,
  type MatterBrief,
} from "../api";

// Takvim — the office calendar. Every dated thing (duruşma, süre, randevu, hatırlatma) is a single
// CalendarEvent, filtered by category and scoped to one month. This is the month-scoped agenda view;
// the month/week/day grid is a later slice. Linked to a matter and/or client.

const input =
  "rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900";

const CATEGORY_LABEL: Record<string, string> = {
  hearing: "Duruşma",
  deadline: "Süre",
  appointment: "Randevu",
  reminder: "Hatırlatma",
};

const CATEGORY_STYLE: Record<string, string> = {
  hearing: "bg-indigo-100 text-indigo-700",
  deadline: "bg-red-100 text-red-700",
  appointment: "bg-emerald-100 text-emerald-700",
  reminder: "bg-amber-100 text-amber-700",
};

const MONTHS = [
  "Ocak",
  "Şubat",
  "Mart",
  "Nisan",
  "Mayıs",
  "Haziran",
  "Temmuz",
  "Ağustos",
  "Eylül",
  "Ekim",
  "Kasım",
  "Aralık",
];

// A local yyyy-mm-dd (no UTC shift), so day grouping matches what the user typed.
function ymd(y: number, m: number, d: number): string {
  return `${y}-${String(m + 1).padStart(2, "0")}-${String(d).padStart(2, "0")}`;
}

function fmtDay(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  return `${String(d).padStart(2, "0")} ${MONTHS[m - 1]} ${y}`;
}

function fmtTime(startAt: string | null, allDay: boolean): string {
  if (!startAt || allDay) return "Tüm gün";
  const t = startAt.split("T")[1];
  return t ? t.slice(0, 5) : "";
}

const EMPTY_FORM = {
  category: "appointment",
  title: "",
  date: "",
  time: "",
  all_day: false,
  type: "",
  location: "",
  matter_id: "",
  client_id: "",
  notes: "",
};

export function CalendarPanel({ onClose }: { onClose: () => void }) {
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth()); // 0-based
  const [filter, setFilter] = useState("");
  const [items, setItems] = useState<CalendarEvent[]>([]);
  const [matters, setMatters] = useState<MatterBrief[]>([]);
  const [clients, setClients] = useState<ClientBrief[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [f, setF] = useState(EMPTY_FORM);

  const monthRange = useCallback(() => {
    const start = ymd(year, month, 1);
    const lastDay = new Date(year, month + 1, 0).getDate();
    const end = `${ymd(year, month, lastDay)}T23:59:59`;
    return { start, end };
  }, [year, month]);

  const reload = useCallback(() => {
    const { start, end } = monthRange();
    api
      .calendarEvents({ start, end, category: filter || undefined })
      .then((d) => setItems(d.items))
      .catch(() => {});
  }, [monthRange, filter]);

  useEffect(() => {
    reload();
  }, [reload]);

  useEffect(() => {
    Promise.all([api.matters(), api.clients()])
      .then(([m, c]) => {
        setMatters(m.items);
        setClients(c.items);
      })
      .catch(() => {});
  }, []);

  function step(delta: number) {
    const d = new Date(year, month + delta, 1);
    setYear(d.getFullYear());
    setMonth(d.getMonth());
  }

  async function add() {
    const title = f.title.trim();
    if (!title || !f.date) return;
    const start_at = f.all_day || !f.time ? f.date : `${f.date}T${f.time}:00`;
    await api.createCalendarEvent({
      category: f.category,
      title,
      start_at,
      all_day: f.all_day || !f.time,
      type: f.type.trim() || null,
      location: f.location.trim() || null,
      matter_id: f.matter_id ? Number(f.matter_id) : null,
      client_id: f.client_id ? Number(f.client_id) : null,
      notes: f.notes.trim() || null,
    });
    setF({ ...EMPTY_FORM, category: f.category });
    setShowForm(false);
    reload();
  }

  async function complete(ev: CalendarEvent) {
    await api.updateCalendarEvent(ev.id, { status: "done" });
    reload();
  }

  async function remove(id: number) {
    await api.deleteCalendarEvent(id);
    reload();
  }

  // Group the month's events by their day (yyyy-mm-dd) for a clean agenda.
  const byDay = new Map<string, CalendarEvent[]>();
  for (const e of items) {
    const day = e.start_at ? e.start_at.split("T")[0] : "";
    if (!byDay.has(day)) byDay.set(day, []);
    byDay.get(day)!.push(e);
  }
  const days = [...byDay.keys()].sort();

  return (
    <div className="flex h-dvh flex-col bg-white">
      <div className="flex items-center gap-3 border-b border-zinc-200 px-4 py-3">
        <button
          onClick={onClose}
          className="rounded-lg border border-zinc-300 px-3 py-1.5 text-sm text-zinc-700 hover:border-zinc-900"
        >
          Geri
        </button>
        <h1 className="text-lg font-bold text-zinc-900">Takvim</h1>
        <div className="ml-auto flex items-center gap-2">
          <button
            onClick={() => step(-1)}
            className="rounded-md border border-zinc-300 px-2 py-1.5 text-sm text-zinc-700 hover:border-zinc-900"
          >
            ‹
          </button>
          <span className="min-w-32 text-center text-sm font-medium text-zinc-800">
            {MONTHS[month]} {year}
          </span>
          <button
            onClick={() => step(1)}
            className="rounded-md border border-zinc-300 px-2 py-1.5 text-sm text-zinc-700 hover:border-zinc-900"
          >
            ›
          </button>
        </div>
        <select
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          className={`${input} text-zinc-700`}
        >
          <option value="">Tümü</option>
          <option value="hearing">Duruşma</option>
          <option value="deadline">Süre</option>
          <option value="appointment">Randevu</option>
          <option value="reminder">Hatırlatma</option>
        </select>
        <button
          onClick={() => setShowForm((v) => !v)}
          className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90"
        >
          {showForm ? "Kapat" : "Yeni"}
        </button>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto bg-zinc-50">
        <div className="mx-auto max-w-2xl px-4 py-4">
          {showForm && (
            <div className="mb-4 grid grid-cols-2 gap-2 rounded-lg border border-zinc-200 bg-white p-3">
              <select
                value={f.category}
                onChange={(e) => setF({ ...f, category: e.target.value })}
                className={`${input} text-zinc-700`}
              >
                <option value="appointment">Randevu/Görüşme</option>
                <option value="hearing">Duruşma</option>
                <option value="deadline">Süre</option>
                <option value="reminder">Hatırlatma</option>
              </select>
              <input
                value={f.type}
                onChange={(e) => setF({ ...f, type: e.target.value })}
                placeholder="Alt tür (örn. keşif, müvekkil görüşme)"
                className={input}
              />
              <input
                value={f.title}
                onChange={(e) => setF({ ...f, title: e.target.value })}
                placeholder="Başlık"
                className={`${input} col-span-2`}
              />
              <input
                type="date"
                value={f.date}
                onChange={(e) => setF({ ...f, date: e.target.value })}
                className={`${input} text-zinc-700`}
              />
              <div className="flex items-center gap-2">
                <input
                  type="time"
                  value={f.time}
                  onChange={(e) => setF({ ...f, time: e.target.value })}
                  disabled={f.all_day}
                  className={`${input} flex-1 text-zinc-700 disabled:opacity-50`}
                />
                <label className="flex items-center gap-1 text-xs text-zinc-600">
                  <input
                    type="checkbox"
                    checked={f.all_day}
                    onChange={(e) => setF({ ...f, all_day: e.target.checked })}
                  />
                  Tüm gün
                </label>
              </div>
              <select
                value={f.matter_id}
                onChange={(e) => setF({ ...f, matter_id: e.target.value })}
                className={`${input} text-zinc-700`}
              >
                <option value="">Dosya (opsiyonel)</option>
                {matters.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.title}
                  </option>
                ))}
              </select>
              <select
                value={f.client_id}
                onChange={(e) => setF({ ...f, client_id: e.target.value })}
                className={`${input} text-zinc-700`}
              >
                <option value="">Müvekkil (opsiyonel)</option>
                {clients.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.full_name}
                  </option>
                ))}
              </select>
              <input
                value={f.location}
                onChange={(e) => setF({ ...f, location: e.target.value })}
                placeholder="Yer (adliye/ofis/online)"
                className={`${input} col-span-2`}
              />
              <div className="col-span-2 flex justify-end">
                <button
                  onClick={add}
                  disabled={!f.title.trim() || !f.date}
                  className="rounded-md bg-zinc-900 px-4 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
                >
                  Ekle
                </button>
              </div>
            </div>
          )}

          {days.length === 0 ? (
            <p className="text-[13px] text-zinc-400">Bu ay için etkinlik yok.</p>
          ) : (
            <div className="space-y-4">
              {days.map((day) => (
                <div key={day}>
                  <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-zinc-500">
                    {day ? fmtDay(day) : "Tarihsiz"}
                  </div>
                  <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200 bg-white">
                    {byDay.get(day)!.map((e) => (
                      <div key={e.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                        <span className="w-12 shrink-0 text-[12px] text-zinc-500">
                          {fmtTime(e.start_at, e.all_day)}
                        </span>
                        <span
                          className={`shrink-0 rounded-md px-2 py-0.5 text-[11px] ${
                            CATEGORY_STYLE[e.category] ?? "bg-zinc-100 text-zinc-600"
                          }`}
                        >
                          {CATEGORY_LABEL[e.category] ?? e.category}
                        </span>
                        <div className="min-w-0 flex-1">
                          <div
                            className={`truncate ${
                              e.status === "done"
                                ? "text-zinc-400 line-through"
                                : "text-zinc-800"
                            }`}
                          >
                            {e.title}
                          </div>
                          <div className="truncate text-[12px] text-zinc-400">
                            {[e.matter_title, e.client_name, e.location]
                              .filter(Boolean)
                              .join(" · ") || "—"}
                          </div>
                        </div>
                        {e.status !== "done" && (
                          <button
                            onClick={() => complete(e)}
                            className="shrink-0 rounded-md px-2 py-1 text-[12px] text-emerald-600 hover:bg-emerald-50"
                          >
                            Bitti
                          </button>
                        )}
                        <button
                          onClick={() => remove(e.id)}
                          className="shrink-0 rounded-md px-2 py-1 text-[12px] text-red-600 hover:bg-red-50"
                        >
                          Sil
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
