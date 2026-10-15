import { Fragment, useEffect, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import {
  api,
  ApiError,
  type ChatMessage,
  type DocBrief,
  type DocumentRef,
  type SourceRef,
  type SessionBrief,
  type Task,
  type ToolCallInfo,
  type User,
} from "../api";
import { DocumentPanel } from "./DocumentPanel";
import { DocumentLibrary } from "./DocumentLibrary";
import { KararAramaPanel } from "./KararAramaPanel";
import { KararlarimPanel } from "./KararlarimPanel";
import { CalculatorsPanel } from "./CalculatorsPanel";
import { ToolCallCard } from "./ToolCallCard";
import { can, P } from "../permissions";
import { RightDrawer } from "./RightDrawer";
import { SourcesDrawer } from "./SourcesDrawer";
import { ConfirmDialog } from "../a11y";
import { Settings } from "./Settings";
import { FileAttachmentCard } from "./FileAttachmentCard";

function parseChatMessageContent(content: string): { text: string; attachmentName: string | null } {
  // What the backend actually persists for an upload turn: "{caption}\n\n[Ekli belge: {fileName}]\n{extracted
  // document text…}" (SessionChatService.UploadAsync) — the extracted text is model context only, never
  // meant to render, so anything after the marker is dropped.
  const uploaded = content.match(/^(?:([\s\S]*?)\n\n)?\[Ekli belge: (.+?)\](?:\n[\s\S]*)?$/);
  if (uploaded) {
    return { text: (uploaded[1] || "").trim(), attachmentName: uploaded[2].trim() };
  }

  // The optimistic local echo set right after send, before the DB round-trip lands: "{caption}\n\nEk: {fileName}".
  const match = content.match(/^(?:([\s\S]*?)\n\n)?Ek:\s*(.+)$/);
  if (match) {
    return {
      text: (match[1] || "").trim(),
      attachmentName: match[2].trim(),
    };
  }
  return { text: content, attachmentName: null };
}
import { ClientsPanel } from "./ClientsPanel";
import { FinancePanel } from "./FinancePanel";
import { MattersPanel } from "./MattersPanel";
import { CalendarPanel } from "./CalendarPanel";
import { TasksPanel } from "./TasksPanel";
import { matchPath, navigate, usePath } from "../router";
import { setThemeMode, useThemeMode, type ThemeMode } from "../theme";
import {
  type Icon,
  BookmarkSimple,
  Calculator,
  CalendarBlank,
  CaretDown,
  Check,
  CheckSquare,
  DotsThree,
  FileText,
  Folder,
  List,
  MagnifyingGlass,
  Monitor,
  Moon,
  Paperclip,
  Plus,
  Scales,
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

// Chat avatar: only the signed-in user's initials — the assistant speaks without an icon.
function UserAvatar({ user, size = 28 }: { user: User; size?: number }) {
  return (
    <div
      style={{ width: size, height: size }}
      className="grid shrink-0 place-items-center rounded-full bg-zinc-900 text-[11px] font-semibold text-white"
      title={user.full_name ?? user.username}
    >
      {initials(user)}
    </div>
  );
}

// Animasyonlu "yazıyor" göstergesi: gerçek bir etkinlik etiketi (araç adımı) ya da ilk token
// gelene kadar balonda üç nokta oynar — jenerik "hazırlanıyor/üretiliyor" metinleri yok.
function TypingDots() {
  return (
    <span className="inline-flex items-center gap-1 py-2" role="status" aria-label="Korkut yazıyor">
      {[0, 1, 2].map((i) => (
        <span key={i} className="typing-dot h-1.5 w-1.5 rounded-full bg-zinc-400" />
      ))}
    </span>
  );
}

// Cevabın üstündeki silik "düşünme" izi (Claude/ChatGPT deseni): araç çağrılarından önce akan ara
// anlatım burada gösterilir — canlıda açık ve soluk akar, tur bitince tek satıra katlanır,
// tıklayınca yeniden açılır. Nihai cevap her zaman balondadır; bu blok cevabın parçası değildir.
function ThinkingTrail({ segments, live }: { segments: string[]; live: boolean }) {
  const [open, setOpen] = useState(false);
  const show = live || open;
  return (
    <div className="w-full">
      <button
        onClick={() => setOpen((o) => !o)}
        className="flex items-center gap-1.5 text-[12px] font-medium text-zinc-400 hover:text-zinc-600"
      >
        <CaretDown
          size={11}
          aria-hidden
          className={"transition-transform " + (show ? "" : "-rotate-90")}
        />
        {live ? "Düşünüyor…" : "Düşünme süreci"}
      </button>
      {show && (
        <div className="mt-1.5 whitespace-pre-wrap border-l-2 border-zinc-200 pl-3 text-[12.5px] leading-relaxed text-zinc-400">
          {segments.join("\n\n")}
        </div>
      )}
    </div>
  );
}

// Sol menü ikonları — Phosphor set'inden (bkz. ../icons.ts).
const NAV_ICON: Record<string, Icon> = {
  matters: Folder,
  clients: Users,
  finance: Wallet,
  tasks: CheckSquare,
  calendar: CalendarBlank,
  docs: FileText,
  kararlarim: BookmarkSimple,
  kararArama: MagnifyingGlass,
  calculators: Calculator,
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
  | "calendar"
  | "kararlarim"
  | "kararArama"
  | "calculators";

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
  kararlarim: "/kararlarim",
  kararArama: "/karar-arama",
  calculators: "/hesap-makineleri",
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
  if (!m?.id) return null;
  // Session ids are GUIDs (opaque strings), so never coerce to a number — Number("019f…") is NaN,
  // which used to make every history click resolve to no chat. Keep the raw id.
  return { id: m.id as unknown as number, pane: doc ? "document" : "chat" };
}

// /belgeler/:id — Belgeler'den TAM SAYFA açılan editör (id = belgenin sohbet kimliği). Chat'teki
// drawer'ın aksine editör tüm kanvası kaplar; kapatınca Belgeler listesine dönülür.
function docRoute(path: string): number | null {
  const m = matchPath("/belgeler/:id", path);
  return m?.id ? (m.id as unknown as number) : null;
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
  kararlarim: "Kararlarım",
  kararArama: "Karar Arama",
  calculators: "Hesap Makineleri",
};

// Sol menü 3 grup: Ofisim (operasyonel), Araçlar (Karar Arama, Hesap Makineleri),
// Sohbetler (ayrı, kendi listesiyle aşağıda).
// Each item names the permission its screen needs; a member sees only what they may use.
const NAV_GROUPS: { label: string; items: { key: SidebarView; label: string; perm?: string }[] }[] = [
  {
    label: "Ofisim",
    items: [
      { key: "matters", label: "Dosyalar", perm: P.mattersRead },
      { key: "clients", label: "Müvekkiller", perm: P.clientsRead },
      { key: "finance", label: "Mali", perm: P.financeRead },
      { key: "tasks", label: "Görevler", perm: P.agendaRead },
      { key: "calendar", label: "Takvim", perm: P.agendaRead },
      { key: "docs", label: "Belgeler", perm: P.documentsRead },
      { key: "kararlarim", label: "Kararlarım", perm: P.researchUse },
    ],
  },
  {
    label: "Araçlar",
    items: [
      { key: "kararArama", label: "Karar Arama", perm: P.researchUse },
      { key: "calculators", label: "Hesap Makineleri" },
    ],
  },
];

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
  // Belgeler'den tam sayfa açılan belge (sohbet kimliği) — yalnız docs görünümünde anlamlı.
  const fullDocId = view === "docs" ? docRoute(path) : null;
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
  const [docRefresh, setDocRefresh] = useState(0);
  // Editör drawer'ı kapatma onayı: kaydedilmemiş değişiklik varken (dirty) X/toggle'a basılırsa sorulur.
  const docDirtyRef = useRef(false);
  const [confirmDocClose, setConfirmDocClose] = useState(false);
  // Kaynaklar drawer'ı: açık olduğu mesajın index'i (URL'e yazılmaz, mesaj-scoped geçici state).
  const [sourcesFor, setSourcesFor] = useState<number | null>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const [rowMenuId, setRowMenuId] = useState<number | null>(null);
  const [renamingId, setRenamingId] = useState<number | null>(null);
  const [renameText, setRenameText] = useState("");

  // Bu sohbetin (varsa) belgesi — Belgeler listesinden conversation_id eşleşmesiyle türer; canlı
  // taslak sonrası loadDocs() listeyi tazelediği için chip yeni belgeyi de görür.
  const sessionDoc = currentId !== null ? (docs.find((d) => d.conversation_id === currentId) ?? null) : null;

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
    // Başka sohbete geçiş editörü (varsa) kapatır; bayat dirty bayrağı beforeunload'u yanlış
    // tetiklemesin. Tam sayfa belge değişiminde de aynısı geçerli.
    docDirtyRef.current = false;
  }, [currentId, fullDocId]);

  // Ana ekrana dönüşte önceki sohbetin mesajları kalmasın — render sırasında state uyarlama
  // deseni (React'ın "adjust state when props change" önerisi); efekt içinde setState'ten kaçınır.
  const routeKey = view !== "chats" ? null : route ? `sohbet:${route.id}` : "home";
  const [viewedKey, setViewedKey] = useState(routeKey);
  if (routeKey !== null && routeKey !== viewedKey) {
    setViewedKey(routeKey);
    setSourcesFor(null); // kaynaklar drawer'ı mesaja bağlıdır, görünüm değişince kapanır
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

  // Sekme kapanırken/yenilenirken kaydedilmemiş belge değişikliği varsa tarayıcı uyarısı.
  useEffect(() => {
    function onBeforeUnload(e: BeforeUnloadEvent) {
      if (docDirtyRef.current) e.preventDefault();
    }
    window.addEventListener("beforeunload", onBeforeUnload);
    return () => window.removeEventListener("beforeunload", onBeforeUnload);
  }, []);

  function closeSidebarOnMobile() {
    if (typeof window !== "undefined" && window.innerWidth < 640) setSidebarOpen(false);
  }

  // Editörü kapatma isteği (drawer VE tam sayfa): dirty ise önce onay iste, değilse hedefe dön.
  // Onay diyaloğu kapatmayı onaylarsa aynı hedefe gider (closeDocTargetRef).
  const closeDocTargetRef = useRef<string | null>(null);
  function requestCloseDoc(target: string) {
    if (docDirtyRef.current) {
      closeDocTargetRef.current = target;
      setConfirmDocClose(true);
      return;
    }
    navigate(target);
  }

  function requestCloseEditor() {
    if (currentId !== null) requestCloseDoc(`/sohbet/${currentId}`);
  }

  // Tek drawer kuralı: kaynaklar açılırken editör kapanır (dirty ise önce onay akışı çalışır ve
  // kaynaklar açılmaz; kullanıcı karar verdikten sonra tekrar tıklar).
  function openSources(messageIndex: number) {
    if (pane === "document") {
      requestCloseEditor();
      if (docDirtyRef.current) return;
    }
    setSourcesFor(messageIndex);
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
      setHomeTasks((await api.tasks("open", "gorev")).items);
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

  // Belgeler listesinden belge TAM SAYFA açılır (chat'teki drawer'ın aksine).
  function openDocFromList(d: DocBrief) {
    closeSidebarOnMobile();
    navigate(`/belgeler/${d.conversation_id}`);
  }

  // Chat'ten editör drawer'ını açar (belge kartı ve belge chip'i buradan geçer).
  function openEditorDrawer() {
    if (currentId === null) return;
    setSourcesFor(null); // tek drawer kuralı
    navigate(`/sohbet/${currentId}/belge`);
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
        sources?: SourceRef[] | null;
        document?: DocumentRef | null;
      };
      if (file) {
        // Uploads aren't streamed (the file is processed server-side, then answered).
        res = await api.uploadToSession(targetId, file, t);
        if (showsThisChat()) {
          setMessages((m) => [...m, { role: "assistant", content: res.reply, sources: res.sources }]);
        }
      } else {
        // Stream the answer: show activity steps, then grow the assistant bubble token by token.
        // Akış modeli (Claude/ChatGPT deseni): `acc` o anki model turunun metnidir. Tur bir araç
        // çağrısıyla biterse (SSE `tool` çerçevesi) o metin CEVAP DEĞİL ara anlatımdır — balondan
        // çıkarılıp üstteki silik "Düşünüyor" izine (thinking) taşınır. Yalnız son turun metni
        // (araçsız biten) cevap balonunda kalır.
        let acc = "";
        let started = false;
        let searches = 0; // canlı arama sayacı (SSE `tool_call` çerçeveleri; persist edilmez)
        const thinking: string[] = []; // araç öncesi ara anlatım segmentleri (canlı tur izi)
        const toolCalls: ToolCallInfo[] = []; // bu turda çalışan araçlar (SSE tool_call/tool_result)
        let liveSources: SourceRef[] | null = null; // SSE `sources` çerçevesi (done'dan önce gelir)
        let liveDocument: DocumentRef | null = null; // SSE `document` çerçevesi (done'dan önce gelir)
        const paint = () => {
          if (!showsThisChat()) return;
          setMessages((m) => {
            const copy = m.slice();
            copy[copy.length - 1] = { role: "assistant", content: acc, thinking: [...thinking], toolCalls: [...toolCalls] };
            return copy;
          });
        };
        res = await api.sessionChatStream(targetId, t, {
          onStep: (label) => {
            // Eski agent sürümlerinin jenerik faz etiketleri yok sayılır — onların yerinde
            // animasyonlu üç nokta oynar; yalnız gerçek araç adımları metin olarak görünür.
            if (label === "Bağlam hazırlanıyor" || label === "Yanıt üretiliyor") return;
            if (showsThisChat()) setActivity(label); // balon boşken ve araçlar dönerken görünür
          },
          onToolCall: (name, args) => {
            if (name === "search_decisions") searches += 1;
            toolCalls.push({ name, arguments: args });
            // Bu turun metni araç çağrısıyla bitti → ara anlatımdı; düşünme izine taşı.
            if (acc.trim()) {
              thinking.push(acc.trim());
              acc = "";
            }
            if (started) paint();
          },
          onToolResult: (name, content, isError) => {
            // Aynı turda aynı tool birden çok kez çağrılabilir — sonucu bekleyen İLK eşleşen kayda yaz.
            const pending = toolCalls.find((c) => c.name === name && !c.result);
            if (pending) pending.result = { content, isError };
            if (started) paint();
          },
          onSources: (items) => {
            liveSources = items;
          },
          onDocument: (doc) => {
            liveDocument = doc;
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
            paint();
          },
        });
        // Reconcile with the authoritative reply (e.g. the cloud path streams no tokens, or the
        // final restored text differs from the streamed slices) — only if still viewing this chat.
        // Kaynaklar/arama sayısı da burada mesaja işlenir ki chip ve drawer kalıcı olsun.
        if (showsThisChat()) {
          const sources = liveSources ?? res.sources ?? null;
          const finalMsg: ChatMessage = {
            role: "assistant",
            content: res.reply || acc,
            sources,
            searches: searches || undefined,
            document: liveDocument ?? res.document ?? null,
            thinking: thinking.length ? thinking : undefined,
            toolCalls: toolCalls.length ? toolCalls : undefined,
          };
          setMessages((m) => (started ? [...m.slice(0, -1), finalMsg] : [...m, finalMsg]));
        }
      }
      if (!file && (res.document || res.open_document)) {
        loadDocs(); // yeni taslak düştü → Belgeler listesi (ve sohbetin belge chip'i) tazelensin
        if (showsThisChat()) {
          setDocRefresh((n) => n + 1); // Korkut drafted into the editor → reload it
          navigate(`/sohbet/${targetId}/belge`); // editör drawer'ı URL'de — Back sohbete döner
        }
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
                      {[user.office, user.role_name].filter(Boolean).join(" · ")}
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
              <div className="flex flex-col gap-3 px-3 pt-3">
                {NAV_GROUPS.map((g) => ({ ...g, items: g.items.filter((it) => !it.perm || can(user, it.perm)) }))
                  .filter((g) => g.items.length > 0)
                  .map((g) => (
                  <div key={g.label}>
                    <div className="px-1.5 pb-1 text-[11px] font-semibold uppercase tracking-wider text-zinc-400">
                      {g.label}
                    </div>
                    <nav className="flex flex-col gap-0.5">
                      {g.items.map((it) => (
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
                ))}
              </div>

              {can(user, P.chatUse) ? (
              <>
              <div className="mt-3 flex items-center justify-between border-t border-zinc-200 px-4 pb-1 pt-3">
                <span className="text-[11px] font-semibold uppercase tracking-wider text-zinc-400">
                  Sohbetler
                </span>
                {/* A personal conversation takes chat.general; otherwise conversations start inside a matter. */}
                {can(user, P.chatGeneral) && (
                  <button
                    onClick={() => newChat()}
                    title="Yeni sohbet"
                    className="rounded-md p-1 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
                  >
                    <Plus size={18} aria-hidden />
                  </button>
                )}
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
                        className="flex min-w-0 flex-1 items-center gap-2 text-left"
                        title={s.title}
                      >
                        <div className="min-w-0 flex-1">
                          <div className="truncate text-zinc-800">{s.title}</div>
                          <div className="truncate text-[11px] text-zinc-400">
                            {[s.created_by_name, whenLabel(s.updated_at)].filter(Boolean).join(" · ")}
                          </div>
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
              </>
              ) : (
                <div className="flex-1" />
              )}
              {/* Sol alt: oturum açan kullanıcının profili. */}
              <div className="flex items-center gap-2.5 border-t border-zinc-200 px-3 py-3">
                <UserAvatar user={user} size={32} />
                <div className="min-w-0 flex-1">
                  <div className="truncate text-[13px] font-medium text-zinc-800">
                    {user.full_name ?? user.username}
                  </div>
                  <div className="truncate text-[11px] text-zinc-400">
                    {user.role_name}
                  </div>
                </div>
              </div>
            </aside>
          </>
        )}

        <div className="relative flex min-w-0 flex-1">
        {view === "settings" ? (
          <Settings user={user} />
        ) : view === "docs" ? (
          fullDocId !== null ? (
            // Belgeler'den açılan belge: TAM SAYFA editör (kapatınca listeye döner).
            <DocumentPanel
              key={fullDocId}
              sessionId={fullDocId}
              refreshKey={0}
              onClose={() => requestCloseDoc(VIEW_PATH.docs)}
              onDirtyChange={(dirty) => {
                docDirtyRef.current = dirty;
              }}
            />
          ) : (
            <DocumentLibrary docs={docs} currentId={currentId} onOpen={openDocFromList} />
          )
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
        ) : view === "kararlarim" ? (
          <KararlarimPanel />
        ) : view === "kararArama" ? (
          <KararAramaPanel />
        ) : view === "calculators" ? (
          <CalculatorsPanel />
        ) : (
        <>
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
            {/* Belge chip'i: bu sohbetin belgesi varsa (geçmişte de) editöre buradan dönülür —
                header'da artık Editör düğmesi yok. */}
            {sessionDoc && pane !== "document" && (
              <div className="pointer-events-none sticky top-0 z-10 -my-1 flex justify-end">
                <button
                  onClick={openEditorDrawer}
                  title={sessionDoc.title}
                  className="pointer-events-auto flex max-w-[60%] items-center gap-1.5 rounded-full border border-zinc-200 bg-white px-2.5 py-1.5 text-[12px] text-zinc-600 shadow-sm hover:border-zinc-400 hover:text-zinc-900"
                >
                  <FileText size={14} aria-hidden className="shrink-0 text-zinc-500" />
                  <span className="truncate">{sessionDoc.title}</span>
                </button>
              </div>
            )}
            {messages.map((m, i) => (
              <Fragment key={i}>
                {/* İçtihat bildirimi: dilekçe chip'iyle aynı dil — sağa yapışık, ikon + isim, tek
                    tetikleyici (eskiden balon üstü + altı iki ayrı düğme aynı işi yapıyordu). */}
                {m.role === "assistant" && (m.sources?.length ?? 0) > 0 && (
                  <div className="flex w-full justify-end">
                    <button
                      onClick={() => openSources(i)}
                      className="flex items-center gap-1.5 rounded-full border border-zinc-200 bg-white px-2.5 py-1 text-[12px] text-zinc-600 shadow-sm hover:border-zinc-400 hover:text-zinc-900"
                    >
                      <Scales size={13} aria-hidden className="shrink-0 text-zinc-500" />
                      <span className="truncate">
                        {m.searches ? `${m.searches} arama · ` : ""}
                        {m.sources!.length} kaynak
                      </span>
                    </button>
                  </div>
                )}
              <div
                className={
                  "flex items-end gap-2 " + (m.role === "user" ? "flex-row-reverse" : "flex-row")
                }
              >
                {m.role === "user" && <UserAvatar user={user} />}
                <div
                  className={
                    "flex min-w-0 flex-col items-start gap-1.5 " +
                    (m.role === "user" ? "max-w-[80%]" : "max-w-[85%]")
                  }
                >
                  {/* Silik düşünme izi: araç öncesi ara anlatım — canlıda soluk akar, sonra katlanır. */}
                  {m.role === "assistant" && (m.thinking?.length ?? 0) > 0 && (
                    <ThinkingTrail
                      segments={m.thinking!}
                      live={streaming && sendingId === currentId && i === messages.length - 1}
                    />
                  )}
                  {/* Araç çağrısı kartları: bu turda çalışan tool'lar, argüman + sonuçlarıyla (katlanmış). */}
                  {m.role === "assistant" && (m.toolCalls?.length ?? 0) > 0 && <ToolCallCard calls={m.toolCalls!} />}
                  <div
                    className={
                      m.role === "user"
                        ? "whitespace-pre-wrap rounded-2xl rounded-br-sm bg-zinc-900 px-3.5 py-2.5 text-[15px] leading-relaxed text-white"
                        : "md w-full rounded-2xl rounded-bl-sm border border-zinc-200 bg-white px-4 py-3 text-[15px] leading-relaxed text-zinc-900"
                    }
                  >
                    {m.role === "user" ? (
                      (() => {
                        const { text: promptText, attachmentName } = parseChatMessageContent(m.content);
                        return (
                          <div className="flex flex-col gap-2">
                            {attachmentName && <FileAttachmentCard fileName={attachmentName} mode="bubble" />}
                            {promptText && <div>{promptText}</div>}
                          </div>
                        );
                      })()
                    ) : m.content ? (
                      <ReactMarkdown remarkPlugins={[remarkGfm]}>{m.content}</ReactMarkdown>
                    ) : activity ? (
                      // Ara anlatım ize taşındı, cevap henüz akmıyor — balonda o anki adım görünür.
                      <span className="italic text-zinc-400">{activity}</span>
                    ) : (
                      <TypingDots />
                    )}
                  </div>
                  {/* Belge kartı: bu turda hazırlanan taslak — tıklayınca editör drawer'ı açılır
                      (artifact deseni; başlıkta ayrıca düğme yok). */}
                  {m.role === "assistant" && m.document && (
                    <button
                      onClick={openEditorDrawer}
                      className="flex w-full max-w-sm items-center gap-3 rounded-xl border border-zinc-200 bg-white px-3.5 py-3 text-left shadow-sm transition hover:border-zinc-400 hover:shadow"
                    >
                      <span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-zinc-100 text-zinc-700">
                        <FileText size={18} aria-hidden />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-[13.5px] font-semibold text-zinc-900">
                          {m.document.title}
                        </span>
                        <span className="block text-[12px] text-zinc-500">
                          Taslak hazırlandı · Editörde aç
                        </span>
                      </span>
                      <CaretDown size={12} aria-hidden className="shrink-0 -rotate-90 text-zinc-400" />
                    </button>
                  )}
                </div>
              </div>
              </Fragment>
            ))}
            {sending && sendingId === currentId && !streaming && (
              <div className="flex items-end gap-2">
                <div className="max-w-[80%] rounded-2xl rounded-bl-sm border border-zinc-200 bg-white px-3.5 py-2.5 text-[15px] italic text-zinc-400">
                  {activity ? activity : <TypingDots />}
                </div>
              </div>
            )}
            <div ref={endRef} />
          </section>
          )}

          {attachment && (
            <div className="mx-auto flex w-full max-w-3xl items-center px-4 pt-2">
              <FileAttachmentCard
                fileName={attachment.name}
                fileSize={attachment.size}
                mode="draft"
                onRemove={() => setAttachment(null)}
              />
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

        {/* Editör: sohbetin üstünde tam ekran (sol menü hariç) drawer. URL tek gerçek kaynak
            kuralı sürer: /sohbet/:id/belge = drawer açık (bkz. DRAWER_ROADMAP Faz 7). */}
        {currentId !== null && (
          <RightDrawer open={pane === "document"} onClose={requestCloseEditor} label="Belge editörü">
            <DocumentPanel
              key={currentId}
              sessionId={currentId}
              refreshKey={docRefresh}
              onClose={requestCloseEditor}
              onDirtyChange={(dirty) => {
                docDirtyRef.current = dirty;
              }}
            />
          </RightDrawer>
        )}
        {/* İlgili Kararlar drawer'ı — editörle aynı anda açık olmaz (editör URL'den önceliklidir). */}
        {sourcesFor !== null && pane !== "document" && (messages[sourcesFor]?.sources?.length ?? 0) > 0 && (
          <SourcesDrawer
            sources={messages[sourcesFor]!.sources!}
            onClose={() => setSourcesFor(null)}
          />
        )}
        </>
        )}
        </div>
      </div>

      {/* Editör kapatma onayı (drawer VE tam sayfa) — hedef requestCloseDoc'ta belirlenir. */}
      <ConfirmDialog
        open={confirmDocClose}
        title="Kaydedilmemiş değişiklikler"
        message="Belgede kaydedilmemiş değişiklikler var. Kaydetmeden kapatılsın mı?"
        confirmLabel="Kapat"
        cancelLabel="Vazgeç"
        destructive
        onCancel={() => setConfirmDocClose(false)}
        onConfirm={() => {
          setConfirmDocClose(false);
          docDirtyRef.current = false;
          const target =
            closeDocTargetRef.current ?? (currentId !== null ? `/sohbet/${currentId}` : VIEW_PATH.docs);
          closeDocTargetRef.current = null;
          navigate(target);
        }}
      />

      <footer className="border-t border-zinc-200 bg-white px-4 py-1.5 text-center text-[12px] text-zinc-500">
        {coverage > 0
          ? `${coverage.toLocaleString("tr-TR")} yargı kararı + tüm yürürlükteki mevzuata canlı erişim · `
          : ""}
        © {user.office ?? "Korkut"}
      </footer>
    </div>
  );
}
