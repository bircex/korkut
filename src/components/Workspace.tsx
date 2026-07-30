import { useEffect, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import {
  api,
  ApiError,
  type ChatMessage,
  type DocBrief,
  type SessionBrief,
  type Task,
  type User,
} from "../api";
import { DocumentPanel } from "./DocumentPanel";
import { DocumentLibrary } from "./DocumentLibrary";
import { Settings } from "./Settings";
import { ClientsPanel } from "./ClientsPanel";
import { FinancePanel } from "./FinancePanel";
import { MattersPanel } from "./MattersPanel";
import { CalendarPanel } from "./CalendarPanel";
import { TasksPanel } from "./TasksPanel";
import { MailConfirm } from "./MailConfirm";
import { matchPath, navigate, usePath } from "../router";
import { setThemeMode, useThemeMode, type ThemeMode } from "../theme";
import {
  type Icon,
  CalendarBlank,
  CaretDown,
  Check,
  CheckSquare,
  DotsThree,
  FileText,
  Folder,
  List,
  Monitor,
  Moon,
  Paperclip,
  Plus,
  Sun,
  Users,
  Wallet,
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

// Ana ekrandaki karşılama — günün saatine göre.
function timeGreeting(): string {
  const h = new Date().getHours();
  if (h < 6) return "İyi geceler";
  if (h < 12) return "Günaydın";
  if (h < 18) return "İyi günler";
  return "İyi akşamlar";
}

function firstName(u: User): string {
  return (u.full_name ?? u.username).trim().split(/\s+/)[0];
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

// Sağ üstteki tema seçici: Açık / Koyu / Sistem (seçim theme.ts üzerinden kalıcı).
const THEME_OPTIONS: { mode: ThemeMode; label: string; Ikon: Icon }[] = [
  { mode: "light", label: "Açık", Ikon: Sun },
  { mode: "dark", label: "Koyu", Ikon: Moon },
  { mode: "system", label: "Sistem", Ikon: Monitor },
];

function ThemeMenu() {
  const mode = useThemeMode();
  const [open, setOpen] = useState(false);
  const current = THEME_OPTIONS.find((o) => o.mode === mode) ?? THEME_OPTIONS[2];
  return (
    <div className="relative">
      <button
        onClick={() => setOpen((o) => !o)}
        title="Tema"
        className="rounded-md px-2 py-1.5 text-zinc-600 hover:bg-zinc-200/70"
      >
        <current.Ikon size={18} aria-hidden />
      </button>
      {open && (
        <>
          <div className="fixed inset-0 z-30" onClick={() => setOpen(false)} />
          <div className="absolute right-0 z-40 mt-2 w-36 overflow-hidden rounded-xl border border-zinc-200 bg-white py-1 shadow-lg">
            {THEME_OPTIONS.map(({ mode: m, label, Ikon }) => (
              <button
                key={m}
                onClick={() => {
                  setThemeMode(m);
                  setOpen(false);
                }}
                className={
                  "flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-zinc-50 " +
                  (m === mode ? "font-medium text-zinc-900" : "text-zinc-600")
                }
              >
                <Ikon size={15} aria-hidden />
                {label}
                {m === mode && <Check size={12} weight="bold" aria-hidden className="ml-auto" />}
              </button>
            ))}
          </div>
        </>
      )}
    </div>
  );
}

type SidebarView =
  | "chats"
  | "docs"
  | "settings"
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
  return hit ? hit[0] : "chats"; // "/" ve /sohbet/... → chats
}

// /sohbet/:id ve /sohbet/:id/belge — açık sohbet ve editör paneli URL'den okunur.
function chatRoute(path: string): { id: number; pane: "chat" | "document" } | null {
  const doc = matchPath("/sohbet/:id/belge", path);
  const m = doc ?? matchPath("/sohbet/:id", path);
  if (!m) return null;
  const id = Number(m.id);
  return Number.isFinite(id) ? { id, pane: doc ? "document" : "chat" } : null;
}

const VIEW_TITLE: Record<SidebarView, string> = {
  chats: "Sohbet",
  docs: "Belgeler",
  settings: "Ayarlar",
  matters: "Dosyalar",
  clients: "Müvekkiller",
  finance: "Mali",
  tasks: "Görevler",
  calendar: "Takvim",
};

export function Workspace({ user, onLogout }: { user: User; onLogout: () => void }) {
  const [sessions, setSessions] = useState<SessionBrief[]>([]);
  const [sessionsTotal, setSessionsTotal] = useState(0);
  const [loadingMore, setLoadingMore] = useState(false);
  const [docs, setDocs] = useState<DocBrief[]>([]);
  const [homeTasks, setHomeTasks] = useState<Task[]>([]); // ana ekrandaki açık görevler
  const path = usePath();
  const view = viewFromPath(path);
  // Açık sohbet ve panel URL'den türer — state değil (URL tek gerçek kaynak).
  const route = view === "chats" ? chatRoute(path) : null;
  const currentId = route?.id ?? null;
  const pane: "chat" | "document" = route?.pane ?? "chat";
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [coverage, setCoverage] = useState(0);
  const [text, setText] = useState("");
  const [attachment, setAttachment] = useState<File | null>(null);
  const [sending, setSending] = useState(false);
  const [sendingId, setSendingId] = useState<number | null>(null); // which chat the in-flight send is for
  const [activity, setActivity] = useState<string | null>(null); // current tool step, while waiting
  const [streaming, setStreaming] = useState(false); // answer tokens are landing in the last bubble
  // Masaüstünde açık başlar; mobilde kapalı (drawer olarak açılır).
  const [sidebarOpen, setSidebarOpen] = useState(() =>
    typeof window !== "undefined" ? window.innerWidth >= 640 : true,
  );
  const [pendingEmailId, setPendingEmailId] = useState<number | null>(null);
  const [docRefresh, setDocRefresh] = useState(0);
  const [menuOpen, setMenuOpen] = useState(false);
  const [rowMenuId, setRowMenuId] = useState<number | null>(null);
  const [renamingId, setRenamingId] = useState<number | null>(null);
  const [renameText, setRenameText] = useState("");

  const endRef = useRef<HTMLDivElement>(null);
  const taRef = useRef<HTMLTextAreaElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  // Mirrors currentId for reads inside async stream callbacks (a state closure goes stale). This
  // is what stops a still-arriving answer from being painted into a chat the user switched to —
  // the reply is always persisted server-side under its own conversation, never mixed here.
  const currentIdRef = useRef<number | null>(null);
  // Mesajları en son yüklenen sohbet — gönderim sırasında yaratılan oturumun URL'e yazılması
  // yeniden yükleme tetiklemesin diye (iyimser mesajlar ezilmesin).
  const loadedIdRef = useRef<number | null>(null);

  useEffect(() => {
    (async () => {
      try {
        setCoverage((await api.coverage()).total_decisions);
      } catch {
        /* best effort */
      }
      loadDocs();
      loadHomeTasks();
      try {
        const page = await api.sessions(SESSION_PAGE, 0);
        setSessions(page.items);
        setSessionsTotal(page.total);
      } catch {
        /* best effort — ana ekran (karşılama) zaten açık */
      }
    })();
  }, []);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, sending]);

  // Keep the ref in lockstep with currentId so stream callbacks read the chat the user is on now.
  useEffect(() => {
    currentIdRef.current = currentId;
  }, [currentId]);

  // Ana ekrana dönüşte önceki sohbetin mesajları kalmasın — render sırasında state uyarlama
  // deseni (React'ın "adjust state when props change" önerisi); efekt içinde setState'ten kaçınır.
  const routeKey = view !== "chats" ? null : route ? `sohbet:${route.id}` : "home";
  const [viewedKey, setViewedKey] = useState(routeKey);
  if (routeKey !== null && routeKey !== viewedKey) {
    setViewedKey(routeKey);
    if (routeKey === "home") {
      setMessages([]);
      loadedIdRef.current = null;
    }
  }

  // URL'deki sohbetin mesajlarını yükle — /sohbet/:id'e her geliş (tıklama, reload, back/forward)
  // buradan geçer; aksiyonlar yalnızca navigate eder.
  useEffect(() => {
    if (currentId === null || loadedIdRef.current === currentId) return;
    loadedIdRef.current = currentId;
    api
      .getSession(currentId)
      .then((detail) => {
        if (currentIdRef.current !== currentId) return; // bu arada başka sohbete geçildi
        setMessages(detail.messages.length ? detail.messages : [greeting(coverage, user.office)]);
      })
      .catch(() => {
        if (currentIdRef.current !== currentId) return;
        setMessages([greeting(coverage, user.office)]);
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentId]);

  // Sekme başlığı görünümü izler.
  useEffect(() => {
    document.title = `${VIEW_TITLE[view]} · ${user.office ?? "Korkut"}`;
  }, [view, user.office]);

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

  function openSession(id: number) {
    closeSidebarOnMobile();
    navigate(`/sohbet/${id}`); // yükleme URL efektinde
  }

  function newChat() {
    closeSidebarOnMobile();
    navigate(VIEW_PATH.chats); // "/" → URL efekti ana ekranı kurar
    loadHomeTasks();
  }

  async function loadDocs() {
    try {
      setDocs(await api.documents());
    } catch {
      /* best effort */
    }
  }

  async function loadHomeTasks() {
    try {
      setHomeTasks((await api.tasks("open")).items);
    } catch {
      /* best effort — kart görünmez */
    }
  }

  async function toggleHomeTask(t: Task) {
    // İyimser: satırı hemen işaretle, sonra sunucuya yaz.
    setHomeTasks((ts) =>
      ts.map((x) => (x.id === t.id ? { ...x, status: x.status === "done" ? "open" : "done" } : x)),
    );
    try {
      await api.updateTask(t.id, { status: t.status === "done" ? "open" : "done" });
    } catch {
      loadHomeTasks(); // geri al
    }
  }

  function openDocFromList(d: DocBrief) {
    closeSidebarOnMobile();
    navigate(`/sohbet/${d.conversation_id}/belge`);
  }

  async function cloneChat(id: number) {
    setRowMenuId(null);
    try {
      const created = await api.cloneSession(id);
      setSessions((s) => [created, ...s]);
      setSessionsTotal((t) => t + 1);
      loadDocs();
      openSession(created.id);
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
        // navigate'ten ÖNCE: yükleme efekti bu oturumu "yüklü" saysın, iyimser mesajlar ezilmesin.
        currentIdRef.current = targetId;
        loadedIdRef.current = targetId;
        setSendingId(targetId);
        setSessions((s) => [created, ...s]);
        setSessionsTotal((tot) => tot + 1);
        navigate(`/sohbet/${targetId}`, { replace: true }); // ana ekrandan sohbete geçen URL
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
        navigate(`/sohbet/${targetId}/belge`); // editör paneli URL'de — Back sohbete döner
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
    } finally {
      setSending(false);
      setSendingId(null);
      setStreaming(false);
      setActivity(null);
      taRef.current?.focus();
    }
  }

  return (
    <div className="flex h-dvh flex-col bg-zinc-100 text-zinc-900">
      {pendingEmailId !== null && (
        <MailConfirm id={pendingEmailId} onClose={() => setPendingEmailId(null)} />
      )}
      {/* Bütünleşik navbar: kanvasla aynı zemin, çizgisiz — Sohbet/Editör anahtarı beyaz kart olarak öne çıkar. */}
      <header className="relative flex items-center gap-3 bg-zinc-100 px-3 py-2">
        <button
          onClick={() => setSidebarOpen((o) => !o)}
          className="rounded-md px-2 py-1 text-zinc-600 hover:bg-zinc-200/70"
          title="Sohbet geçmişi"
        >
          <List size={20} aria-hidden />
        </button>
        <div className="flex items-center gap-2">
          {/* Marka ana sayfaya döner — Ayarlar/Şablonlar gibi ekranların ayrı "Geri" düğmesi yok. */}
          <a
            href={VIEW_PATH.chats}
            onClick={(e) => {
              e.preventDefault();
              navigate(VIEW_PATH.chats);
            }}
            title="Ana sayfa"
            className="flex items-center gap-2 rounded-md hover:opacity-75"
          >
            <img
              src="/korkut-logo.svg"
              alt="Korkut"
              className="h-7 w-7 rounded-lg bg-[#ffffff] p-0.5 ring-1 ring-black/10"
            />
            <span className="font-bold">Korkut</span>
          </a>
          {user.office && (
            <span className="hidden border-l border-zinc-200 pl-2 text-sm text-zinc-500 sm:inline">
              {user.office}
            </span>
          )}
        </div>
        {view === "chats" && currentId !== null && (
          <div className="absolute left-1/2 flex -translate-x-1/2 gap-1 rounded-lg border border-zinc-200 bg-white p-1 text-[13px] font-medium shadow-sm">
            <button
              onClick={() => navigate(`/sohbet/${currentId}`)}
              className={
                "rounded-md px-4 py-1 " +
                (pane === "chat" ? "bg-zinc-900 text-white shadow-sm" : "text-zinc-500 hover:text-zinc-800")
              }
            >
              Sohbet
            </button>
            <button
              onClick={() => navigate(`/sohbet/${currentId}/belge`)}
              className={
                "rounded-md px-4 py-1 " +
                (pane === "document" ? "bg-zinc-900 text-white shadow-sm" : "text-zinc-500 hover:text-zinc-800")
              }
            >
              Editör
            </button>
          </div>
        )}
        <div className="ml-auto flex items-center gap-2">
          <ThemeMenu />
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
                    <a
                      key={it.key}
                      href={VIEW_PATH[it.key]}
                      onClick={(e) => {
                        e.preventDefault();
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
                    </a>
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
                      <a
                        href={`/sohbet/${s.id}`}
                        onClick={(e) => {
                          e.preventDefault();
                          openSession(s.id);
                        }}
                        className="min-w-0 flex-1 text-left"
                        title={s.title}
                      >
                        <div className="truncate text-zinc-800">{s.title}</div>
                        <div className="truncate text-[11px] text-zinc-400">
                          {[s.created_by_name, whenLabel(s.updated_at)].filter(Boolean).join(" · ")}
                        </div>
                      </a>
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
        ) : pane === "document" && currentId !== null ? (
          <DocumentPanel key={currentId} sessionId={currentId} refreshKey={docRefresh} />
        ) : (
        <div className="flex min-w-0 flex-1 flex-col">
          {currentId === null && messages.length === 0 ? (
            // Ana ekran: saate göre karşılama + açık görevler.
            <section className="mx-auto flex w-full max-w-3xl flex-1 flex-col justify-center gap-8 overflow-y-auto px-4 py-8">
              <div className="text-center">
                <h2 className="text-3xl font-semibold tracking-tight text-zinc-900">
                  {timeGreeting()}, {firstName(user)}
                </h2>
                <p className="mt-2 text-[15px] text-zinc-500">Size nasıl yardımcı olabilirim?</p>
              </div>
              {homeTasks.length > 0 && (
                <div className="mx-auto w-full max-w-xl">
                  <div className="mb-2 flex items-baseline justify-between px-1">
                    <span className="text-[11px] font-semibold uppercase tracking-wider text-zinc-400">
                      Görevleriniz
                    </span>
                    <button
                      onClick={() => navigate(VIEW_PATH.tasks)}
                      className="text-xs text-zinc-500 hover:text-zinc-900"
                    >
                      Tümünü gör
                    </button>
                  </div>
                  <div className="divide-y divide-zinc-100 overflow-hidden rounded-xl border border-zinc-200 bg-white">
                    {homeTasks.slice(0, 5).map((t) => (
                      <label
                        key={t.id}
                        className="flex cursor-pointer items-center gap-3 px-4 py-2.5 text-sm hover:bg-zinc-50"
                      >
                        <input
                          type="checkbox"
                          checked={t.status === "done"}
                          onChange={() => toggleHomeTask(t)}
                          className="shrink-0"
                        />
                        <span
                          className={
                            "min-w-0 flex-1 truncate " +
                            (t.status === "done" ? "text-zinc-400 line-through" : "text-zinc-800")
                          }
                        >
                          {t.title}
                        </span>
                        <span className="shrink-0 text-[12px] text-zinc-400">
                          {[t.assignee_name, whenLabel(t.due_date)].filter(Boolean).join(" · ")}
                        </span>
                      </label>
                    ))}
                  </div>
                  {homeTasks.length > 5 && (
                    <div className="mt-1.5 px-1 text-center text-[12px] text-zinc-400">
                      +{homeTasks.length - 5} görev daha
                    </div>
                  )}
                </div>
              )}
            </section>
          ) : (
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
          )}

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

      <footer className="border-t border-zinc-200 bg-white px-4 py-1.5 text-center text-[12px] text-zinc-500">
        {coverage > 0
          ? `${coverage.toLocaleString("tr-TR")} yargı kararı + tüm yürürlükteki mevzuata canlı erişim · `
          : ""}
        © {user.office ?? "Korkut"}
      </footer>
    </div>
  );
}
