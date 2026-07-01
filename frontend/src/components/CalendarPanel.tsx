import { useCallback, useEffect, useState } from "react";
import { api, type CalendarEvent, type ClientBrief, type MatterBrief } from "../api";

// Takvim — the office calendar. Every dated thing (duruşma, süre, randevu, hatırlatma) is a single
// CalendarEvent, linked to a matter and/or client. Four views over the same data: month grid, week,
// day, and a flat agenda. The visible range drives what is fetched.

const input =
  "rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-zinc-900";

type Mode = "month" | "week" | "day" | "agenda";

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

const CATEGORY_DOT: Record<string, string> = {
  hearing: "bg-indigo-500",
  deadline: "bg-red-500",
  appointment: "bg-emerald-500",
  reminder: "bg-amber-500",
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
const WEEKDAYS = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];
const WEEKDAYS_LONG = ["Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar"];

// --- local date helpers (never touch UTC, so a day is the day the user sees) ---
function startOfDay(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate());
}
function addDays(d: Date, n: number): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate() + n);
}
function mondayOf(d: Date): Date {
  const offset = (d.getDay() + 6) % 7; // JS: 0=Sun → treat Monday as week start
  return addDays(startOfDay(d), -offset);
}
function ymdOf(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(
    d.getDate(),
  ).padStart(2, "0")}`;
}
function fmtDayLabel(iso: string): string {
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
  repeat: "", // "" | daily | weekly | monthly
  until: "",
};

const FREQ_BY_REPEAT: Record<string, string> = {
  daily: "DAILY",
  weekly: "WEEKLY",
  monthly: "MONTHLY",
};

// Build an RFC-5545 subset RRULE from the form's repeat + until, or null when non-recurring.
function buildRrule(repeat: string, until: string): string | null {
  const freq = FREQ_BY_REPEAT[repeat];
  if (!freq) return null;
  let rule = `FREQ=${freq};INTERVAL=1`;
  if (until) rule += `;UNTIL=${until}`;
  return rule;
}

export function CalendarPanel({ onClose }: { onClose: () => void }) {
  const [mode, setMode] = useState<Mode>("month");
  const [cursor, setCursor] = useState<Date>(startOfDay(new Date()));
  const [filter, setFilter] = useState("");
  const [items, setItems] = useState<CalendarEvent[]>([]);
  const [matters, setMatters] = useState<MatterBrief[]>([]);
  const [clients, setClients] = useState<ClientBrief[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [f, setF] = useState(EMPTY_FORM);

  const todayYmd = ymdOf(new Date());

  // The [start, end] day span the current view needs, as Date objects.
  const range = useCallback((): { start: Date; end: Date } => {
    if (mode === "day") return { start: cursor, end: cursor };
    if (mode === "week") {
      const s = mondayOf(cursor);
      return { start: s, end: addDays(s, 6) };
    }
    // month & agenda both span the whole visible month (month adds the padding weeks).
    const first = new Date(cursor.getFullYear(), cursor.getMonth(), 1);
    const last = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 0);
    if (mode === "agenda") return { start: first, end: last };
    return { start: mondayOf(first), end: addDays(mondayOf(first), 41) }; // 6-week grid
  }, [mode, cursor]);

  const reload = useCallback(() => {
    const { start, end } = range();
    api
      .calendarEvents({
        start: `${ymdOf(start)}T00:00:00`,
        end: `${ymdOf(end)}T23:59:59`,
        category: filter || undefined,
      })
      .then((d) => setItems(d.items))
      .catch(() => {});
  }, [range, filter]);

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
    if (mode === "day") setCursor((c) => addDays(c, delta));
    else if (mode === "week") setCursor((c) => addDays(c, delta * 7));
    else setCursor((c) => new Date(c.getFullYear(), c.getMonth() + delta, 1));
  }

  function openFormOn(day?: string) {
    setF({ ...EMPTY_FORM, date: day ?? "" });
    setShowForm(true);
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
      rrule: buildRrule(f.repeat, f.until),
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

  // Group the fetched events by their day for quick per-cell lookup.
  const byDay = new Map<string, CalendarEvent[]>();
  for (const e of items) {
    const day = e.start_at ? e.start_at.split("T")[0] : "";
    if (!byDay.has(day)) byDay.set(day, []);
    byDay.get(day)!.push(e);
  }
  for (const list of byDay.values()) {
    list.sort((a, b) => (a.start_at ?? "").localeCompare(b.start_at ?? ""));
  }

  const label =
    mode === "day"
      ? `${fmtDayLabel(ymdOf(cursor))} ${WEEKDAYS_LONG[(cursor.getDay() + 6) % 7]}`
      : mode === "week"
        ? (() => {
            const s = mondayOf(cursor);
            const e = addDays(s, 6);
            return `${String(s.getDate()).padStart(2, "0")} ${MONTHS[s.getMonth()]} – ${String(
              e.getDate(),
            ).padStart(2, "0")} ${MONTHS[e.getMonth()]} ${e.getFullYear()}`;
          })()
        : `${MONTHS[cursor.getMonth()]} ${cursor.getFullYear()}`;

  return (
    <div className="flex h-dvh flex-col bg-white">
      <div className="flex flex-wrap items-center gap-2 border-b border-zinc-200 px-4 py-3">
        <button
          onClick={onClose}
          className="rounded-lg border border-zinc-300 px-3 py-1.5 text-sm text-zinc-700 hover:border-zinc-900"
        >
          Geri
        </button>
        <h1 className="text-lg font-bold text-zinc-900">Takvim</h1>

        <div className="ml-2 flex overflow-hidden rounded-lg border border-zinc-300 text-sm">
          {(
            [
              ["month", "Ay"],
              ["week", "Hafta"],
              ["day", "Gün"],
              ["agenda", "Ajanda"],
            ] as [Mode, string][]
          ).map(([m, lbl]) => (
            <button
              key={m}
              onClick={() => setMode(m)}
              className={`px-3 py-1.5 ${
                mode === m ? "bg-zinc-900 text-white" : "bg-white text-zinc-700 hover:bg-zinc-50"
              }`}
            >
              {lbl}
            </button>
          ))}
        </div>

        <div className="ml-auto flex items-center gap-1">
          <button
            onClick={() => step(-1)}
            className="rounded-md border border-zinc-300 px-2 py-1.5 text-sm text-zinc-700 hover:border-zinc-900"
          >
            ‹
          </button>
          <button
            onClick={() => setCursor(startOfDay(new Date()))}
            className="rounded-md border border-zinc-300 px-2 py-1.5 text-sm text-zinc-700 hover:border-zinc-900"
          >
            Bugün
          </button>
          <button
            onClick={() => step(1)}
            className="rounded-md border border-zinc-300 px-2 py-1.5 text-sm text-zinc-700 hover:border-zinc-900"
          >
            ›
          </button>
        </div>
        <span className="min-w-40 text-sm font-medium text-zinc-800">{label}</span>

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
          onClick={() => (showForm ? setShowForm(false) : openFormOn(ymdOf(cursor)))}
          className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90"
        >
          {showForm ? "Kapat" : "Yeni"}
        </button>
      </div>

      {showForm && (
        <div className="border-b border-zinc-200 bg-zinc-50 px-4 py-3">
          <div className="mx-auto grid max-w-3xl grid-cols-2 gap-2 sm:grid-cols-4">
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
              value={f.title}
              onChange={(e) => setF({ ...f, title: e.target.value })}
              placeholder="Başlık"
              className={`${input} col-span-2`}
            />
            <input
              value={f.type}
              onChange={(e) => setF({ ...f, type: e.target.value })}
              placeholder="Alt tür (keşif, görüşme…)"
              className={input}
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
            <select
              value={f.repeat}
              onChange={(e) => setF({ ...f, repeat: e.target.value })}
              className={`${input} text-zinc-700`}
            >
              <option value="">Tekrar yok</option>
              <option value="daily">Her gün</option>
              <option value="weekly">Her hafta</option>
              <option value="monthly">Her ay</option>
            </select>
            <input
              type="date"
              value={f.until}
              onChange={(e) => setF({ ...f, until: e.target.value })}
              disabled={!f.repeat}
              title="Tekrar bitiş tarihi (opsiyonel)"
              className={`${input} text-zinc-700 disabled:opacity-50`}
            />
            <div className="col-span-2 flex justify-end sm:col-span-4">
              <button
                onClick={add}
                disabled={!f.title.trim() || !f.date}
                className="rounded-md bg-zinc-900 px-4 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
              >
                Ekle
              </button>
            </div>
          </div>
        </div>
      )}

      <div className="min-h-0 flex-1 overflow-y-auto bg-zinc-50">
        {mode === "month" && (
          <MonthGrid
            cursor={cursor}
            todayYmd={todayYmd}
            byDay={byDay}
            onPickDay={(day) => {
              setCursor(new Date(Number(day.slice(0, 4)), Number(day.slice(5, 7)) - 1, Number(day.slice(8, 10))));
              setMode("day");
            }}
            onAdd={openFormOn}
          />
        )}
        {mode === "week" && (
          <WeekView cursor={cursor} todayYmd={todayYmd} byDay={byDay} onAdd={openFormOn} />
        )}
        {mode === "day" && (
          <DayView
            day={ymdOf(cursor)}
            events={byDay.get(ymdOf(cursor)) ?? []}
            onComplete={complete}
            onRemove={remove}
          />
        )}
        {mode === "agenda" && (
          <AgendaView byDay={byDay} onComplete={complete} onRemove={remove} />
        )}
      </div>
    </div>
  );
}

// ---------- month grid ----------

function MonthGrid({
  cursor,
  todayYmd,
  byDay,
  onPickDay,
  onAdd,
}: {
  cursor: Date;
  todayYmd: string;
  byDay: Map<string, CalendarEvent[]>;
  onPickDay: (day: string) => void;
  onAdd: (day: string) => void;
}) {
  const first = new Date(cursor.getFullYear(), cursor.getMonth(), 1);
  const gridStart = mondayOf(first);
  const days = Array.from({ length: 42 }, (_, i) => addDays(gridStart, i));
  return (
    <div className="flex h-full flex-col">
      <div className="grid grid-cols-7 border-b border-zinc-200 bg-white text-center text-[11px] font-semibold uppercase tracking-wide text-zinc-500">
        {WEEKDAYS.map((w) => (
          <div key={w} className="py-2">
            {w}
          </div>
        ))}
      </div>
      <div className="grid flex-1 grid-cols-7 grid-rows-6">
        {days.map((d) => {
          const iso = ymdOf(d);
          const inMonth = d.getMonth() === cursor.getMonth();
          const evs = byDay.get(iso) ?? [];
          return (
            <div
              key={iso}
              className={`group min-h-20 border-b border-r border-zinc-200 p-1 ${
                inMonth ? "bg-white" : "bg-zinc-50"
              }`}
            >
              <div className="flex items-center justify-between">
                <button
                  onClick={() => onPickDay(iso)}
                  className={`flex h-6 w-6 items-center justify-center rounded-full text-[12px] ${
                    iso === todayYmd
                      ? "bg-zinc-900 font-semibold text-white"
                      : inMonth
                        ? "text-zinc-700 hover:bg-zinc-100"
                        : "text-zinc-400 hover:bg-zinc-100"
                  }`}
                >
                  {d.getDate()}
                </button>
                <button
                  onClick={() => onAdd(iso)}
                  className="hidden text-[13px] text-zinc-400 hover:text-zinc-900 group-hover:block"
                  title="Etkinlik ekle"
                >
                  +
                </button>
              </div>
              <div className="mt-0.5 space-y-0.5">
                {evs.slice(0, 3).map((e) => (
                  <button
                    key={e.id}
                    onClick={() => onPickDay(iso)}
                    className="flex w-full items-center gap-1 truncate rounded px-1 py-0.5 text-left text-[11px] hover:bg-zinc-100"
                  >
                    <span
                      className={`h-1.5 w-1.5 shrink-0 rounded-full ${
                        CATEGORY_DOT[e.category] ?? "bg-zinc-400"
                      }`}
                    />
                    <span
                      className={`truncate ${
                        e.status === "done" ? "text-zinc-400 line-through" : "text-zinc-700"
                      }`}
                    >
                      {!e.all_day && fmtTime(e.start_at, false) !== ""
                        ? `${fmtTime(e.start_at, false)} `
                        : ""}
                      {e.recurring ? "↻ " : ""}
                      {e.title}
                    </span>
                  </button>
                ))}
                {evs.length > 3 && (
                  <button
                    onClick={() => onPickDay(iso)}
                    className="px-1 text-[11px] text-zinc-400 hover:text-zinc-700"
                  >
                    +{evs.length - 3} daha
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ---------- week view (7 day columns, time-ordered lists) ----------

function WeekView({
  cursor,
  todayYmd,
  byDay,
  onAdd,
}: {
  cursor: Date;
  todayYmd: string;
  byDay: Map<string, CalendarEvent[]>;
  onAdd: (day: string) => void;
}) {
  const start = mondayOf(cursor);
  const days = Array.from({ length: 7 }, (_, i) => addDays(start, i));
  return (
    <div className="grid grid-cols-1 gap-px bg-zinc-200 sm:grid-cols-7">
      {days.map((d, i) => {
        const iso = ymdOf(d);
        const evs = byDay.get(iso) ?? [];
        return (
          <div key={iso} className="min-h-40 bg-white p-2">
            <div className="mb-2 flex items-center justify-between">
              <div className="text-[12px]">
                <span className="font-semibold text-zinc-700">{WEEKDAYS[i]}</span>{" "}
                <span className={iso === todayYmd ? "font-bold text-zinc-900" : "text-zinc-400"}>
                  {d.getDate()}
                </span>
              </div>
              <button
                onClick={() => onAdd(iso)}
                className="text-[13px] text-zinc-400 hover:text-zinc-900"
                title="Etkinlik ekle"
              >
                +
              </button>
            </div>
            <div className="space-y-1">
              {evs.length === 0 ? (
                <p className="text-[11px] text-zinc-300">—</p>
              ) : (
                evs.map((e) => (
                  <div
                    key={e.id}
                    className={`rounded px-1.5 py-1 text-[11px] ${
                      CATEGORY_STYLE[e.category] ?? "bg-zinc-100 text-zinc-600"
                    }`}
                  >
                    <div className="font-medium">{fmtTime(e.start_at, e.all_day)}</div>
                    <div
                      className={`truncate ${e.status === "done" ? "line-through opacity-60" : ""}`}
                    >
                      {e.title}
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}

// ---------- day view ----------

function DayView({
  day,
  events,
  onComplete,
  onRemove,
}: {
  day: string;
  events: CalendarEvent[];
  onComplete: (e: CalendarEvent) => void;
  onRemove: (id: number) => void;
}) {
  return (
    <div className="mx-auto max-w-2xl px-4 py-4">
      <div className="mb-3 text-sm font-semibold text-zinc-700">{fmtDayLabel(day)}</div>
      {events.length === 0 ? (
        <p className="text-[13px] text-zinc-400">Bu gün için etkinlik yok.</p>
      ) : (
        <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200 bg-white">
          {events.map((e) => (
            <EventRow key={e.id} e={e} onComplete={onComplete} onRemove={onRemove} />
          ))}
        </div>
      )}
    </div>
  );
}

// ---------- agenda (flat list grouped by day) ----------

function AgendaView({
  byDay,
  onComplete,
  onRemove,
}: {
  byDay: Map<string, CalendarEvent[]>;
  onComplete: (e: CalendarEvent) => void;
  onRemove: (id: number) => void;
}) {
  const days = [...byDay.keys()].filter(Boolean).sort();
  return (
    <div className="mx-auto max-w-2xl px-4 py-4">
      {days.length === 0 ? (
        <p className="text-[13px] text-zinc-400">Bu ay için etkinlik yok.</p>
      ) : (
        <div className="space-y-4">
          {days.map((day) => (
            <div key={day}>
              <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-zinc-500">
                {fmtDayLabel(day)}
              </div>
              <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200 bg-white">
                {byDay.get(day)!.map((e) => (
                  <EventRow key={e.id} e={e} onComplete={onComplete} onRemove={onRemove} />
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function EventRow({
  e,
  onComplete,
  onRemove,
}: {
  e: CalendarEvent;
  onComplete: (e: CalendarEvent) => void;
  onRemove: (id: number) => void;
}) {
  return (
    <div className="flex items-center gap-3 px-4 py-2.5 text-sm">
      <span className="w-14 shrink-0 text-[12px] text-zinc-500">
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
            e.status === "done" ? "text-zinc-400 line-through" : "text-zinc-800"
          }`}
        >
          {e.recurring && <span title="Tekrarlayan">↻ </span>}
          {e.title}
        </div>
        <div className="truncate text-[12px] text-zinc-400">
          {[e.matter_title, e.client_name, e.location].filter(Boolean).join(" · ") || "—"}
        </div>
      </div>
      {!e.recurring && e.status !== "done" && (
        <button
          onClick={() => onComplete(e)}
          className="shrink-0 rounded-md px-2 py-1 text-[12px] text-emerald-600 hover:bg-emerald-50"
        >
          Bitti
        </button>
      )}
      <button
        onClick={() => onRemove(e.id)}
        title={e.recurring ? "Tüm tekrar serisini sil" : "Sil"}
        className="shrink-0 rounded-md px-2 py-1 text-[12px] text-red-600 hover:bg-red-50"
      >
        Sil
      </button>
    </div>
  );
}
