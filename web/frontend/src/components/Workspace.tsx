import { useEffect, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { api, ApiError, type ChatMessage, type SessionBrief, type User } from "../api";

const BASE_GREETING =
  "Merhaba, ben Korkut. Cankar Hukuk asistanınız. Size nasıl yardımcı olabilirim?";

const MAX_UPLOAD = 15 * 1024 * 1024;
const ACCEPT = ".pdf,.docx,.txt,.png,.jpg,.jpeg";

function greeting(total: number): ChatMessage {
  const content =
    total > 0
      ? `Merhaba, ben Korkut — Cankar Hukuk asistanınız. Şu an ${total.toLocaleString(
          "tr-TR",
        )} yargı kararına ve tüm yürürlükteki mevzuata canlı erişiyorum. Size nasıl yardımcı olabilirim?`
      : BASE_GREETING;
  return { role: "assistant", content };
}

function whenLabel(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  return d.toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit" });
}

export function Workspace({ user, onLogout }: { user: User; onLogout: () => void }) {
  const [sessions, setSessions] = useState<SessionBrief[]>([]);
  const [currentId, setCurrentId] = useState<number | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [coverage, setCoverage] = useState(0);
  const [text, setText] = useState("");
  const [attachment, setAttachment] = useState<File | null>(null);
  const [sending, setSending] = useState(false);
  const [sidebarOpen, setSidebarOpen] = useState(false);

  const endRef = useRef<HTMLDivElement>(null);
  const taRef = useRef<HTMLTextAreaElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    (async () => {
      try {
        setCoverage((await api.coverage()).total_decisions);
      } catch {
        /* best effort */
      }
      try {
        const list = await api.sessions();
        setSessions(list);
        if (list.length) await openSession(list[0].id, list);
        else newChat();
      } catch {
        newChat();
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, sending]);

  async function openSession(id: number, list = sessions) {
    setCurrentId(id);
    setSidebarOpen(false);
    try {
      const detail = await api.getSession(id);
      setMessages(detail.messages.length ? detail.messages : [greeting(coverage)]);
    } catch {
      setMessages([greeting(coverage)]);
    }
    void list;
  }

  function newChat() {
    setCurrentId(null);
    setMessages([greeting(coverage)]);
    setSidebarOpen(false);
  }

  async function remove(id: number) {
    try {
      await api.deleteSession(id);
    } catch {
      /* ignore */
    }
    const rest = sessions.filter((s) => s.id !== id);
    setSessions(rest);
    if (id === currentId) {
      if (rest.length) openSession(rest[0].id, rest);
      else newChat();
    }
  }

  function pickFile(e: React.ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0];
    e.target.value = "";
    if (!f) return;
    if (f.size > MAX_UPLOAD) {
      alert("Dosya çok büyük (en fazla 15 MB).");
      return;
    }
    setAttachment(f);
  }

  async function send() {
    const t = text.trim();
    if ((!t && !attachment) || sending) return;
    const file = attachment;
    setText("");
    setAttachment(null);
    if (taRef.current) taRef.current.style.height = "auto";
    setSending(true);

    const hadGreetingOnly = messages.length <= 1 && currentId === null;
    const userDisplay = file ? (t ? `${t}\n\n📎 ${file.name}` : `📎 ${file.name}`) : t;
    setMessages((m) => [...(hadGreetingOnly ? [] : m), { role: "user", content: userDisplay }]);

    try {
      let id = currentId;
      if (id === null) {
        const created = await api.createSession();
        id = created.id;
        setCurrentId(id);
        setSessions((s) => [created, ...s]);
      }
      const res = file ? await api.uploadToSession(id, file, t) : await api.sessionChat(id, t);
      setMessages((m) => [...m, { role: "assistant", content: res.reply }]);
      const fixedId = id;
      setSessions((s) => [
        {
          ...(s.find((x) => x.id === fixedId) as SessionBrief),
          title: res.title,
          updated_at: new Date().toISOString(),
        },
        ...s.filter((x) => x.id !== fixedId),
      ]);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        onLogout();
        return;
      }
      const detail = err instanceof ApiError ? err.message : "yanıt alınamadı";
      setMessages((m) => [...m, { role: "assistant", content: `Hata: ${detail}` }]);
    } finally {
      setSending(false);
      taRef.current?.focus();
    }
  }

  return (
    <div className="flex h-dvh">
      {sidebarOpen && (
        <div className="fixed inset-0 z-10 bg-black/50 sm:hidden" onClick={() => setSidebarOpen(false)} />
      )}
      <aside
        className={
          "fixed inset-y-0 left-0 z-20 flex w-64 flex-col border-r border-zinc-800 bg-zinc-950 " +
          "transition-transform sm:static sm:translate-x-0 " +
          (sidebarOpen ? "translate-x-0" : "-translate-x-full")
        }
      >
        <div className="flex items-center gap-2.5 border-b border-zinc-800 px-4 py-3">
          <span className="grid h-7 w-7 place-items-center rounded-lg bg-white text-[11px] font-extrabold text-black">
            CH
          </span>
          <span className="font-bold">Korkut</span>
        </div>
        <button
          onClick={newChat}
          className="m-3 rounded-lg bg-white px-3 py-2.5 text-sm font-semibold text-black hover:opacity-90"
        >
          + Yeni sohbet
        </button>
        <nav className="flex-1 overflow-y-auto px-2 pb-2">
          {sessions.map((s) => (
            <div
              key={s.id}
              className={
                "group flex items-center gap-1 rounded-lg px-2 py-2 text-sm " +
                (s.id === currentId ? "bg-zinc-800" : "hover:bg-zinc-900")
              }
            >
              <button
                onClick={() => openSession(s.id)}
                className="min-w-0 flex-1 text-left"
                title={s.title}
              >
                <div className="truncate text-zinc-100">{s.title}</div>
                <div className="truncate text-[11px] text-zinc-500">
                  {[s.created_by_name, whenLabel(s.updated_at)].filter(Boolean).join(" · ")}
                </div>
              </button>
              <button
                onClick={() => remove(s.id)}
                className="shrink-0 px-1.5 text-zinc-600 opacity-0 hover:text-red-400 group-hover:opacity-100"
                title="Sil"
              >
                ✕
              </button>
            </div>
          ))}
          {sessions.length === 0 && (
            <p className="px-2 py-3 text-[13px] text-zinc-600">Henüz sohbet yok.</p>
          )}
        </nav>
        <div className="border-t border-zinc-800 px-4 py-3 text-[13px] text-zinc-400">
          {user.full_name ?? user.username}
          <button onClick={onLogout} className="ml-2 text-zinc-500 hover:text-zinc-200">
            · Çıkış
          </button>
        </div>
      </aside>

      <main className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center gap-3 border-b border-zinc-800 bg-zinc-950 px-4 py-3 sm:hidden">
          <button onClick={() => setSidebarOpen(true)} className="text-zinc-300" title="Sohbetler">
            ☰
          </button>
          <span className="font-bold">Korkut</span>
        </header>
        {coverage > 0 && (
          <div className="border-b border-zinc-900 bg-zinc-950 px-4 py-1.5 text-center text-[12px] text-zinc-500">
            {coverage.toLocaleString("tr-TR")} yargı kararı + tüm yürürlükteki mevzuata canlı erişim
          </div>
        )}

        <section className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-3.5 overflow-y-auto px-4 py-5">
          {messages.map((m, i) => (
            <div key={i} className={m.role === "user" ? "flex justify-end" : "flex justify-start"}>
              <div
                className={
                  m.role === "user"
                    ? "max-w-[80%] whitespace-pre-wrap rounded-2xl rounded-br-sm bg-white px-3.5 py-2.5 text-[15px] leading-relaxed text-black"
                    : "md max-w-[85%] rounded-2xl rounded-bl-sm border border-zinc-800 bg-zinc-900 px-4 py-3 text-[15px] leading-relaxed"
                }
              >
                {m.role === "user" ? (
                  m.content
                ) : (
                  <ReactMarkdown remarkPlugins={[remarkGfm]}>{m.content}</ReactMarkdown>
                )}
              </div>
            </div>
          ))}
          {sending && (
            <div className="flex justify-start">
              <div className="max-w-[80%] rounded-2xl rounded-bl-sm border border-zinc-800 bg-zinc-900 px-3.5 py-2.5 text-[15px] italic text-zinc-400">
                Korkut yazıyor…
              </div>
            </div>
          )}
          <div ref={endRef} />
        </section>

        {attachment && (
          <div className="mx-auto flex w-full max-w-3xl items-center gap-2 px-4 pt-2 text-[13px] text-zinc-400">
            <span className="truncate rounded-md border border-zinc-700 bg-zinc-900 px-2 py-1">
              📎 {attachment.name}
            </span>
            <button
              type="button"
              onClick={() => setAttachment(null)}
              className="shrink-0 text-zinc-500 hover:text-red-400"
            >
              kaldır
            </button>
          </div>
        )}
        <form
          onSubmit={(e) => {
            e.preventDefault();
            send();
          }}
          className="mx-auto flex w-full max-w-3xl items-end gap-2.5 border-t border-zinc-800 bg-zinc-950 px-4 py-3"
        >
          <input
            ref={fileRef}
            type="file"
            accept={ACCEPT}
            className="hidden"
            onChange={pickFile}
          />
          <button
            type="button"
            onClick={() => fileRef.current?.click()}
            title="Dosya ekle (PDF, DOCX, TXT, PNG, JPG)"
            className="shrink-0 rounded-[10px] border border-zinc-800 bg-zinc-900 px-3 py-3 text-lg leading-none text-zinc-300 hover:text-white"
          >
            📎
          </button>
          <textarea
            ref={taRef}
            rows={1}
            value={text}
            placeholder="Korkut'a yazın…"
            onChange={(e) => {
              setText(e.target.value);
              e.target.style.height = "auto";
              e.target.style.height = Math.min(e.target.scrollHeight, 160) + "px";
            }}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                send();
              }
            }}
            className="max-h-40 flex-1 resize-none rounded-[10px] border border-zinc-800 bg-zinc-900 px-3.5 py-3 text-[15px] text-zinc-100 outline-none focus:border-white"
          />
          <button
            type="submit"
            disabled={sending || (!text.trim() && !attachment)}
            className="rounded-[10px] bg-white px-4 py-3 text-[15px] font-semibold text-black hover:opacity-90 disabled:opacity-50"
          >
            Gönder
          </button>
        </form>
      </main>
    </div>
  );
}
