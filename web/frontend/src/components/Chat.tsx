import { useEffect, useRef, useState } from "react";
import { api, ApiError, type ChatMessage, type User } from "../api";

const GREETING: ChatMessage = {
  role: "assistant",
  content: "Merhaba, ben Korkut. Cankar Hukuk asistanınız. Size nasıl yardımcı olabilirim?",
};

export function Chat({ user, onLogout }: { user: User; onLogout: () => void }) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [text, setText] = useState("");
  const [sending, setSending] = useState(false);
  const endRef = useRef<HTMLDivElement>(null);
  const taRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    api
      .history()
      .then((rows) => setMessages(rows.length ? rows : [GREETING]))
      .catch(() => setMessages([GREETING]));
  }, []);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, sending]);

  async function send() {
    const t = text.trim();
    if (!t || sending) return;
    setText("");
    if (taRef.current) taRef.current.style.height = "auto";
    setMessages((m) => [...m, { role: "user", content: t }]);
    setSending(true);
    try {
      const { reply } = await api.chat(t);
      setMessages((m) => [...m, { role: "assistant", content: reply }]);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        onLogout();
        return;
      }
      const detail = err instanceof ApiError ? err.message : "yanıt alınamadı";
      setMessages((m) => [...m, { role: "assistant", content: `Hata: ${detail}` }]);
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="flex h-dvh flex-col">
      <header className="flex items-center justify-between border-b border-zinc-800 bg-zinc-950 px-4 py-3">
        <div className="flex items-center gap-2.5">
          <span className="grid h-7 w-7 place-items-center rounded-lg bg-white text-[11px] font-extrabold text-black">
            CH
          </span>
          <span className="font-bold">Korkut</span>
        </div>
        <div className="flex items-center gap-3">
          <span className="hidden text-[13px] text-zinc-400 sm:inline">
            {user.full_name ?? user.username}
          </span>
          <button
            onClick={onLogout}
            className="rounded-lg border border-zinc-800 px-3 py-1.5 text-[13px] text-zinc-400 hover:text-zinc-100"
          >
            Çıkış
          </button>
        </div>
      </header>

      <section className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-3.5 overflow-y-auto px-4 py-5">
        {messages.map((m, i) => (
          <div key={i} className={m.role === "user" ? "flex justify-end" : "flex justify-start"}>
            <div
              className={
                m.role === "user"
                  ? "max-w-[80%] whitespace-pre-wrap rounded-2xl rounded-br-sm bg-white px-3.5 py-2.5 text-[15px] leading-relaxed text-black"
                  : "max-w-[80%] whitespace-pre-wrap rounded-2xl rounded-bl-sm border border-zinc-800 bg-zinc-900 px-3.5 py-2.5 text-[15px] leading-relaxed"
              }
            >
              {m.content}
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

      <form
        onSubmit={(e) => {
          e.preventDefault();
          send();
        }}
        className="mx-auto flex w-full max-w-3xl items-end gap-2.5 border-t border-zinc-800 bg-zinc-950 px-4 py-3"
      >
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
          disabled={sending || !text.trim()}
          className="rounded-[10px] bg-white px-4 py-3 text-[15px] font-semibold text-black hover:opacity-90 disabled:opacity-50"
        >
          Gönder
        </button>
      </form>
    </div>
  );
}
