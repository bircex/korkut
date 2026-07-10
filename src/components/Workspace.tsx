import { useEffect, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import {
  api,
  ApiError,
  type ChatMessage,
  type DocBrief,
  type ModelHealth,
  type SessionBrief,
  type User,
} from "../api";
import { DocumentPanel } from "./DocumentPanel";
import { DocumentLibrary } from "./DocumentLibrary";
import { Settings } from "./Settings";
import { Customize } from "./Customize";
import { ClientsPanel } from "./ClientsPanel";
import { FinancePanel } from "./FinancePanel";
import { MattersPanel } from "./MattersPanel";
import { CalendarPanel } from "./CalendarPanel";
import { TasksPanel } from "./TasksPanel";
import { MailConfirm } from "./MailConfirm";
import { navigate, usePath } from "../router";
import {
  type Icon,
  ArrowClockwise,
  CalendarBlank,
  CaretDown,
  CheckSquare,
  DotsThree,
  FileText,
  Folder,
  List,
  Paperclip,
  Plus,
  Users,
  Wallet,
  Warning,
} from "../icons";

const SESSION_PAGE = 20;

const MAX_UPLOAD = 15 * 1024 * 1024;
const ACCEPT = ".pdf,.docx,.txt,.png,.jpg,.jpeg";

function greeting(total: number, office: string | null): ChatMessage {
  const who = office ? `${office} hukuk asistanınız` : "hukuk asistanınız";
  const content =
    total > 0
      ? `Merhaba, ben Korkut — ${who}. Şu an ${total.toLocaleString(
          "tr-TR",
        )} yargı kararına ve tüm yürürlükteki mevzuata canlı erişiyorum. Size nasıl yardımcı olabilirim?`
      : `Merhaba, ben Korkut. ${who}. Size nasıl yardımcı olabilirim?`;
  return { role: "assistant", content };
}

function whenLabel(iso: string | null): string {
  return iso ? new Date(iso).toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit" }) : "";
}

function initials(u: User): string {
  const base = u.full_name ?? u.username;
  return base.split(/\s+/).map((w) => w[0]).slice(0, 2).join("").toUpperCase();
}

// Sol menü ikonları — Phosphor set'inden (bkz. ../icons.ts).
const NAV_ICON: Record<string, Icon> = {
  matters: Folder,
  clients: Users,
  finance: Wallet,
  tasks: CheckSquare,
  calendar: CalendarBlank,
  docs: FileText,
};

function NavIcon({ name }: { name: string }) {
  const Ikon = NAV_ICON[name];
  return Ikon ? <Ikon size={18} aria-hidden className="shrink-0" /> : null;
}

type SidebarView =
  | "chats"
  | "docs"
  | "settings"
  | "customize"
  | "matters"
  | "clients"
  | "finance"
  | "tasks"
  | "calendar";

// Top-level page URLs — a page can be bookmarked, shared, or survive a reload; the specific chat
// or matter open within "chats" is deliberately not part of the URL (out of scope, see Settings.tsx
// for the same pattern one level down for its tabs).
const VIEW_PATH: Record<SidebarView, string> = {
  chats: "/",
  docs: "/belgeler",
  settings: "/ayarlar",
  customize: "/ozellestir",
  matters: "/dosyalar",
  clients: "/muvekkiller",
  finance: "/mali",
  tasks: "/gorevler",
  calendar: "/takvim",
};

function viewFromPath(path: string): SidebarView {
  const hit = (Object.entries(VIEW_PATH) as [SidebarView, string][]).find(
    ([v, p]) => v !== "chats" && (path === p || path.startsWith(`${p}/`)),
  );
  return hit ? hit[0] : "chats";
}

export function Workspace({ user, onLogout }: { user: User; onLogout: () => void }) {
  const [sessions, setSessions] = useState<SessionBrief[]>([]);
  const [sessionsTotal, setSessionsTotal] = useState(0);
  const [loadingMore, setLoadingMore] = useState(false);
  const [docs, setDocs] = useState<DocBrief[]>([]);
  const path = usePath();
  const view = viewFromPath(path);
  const [currentId, setCurrentId] = useState<number | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [coverage, setCoverage] = useState(0);
  const [text, setText] = useState("");
  const [attachment, setAttachment] = useState<File | null>(null);
  const [sending, setSending] = useState(false);
  const [sendingId, setSendingId] = useState<number | null>(null); // which chat the in-flight send is for
  const [activity, setActivity] = useState<string | null>(null); // current tool step, while waiting
  const [streaming, setStreaming] = useState(false); // answer tokens are landing in the last bubble
  const [sidebarOpen, setSidebarOpen] = useState(() =>
    typeof window !== "undefined" ? window.innerWidth >= 640 : true,
  );
  const [pane, setPane] = useState<"chat" | "document">("chat");
  const [pendingEmailId, setPendingEmailId] = useState<number | null>(null);
  const [docRefresh, setDocRefresh] = useState(0);
  const [menuOpen, setMenuOpen] = useState(false);
  const [rowMenuId, setRowMenuId] = useState<number | null>(null);
  const [renamingId, setRenamingId] = useState<number | null>(null);
  const [renameText, setRenameText] = useState("");
  const [health, setHealth] = useState<ModelHealth | null>(null);

  // Best-effort model-reachability check → drives the warning banner. Never throws.
  async function checkHealth() {
    try {
      setHealth(await api.modelHealth());
    } catch {
      /* leave the banner as-is on a transient failure */
    }
  }

  const endRef = useRef<HTMLDivElement>(null);
  const taRef = useRef<HTMLTextAreaElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  // Mirrors currentId for reads inside async stream callbacks (a state closure goes stale). This
  // is what stops a still-arriving answer from being painted into a chat the user switched to —
  // the reply is always persisted server-side under its own conversation, never mixed here.
  const currentIdRef = useRef<number | null>(null);

  useEffect(() => {
    (async () => {
      try {
        setCoverage((await api.coverage()).total_decisions);
      } catch {
        /* best effort */
      }
      checkHealth();
      loadDocs();
      try {
        const page = await api.sessions(SESSION_PAGE, 0);
        setSessions(page.items);
        setSessionsTotal(page.total);
        // Loads the latest chat in the background regardless of which page we landed on — but
        // must not steal a deep link (e.g. /ayarlar/model) by forcing the view back to "chats".
        if (page.items.length) await openSession(page.items[0].id, { switchView: false });
        else newChat({ switchView: false });
      } catch {
        newChat({ switchView: false });
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, sending]);

  // Keep the ref in lockstep with currentId so stream callbacks read the chat the user is on now.
  useEffect(() => {
    currentIdRef.current = currentId;
  }, [currentId]);

  function closeSidebarOnMobile() {
    if (typeof window !== "undefined" && window.innerWidth < 640) setSidebarOpen(false);
  }

  async function loadOlder() {
    if (loadingMore) return;
    setLoadingMore(true);
    try {
      const page = await api.sessions(SESSION_PAGE, sessions.length);
      setSessionsTotal(page.total);
      setSessions((prev) => {
        const seen = new Set(prev.map((x) => x.id));
        return [...prev, ...page.items.filter((x) => !seen.has(x.id))];
      });
    } catch {
      /* best effort */
    } finally {
      setLoadingMore(false);
    }
  }

  async function openSession(id: number, opts: { switchView?: boolean } = {}) {
    currentIdRef.current = id; // synchronously, so an in-flight stream stops painting this view
    setCurrentId(id);
    if (opts.switchView !== false) navigate(VIEW_PATH.chats);
    setPane("chat");
    closeSidebarOnMobile();
    try {
      const detail = await api.getSession(id);
      setMessages(detail.messages.length ? detail.messages : [greeting(coverage, user.office)]);
    } catch {
      setMessages([greeting(coverage, user.office)]);
    }
  }

  function newChat(opts: { switchView?: boolean } = {}) {
    currentIdRef.current = null;
    setCurrentId(null);
    setMessages([greeting(coverage, user.office)]);
    if (opts.switchView !== false) navigate(VIEW_PATH.chats);
    setPane("chat");
    closeSidebarOnMobile();
  }

  async function loadDocs() {
    try {
      setDocs(await api.documents());
    } catch {
      /* best effort */
    }
  }

  async function openDocFromList(d: DocBrief) {
    currentIdRef.current = d.conversation_id;
    setCurrentId(d.conversation_id);
    navigate(VIEW_PATH.chats);
    setPane("document");
    closeSidebarOnMobile();
    try {
      const detail = await api.getSession(d.conversation_id);
      setMessages(detail.messages.length ? detail.messages : [greeting(coverage, user.office)]);
    } catch {
      setMessages([greeting(coverage, user.office)]);
    }
  }

  async function cloneChat(id: number) {
    setRowMenuId(null);
    try {
      const created = await api.cloneSession(id);
      setSessions((s) => [created, ...s]);
      setSessionsTotal((t) => t + 1);
      loadDocs();
      await openSession(created.id);
    } catch {
      /* ignore */
    }
  }

  function startRename(s: SessionBrief) {
    setRowMenuId(null);
    setRenamingId(s.id);
    setRenameText(s.title);
  }

  async function commitRename() {
    const id = renamingId;
    const title = renameText.trim();
    setRenamingId(null);
    if (id === null) return;
    const current = sessions.find((x) => x.id === id);
    if (!title || !current || title === current.title) return;
    setSessions((s) => s.map((x) => (x.id === id ? { ...x, title } : x)));
    try {
      await api.renameSession(id, title);
    } catch {
      /* ignore — local title stays optimistic */
    }
  }

  async function remove(id: number) {
    setRowMenuId(null);
    try {
      await api.deleteSession(id);
    } catch {
      /* ignore */
    }
    const rest = sessions.filter((s) => s.id !== id);
    setSessions(rest);
    setSessionsTotal((t) => Math.max(rest.length, t - 1));
    if (id === currentId) {
      if (rest.length) openSession(rest[0].id);
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
    const userDisplay = file ? (t ? `${t}\n\nEk: ${file.name}` : `Ek: ${file.name}`) : t;
    setMessages((m) => [...(hadGreetingOnly ? [] : m), { role: "user", content: userDisplay }]);

    // The chat this send belongs to. Every view mutation below is gated on the user still being
    // here (currentIdRef), so a slow/streamed answer can never bleed into another chat's window.
    let targetId = currentId;
    const showsThisChat = () => currentIdRef.current === targetId;
    setSendingId(targetId); // pin the "yazıyor…" indicator to this chat (updated below for new chats)
    try {
      if (targetId === null) {
        const created = await api.createSession();
        targetId = created.id;
        currentIdRef.current = targetId;
        setCurrentId(targetId);
        setSendingId(targetId);
        setSessions((s) => [created, ...s]);
        setSessionsTotal((tot) => tot + 1);
      }
      let res: {
        reply: string;
        title: string;
        open_document?: number | null;
        pending_email?: number | null;
      };
      if (file) {
        // Uploads aren't streamed (the file is processed server-side, then answered).
        res = await api.uploadToSession(targetId, file, t);
        if (showsThisChat()) setMessages((m) => [...m, { role: "assistant", content: res.reply }]);
      } else {
        // Stream the answer: show activity steps, then grow the assistant bubble token by token.
        let acc = "";
        let started = false;
        res = await api.sessionChatStream(targetId, t, {
          onStep: (label) => {
            if (!started && showsThisChat()) setActivity(label);
          },
          onToken: (tok) => {
            acc += tok;
            if (!showsThisChat()) return; // user moved to another chat → don't paint this one
            if (!started) {
              started = true;
              setStreaming(true);
              setActivity(null);
              setMessages((m) => [...m, { role: "assistant", content: "" }]);
            }
            setMessages((m) => {
              const copy = m.slice();
              copy[copy.length - 1] = { role: "assistant", content: acc };
              return copy;
            });
          },
        });
        // Reconcile with the authoritative reply (e.g. the cloud path streams no tokens, or the
        // final restored text differs from the streamed slices) — only if still viewing this chat.
        if (showsThisChat()) {
          if (!started) {
            setMessages((m) => [...m, { role: "assistant", content: res.reply }]);
          } else if (res.reply && res.reply !== acc) {
            setMessages((m) => {
              const copy = m.slice();
              copy[copy.length - 1] = { role: "assistant", content: res.reply };
              return copy;
            });
          }
        }
      }
      if (!file && showsThisChat() && res.open_document) {
        setPane("document");
        setDocRefresh((n) => n + 1); // Korkut drafted into the editor → reload it
        loadDocs(); // a new draft may have landed → refresh the Belgeler list
      }
      if (!file && showsThisChat() && res.pending_email) {
        // Korkut proposed an e-mail → surface the confirmation card (it never sends on its own).
        setPendingEmailId(res.pending_email);
      }
      // The sidebar entry updates regardless of which chat is on screen — it's keyed by targetId.
      const fixedId = targetId;
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
      if (showsThisChat()) {
        setMessages((m) => [...m, { role: "assistant", content: `Hata: ${detail}` }]);
      }
      if (err instanceof ApiError && err.status === 503) checkHealth(); // model down → refresh banner
    } finally {
      setSending(false);
      setSendingId(null);
      setStreaming(false);
      setActivity(null);
      taRef.current?.focus();
    }
  }

  // Every page renders below the persistent navbar with the sidebar; only the document editor
  // goes full-width (an A4 canvas needs the room, and its Sohbet/Editör switch lives in the navbar).
  const editorOpen = view !== "docs" && pane === "document" && currentId !== null;

  return (
    <div className="flex h-dvh flex-col bg-zinc-100 text-zinc-900">
      {pendingEmailId !== null && (
        <MailConfirm id={pendingEmailId} onClose={() => setPendingEmailId(null)} />
      )}
      {/* Bütünleşik navbar: kanvasla aynı zemin, çizgisiz — Sohbet/Editör anahtarı beyaz kart olarak öne çıkar. */}
      <header className="relative flex items-center gap-3 bg-zinc-100 px-3 py-2">
        {!editorOpen && (
          <button
            onClick={() => setSidebarOpen((o) => !o)}
            className="rounded-md px-2 py-1 text-zinc-600 hover:bg-zinc-200/70"
            title="Sohbet geçmişi"
          >
            <List size={20} aria-hidden />
          </button>
        )}
        <div className="flex items-center gap-2">
          {/* Marka ana sayfaya döner — Ayarlar/Özelleştir gibi ekranların ayrı "Geri" düğmesi yok. */}
          <button
            onClick={() => navigate(VIEW_PATH.chats)}
            title="Ana sayfa"
            className="flex items-center gap-2 rounded-md hover:opacity-75"
          >
            <img
              src="/korkut-logo.svg"
              alt="Korkut"
              className="h-7 w-7 rounded-lg border border-zinc-200 bg-white p-0.5"
            />
            <span className="font-bold">Korkut</span>
          </button>
          {user.office && (
            <span className="hidden border-l border-zinc-200 pl-2 text-sm text-zinc-500 sm:inline">
              {user.office}
            </span>
          )}
        </div>
        {view === "chats" && currentId !== null && (
          <div className="absolute left-1/2 flex -translate-x-1/2 gap-1 rounded-lg border border-zinc-200 bg-white p-1 text-[13px] font-medium shadow-sm">
            <button
              onClick={() => setPane("chat")}
              className={
                "rounded-md px-4 py-1 " +
                (pane === "chat" ? "bg-zinc-900 text-white shadow-sm" : "text-zinc-500 hover:text-zinc-800")
              }
            >
              Sohbet
            </button>
            <button
              onClick={() => setPane("document")}
              className={
                "rounded-md px-4 py-1 " +
                (pane === "document" ? "bg-zinc-900 text-white shadow-sm" : "text-zinc-500 hover:text-zinc-800")
              }
            >
              Editör
            </button>
          </div>
        )}
        <div className="ml-auto flex items-center gap-3">
          <div className="relative">
            <button
              onClick={() => setMenuOpen((o) => !o)}
              className="flex items-center gap-2 rounded-md py-1 pl-1 pr-1.5 hover:bg-zinc-200/70"
            >
              <span className="hidden text-sm font-medium text-zinc-800 sm:inline">
                {user.full_name ?? user.username}
              </span>
              <span className="grid h-8 w-8 place-items-center rounded-full bg-zinc-900 text-[12px] font-bold text-white">
                {initials(user)}
              </span>
              <CaretDown size={12} aria-hidden className="text-zinc-400" />
            </button>
            {menuOpen && (
              <>
                <div className="fixed inset-0 z-30" onClick={() => setMenuOpen(false)} />
                <div className="absolute right-0 z-40 mt-2 w-48 overflow-hidden rounded-xl border border-zinc-200 bg-white py-1 shadow-lg">
                  <div className="border-b border-zinc-100 px-3 py-2">
                    <div className="text-[13px] font-medium text-zinc-800">
                      {user.full_name ?? user.username}
                    </div>
                    <div className="text-[12px] text-zinc-400">
                      {[user.office, user.role === "owner" ? "Sahip" : "Üye"].filter(Boolean).join(" · ")}
                    </div>
                  </div>
                  <button
                    onClick={() => {
                      setMenuOpen(false);
                      navigate(VIEW_PATH.settings);
                    }}
                    className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-zinc-700 hover:bg-zinc-50"
                  >
                    Ayarlar
                  </button>
                  <button
                    onClick={() => {
                      setMenuOpen(false);
                      navigate(VIEW_PATH.customize);
                    }}
                    className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-zinc-700 hover:bg-zinc-50"
                  >
                    Özelleştir
                  </button>
                  <button
                    onClick={onLogout}
                    className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-zinc-700 hover:bg-zinc-50"
                  >
                    Çıkış
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      </header>

      {health && !health.connected && (
        <div className="flex items-center gap-3 border-b border-amber-200 bg-amber-50 px-4 py-2 text-[13px] text-amber-800">
          <Warning size={18} weight="fill" aria-hidden className="shrink-0" />
          <span className="min-w-0 flex-1">{health.detail}</span>
          <button
            onClick={() => navigate(VIEW_PATH.settings)}
            className="shrink-0 rounded-md border border-amber-300 bg-white px-2.5 py-1 text-xs font-medium text-amber-800 hover:bg-amber-100"
          >
            Ayarlar → Model
          </button>
          <button
            onClick={checkHealth}
            className="shrink-0 rounded-md px-2 py-1 text-xs font-medium text-amber-700 hover:bg-amber-100"
            title="Yeniden dene"
          >
            <ArrowClockwise size={14} aria-hidden />
          </button>
        </div>
      )}

      {editorOpen && currentId !== null ? (
        <div className="min-h-0 flex-1">
          <DocumentPanel key={currentId} sessionId={currentId} refreshKey={docRefresh} />
        </div>
      ) : (
      <div className="flex min-h-0 flex-1">
        {sidebarOpen && (
          <>
            <div className="fixed inset-0 z-10 bg-black/30 sm:hidden" onClick={() => setSidebarOpen(false)} />
            <aside className="fixed inset-y-0 left-0 z-20 flex w-64 flex-col border-r border-zinc-200 bg-white sm:static sm:z-auto">
              <div className="px-3 pt-3">
                <div className="px-1.5 pb-1 text-[11px] font-semibold uppercase tracking-wider text-zinc-400">
                  Menü
                </div>
                <nav className="flex flex-col gap-0.5">
                  {(
                    [
                      { key: "matters", label: "Dosyalar" },
                      { key: "clients", label: "Müvekkiller" },
                      { key: "finance", label: "Mali" },
                      { key: "tasks", label: "Görevler" },
                      { key: "calendar", label: "Takvim" },
                      { key: "docs", label: "Belgeler" },
                    ] as { key: SidebarView; label: string }[]
                  ).map((it) => (
                    <button
                      key={it.key}
                      onClick={() => {
                        navigate(VIEW_PATH[it.key]);
                        if (it.key === "docs") loadDocs();
                        closeSidebarOnMobile();
                      }}
                      className={
                        "flex items-center gap-2.5 rounded-lg px-2.5 py-1.5 text-left text-sm " +
                        (view === it.key
                          ? "bg-zinc-100 font-medium text-zinc-900"
                          : "text-zinc-600 hover:bg-zinc-50 hover:text-zinc-900")
                      }
                    >
                      <span className={view === it.key ? "text-zinc-700" : "text-zinc-400"}>
                        <NavIcon name={it.key} />
                      </span>
                      {it.label}
                    </button>
                  ))}
                </nav>
              </div>

              <div className="mt-3 flex items-center justify-between border-t border-zinc-200 px-4 pb-1 pt-3">
                <span className="text-[11px] font-semibold uppercase tracking-wider text-zinc-400">
                  Sohbetler
                </span>
                <button
                  onClick={() => newChat()}
                  title="Yeni sohbet"
                  className="rounded-md p-1 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
                >
                  <Plus size={18} aria-hidden />
                </button>
              </div>
              <nav className="flex-1 overflow-y-auto px-2 pb-2">
                {sessions.map((s) => (
                  <div
                    key={s.id}
                    className={
                      "group relative flex items-center gap-1 rounded-lg px-2 py-2 text-sm " +
                      (view === "chats" && s.id === currentId ? "bg-zinc-100" : "hover:bg-zinc-50")
                    }
                  >
                    {renamingId === s.id ? (
                      <input
                        autoFocus
                        value={renameText}
                        onChange={(e) => setRenameText(e.target.value)}
                        onBlur={commitRename}
                        onKeyDown={(e) => {
                          if (e.key === "Enter") commitRename();
                          if (e.key === "Escape") setRenamingId(null);
                        }}
                        className="min-w-0 flex-1 rounded-md border border-zinc-300 bg-white px-2 py-1 text-sm text-zinc-900 outline-none focus:border-zinc-900"
                      />
                    ) : (
                      <button onClick={() => openSession(s.id)} className="min-w-0 flex-1 text-left" title={s.title}>
                        <div className="truncate text-zinc-800">{s.title}</div>
                        <div className="truncate text-[11px] text-zinc-400">
                          {[s.created_by_name, whenLabel(s.updated_at)].filter(Boolean).join(" · ")}
                        </div>
                      </button>
                    )}
                    <button
                      onClick={() => setRowMenuId((id) => (id === s.id ? null : s.id))}
                      className={
                        "shrink-0 rounded-md px-1.5 py-1 text-zinc-400 hover:bg-zinc-200 hover:text-zinc-700 " +
                        (rowMenuId === s.id ? "opacity-100" : "opacity-0 group-hover:opacity-100")
                      }
                      title="Seçenekler"
                    >
                      <DotsThree size={18} weight="bold" aria-hidden />
                    </button>
                    {rowMenuId === s.id && (
                      <>
                        <div className="fixed inset-0 z-30" onClick={() => setRowMenuId(null)} />
                        <div className="absolute right-2 top-9 z-40 w-44 overflow-hidden rounded-xl border border-zinc-200 bg-white py-1 shadow-lg">
                          <button
                            onClick={() => startRename(s)}
                            className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-zinc-700 hover:bg-zinc-50"
                          >
                            Yeniden adlandır
                          </button>
                          <button
                            onClick={() => cloneChat(s.id)}
                            className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-zinc-700 hover:bg-zinc-50"
                          >
                            Klonla
                          </button>
                          <button
                            onClick={() => remove(s.id)}
                            className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-red-600 hover:bg-red-50"
                          >
                            Sil
                          </button>
                        </div>
                      </>
                    )}
                  </div>
                ))}
                {sessions.length === 0 && <p className="px-2 py-3 text-[13px] text-zinc-400">Henüz sohbet yok.</p>}
                {sessions.length < sessionsTotal && (
                  <button
                    onClick={loadOlder}
                    disabled={loadingMore}
                    className="mt-1 w-full rounded-lg px-2 py-2 text-center text-[13px] text-zinc-500 hover:bg-zinc-50 disabled:opacity-50"
                  >
                    {loadingMore ? "Yükleniyor…" : `Daha eski sohbetler · ${sessionsTotal - sessions.length}`}
                  </button>
                )}
              </nav>
            </aside>
          </>
        )}

        <div className="relative flex min-w-0 flex-1">
        {view === "settings" ? (
          <Settings user={user} />
        ) : view === "customize" ? (
          <Customize user={user} />
        ) : view === "docs" ? (
          <DocumentLibrary docs={docs} currentId={currentId} onOpen={openDocFromList} />
        ) : view === "matters" ? (
          <MattersPanel user={user} onOpenSession={(id) => openSession(id)} />
        ) : view === "clients" ? (
          <ClientsPanel />
        ) : view === "finance" ? (
          <FinancePanel />
        ) : view === "tasks" ? (
          <TasksPanel />
        ) : view === "calendar" ? (
          <CalendarPanel />
        ) : (
        <div className="flex min-w-0 flex-1 flex-col">
          <section className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-3.5 overflow-y-auto px-4 py-5">
            {messages.map((m, i) => (
              <div key={i} className={m.role === "user" ? "flex justify-end" : "flex justify-start"}>
                <div
                  className={
                    m.role === "user"
                      ? "max-w-[80%] whitespace-pre-wrap rounded-2xl rounded-br-sm bg-zinc-900 px-3.5 py-2.5 text-[15px] leading-relaxed text-white"
                      : "md max-w-[85%] rounded-2xl rounded-bl-sm border border-zinc-200 bg-white px-4 py-3 text-[15px] leading-relaxed text-zinc-900"
                  }
                >
                  {m.role === "user" ? m.content : <ReactMarkdown remarkPlugins={[remarkGfm]}>{m.content}</ReactMarkdown>}
                </div>
              </div>
            ))}
            {sending && sendingId === currentId && !streaming && (
              <div className="flex justify-start">
                <div className="max-w-[80%] rounded-2xl rounded-bl-sm border border-zinc-200 bg-white px-3.5 py-2.5 text-[15px] italic text-zinc-400">
                  {activity ? activity : "Korkut yazıyor…"}
                </div>
              </div>
            )}
            <div ref={endRef} />
          </section>

          {attachment && (
            <div className="mx-auto flex w-full max-w-3xl items-center gap-2 px-4 pt-2 text-[13px] text-zinc-500">
              <span className="truncate rounded-md border border-zinc-300 bg-white px-2 py-1">Ek: {attachment.name}</span>
              <button type="button" onClick={() => setAttachment(null)} className="shrink-0 text-zinc-400 hover:text-red-500">
                kaldır
              </button>
            </div>
          )}
          <form
            onSubmit={(e) => {
              e.preventDefault();
              send();
            }}
            className="mx-auto flex w-full max-w-3xl items-end gap-2.5 border-t border-zinc-200 bg-zinc-100 px-4 py-3"
          >
            <input ref={fileRef} type="file" accept={ACCEPT} className="hidden" onChange={pickFile} />
            <button
              type="button"
              onClick={() => fileRef.current?.click()}
              title="Dosya ekle (PDF, DOCX, TXT, PNG, JPG)"
              className="shrink-0 rounded-[10px] border border-zinc-300 bg-white px-3 py-3 leading-none text-zinc-600 hover:border-zinc-900"
            >
              <Paperclip size={18} aria-hidden />
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
              className="max-h-40 flex-1 resize-none rounded-[10px] border border-zinc-300 bg-white px-3.5 py-3 text-[15px] text-zinc-900 outline-none focus:border-zinc-900"
            />
            <button
              type="submit"
              disabled={sending || (!text.trim() && !attachment)}
              className="rounded-[10px] bg-zinc-900 px-4 py-3 text-[15px] font-semibold text-white hover:opacity-90 disabled:opacity-50"
            >
              Gönder
            </button>
          </form>
        </div>
        )}
        </div>
      </div>
      )}

      {!editorOpen && (
        <footer className="border-t border-zinc-200 bg-white px-4 py-1.5 text-center text-[12px] text-zinc-500">
          {coverage > 0
            ? `${coverage.toLocaleString("tr-TR")} yargı kararı + tüm yürürlükteki mevzuata canlı erişim · `
            : ""}
          © {user.office ?? "Korkut"}
        </footer>
      )}
    </div>
  );
}
