import { useEffect, useState } from "react";
import { api, ApiError, type OutboxMessage } from "../api";

// The confirmation card Korkut shows when it proposes an e-mail. Korkut never sends on its own:
// it writes a draft, this card surfaces it, and the lawyer reviews → "Gönder" (or "Zamanla" for a
// future date) → it actually leaves. "Vazgeç" discards the draft. Editable before sending.

function toLocalInput(iso: string | null): string {
  return iso ? iso.slice(0, 16) : ""; // "2025-01-01T10:00:00" → "2025-01-01T10:00"
}

export function MailConfirm({ id, onClose }: { id: number; onClose: () => void }) {
  const [draft, setDraft] = useState<OutboxMessage | null>(null);
  const [to, setTo] = useState("");
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [schedule, setSchedule] = useState(false);
  const [sendAt, setSendAt] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<OutboxMessage | null>(null);

  useEffect(() => {
    api
      .getOutbox(id)
      .then((d) => {
        setDraft(d);
        setTo(d.to);
        setSubject(d.subject);
        setBody(d.body);
        setSchedule(Boolean(d.send_at));
        setSendAt(toLocalInput(d.send_at));
      })
      .catch(() => setError("Taslak yüklenemedi."));
  }, [id]);

  async function send() {
    setBusy(true);
    setError(null);
    try {
      await api.editOutbox(id, {
        to_addr: to,
        subject,
        body,
        send_at: schedule && sendAt ? sendAt : null,
      });
      const res = await api.confirmOutbox(id);
      if (res.status === "failed") {
        setError(res.error || "Gönderilemedi.");
      } else {
        setDone(res); // sent | queued
      }
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Gönderilemedi.");
    } finally {
      setBusy(false);
    }
  }

  async function discard() {
    setBusy(true);
    try {
      await api.cancelOutbox(id);
    } catch {
      /* ignore — closing anyway */
    } finally {
      onClose();
    }
  }

  const input =
    "w-full rounded-lg border border-zinc-200 px-3 py-2 text-sm outline-none focus:border-zinc-400";

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/30 p-4" onClick={onClose}>
      <div
        className="w-full max-w-lg rounded-2xl bg-white p-5 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center gap-2">
          <span className="grid h-8 w-8 place-items-center rounded-lg bg-zinc-100">✉️</span>
          <div>
            <div className="text-sm font-semibold text-zinc-900">E-posta gönderilsin mi?</div>
            <div className="text-xs text-zinc-400">
              Korkut taslağı hazırladı — gözden geçirip onaylayın.
            </div>
          </div>
        </div>

        {done ? (
          <div className="py-6 text-center">
            <div className="text-3xl">{done.status === "sent" ? "✅" : "🕑"}</div>
            <div className="mt-2 text-sm font-medium text-zinc-900">
              {done.status === "sent"
                ? "E-posta gönderildi."
                : "E-posta zamanlandı — belirtilen tarihte gönderilecek."}
            </div>
            <button
              onClick={onClose}
              className="mt-4 rounded-lg bg-zinc-900 px-4 py-1.5 text-sm font-medium text-white hover:bg-zinc-700"
            >
              Kapat
            </button>
          </div>
        ) : (
          <>
            <div className="space-y-3">
              <div>
                <label className="mb-1 block text-xs font-medium text-zinc-500">Alıcı</label>
                <input className={input} value={to} onChange={(e) => setTo(e.target.value)} />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-zinc-500">Konu</label>
                <input
                  className={input}
                  value={subject}
                  onChange={(e) => setSubject(e.target.value)}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-zinc-500">Mesaj</label>
                <textarea
                  className={`${input} min-h-[140px] resize-y`}
                  value={body}
                  onChange={(e) => setBody(e.target.value)}
                />
              </div>
              <label className="flex items-center gap-2 text-xs text-zinc-600">
                <input
                  type="checkbox"
                  checked={schedule}
                  onChange={(e) => setSchedule(e.target.checked)}
                  className="h-4 w-4 accent-zinc-900"
                />
                Daha sonra gönder (zamanla)
              </label>
              {schedule && (
                <input
                  type="datetime-local"
                  className={input}
                  value={sendAt}
                  onChange={(e) => setSendAt(e.target.value)}
                />
              )}
            </div>

            {error && <div className="mt-3 text-xs text-red-600">{error}</div>}

            <div className="mt-5 flex items-center gap-2">
              <button
                onClick={discard}
                disabled={busy || !draft}
                className="rounded-lg border border-zinc-200 px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-50 disabled:opacity-50"
              >
                Vazgeç
              </button>
              <button
                onClick={send}
                disabled={busy || !draft || !to.trim()}
                className="ml-auto rounded-lg bg-zinc-900 px-4 py-1.5 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-40"
              >
                {busy ? "…" : schedule ? "Zamanla" : "Gönder"}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
