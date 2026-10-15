export type User = {
  username: string;
  email: string;
  full_name: string | null;
  bar_registry_no: string | null;
  role: string; // role key: owner, lawyer, intern, secretary, accounting, reader or custom-…
  role_name: string;
  office: string | null;
  permissions: string[];
  id?: string;
};
export type Member = {
  id: number;
  username: string;
  email: string;
  full_name: string | null;
  bar_registry_no: string | null;
  role: string;
  is_active: boolean;
  has_password: boolean; // false until a member the owner added sets one
  is_self: boolean;
};
export type Invite = {
  id: number;
  email: string | null;
  full_name: string | null;
  role: string;
  expires_at: string;
  redeemed: boolean;
};
export type NewInvite = Invite & { token: string; path: string };
// Bir sohbet turunda danışılan hukuki kaynak (backend SourceRef, snake_case) — İlgili Kararlar
// drawer'ını besler. `kind` içtihat/mevzuat ayrımıdır; `source`+`id` karar detayının anahtarıdır.
export type SourceRef = {
  kind: "ictihat" | "mevzuat";
  source: string;
  id: string;
  title: string;
  date?: string | null;
  case_number?: string | null;
  decision_number?: string | null;
  court?: string | null;
  chamber?: string | null;
  url?: string | null;
  snippet?: string | null;
};

// Karar Arama sayfasının bir arama sonucu (backend LawClientsSearchResult, snake_case) — SourceRef
// ile aynı alanları taşır, ayrıca mevzuat'a özgü gazette_number/gerekce_id içerir.
export type LawSearchResult = {
  source: string;
  id: string;
  title: string;
  date?: string | null;
  case_number?: string | null;
  decision_number?: string | null;
  court?: string | null;
  chamber?: string | null;
  url?: string | null;
  snippet?: string | null;
  gazette_number?: string | null;
  gerekce_id?: string | null;
  // Bedesten kaynakları (yargitay/danistay/emsal/kyb) için: kararTuru/kesinlesmeDurumu ve
  // itemType.description (Yargıtay Kararı / Yerel Hukuk Mahkemesi Kararı / İstinaf Hukuk
  // Mahkemesi Kararı — "emsal" kaynağının kendi içinde ayırt ettiği mahkeme sistemi etiketi).
  decision_type?: string | null;
  finalization_status?: string | null;
  court_type?: string | null;
};
export type LawSearchResponse = { total: number | null; count: number; results: LawSearchResult[] };

// Mevzuat madde ağacı (backend LawClientsMaddeTreeNode/Response, snake_case): kısım/bölüm başlıkları
// ve MADDE yaprakları iç içe. Yaprak düğümlerde madde_no+madde_id dolu; başlık düğümlerinde boş.
export type MaddeTreeNode = {
  title: string | null;
  madde_no: string | null;
  madde_id: string | null;
  gerekce_id: string | null;
  children: MaddeTreeNode[];
};
export type MaddeTreeResponse = { count: number; nodes: MaddeTreeNode[] };

// Kararlarım: Karar Arama sonuçlarından ya da sohbetteki İlgili Kararlar'dan kaydedilen kararlar
// (backend SavedDecisionResponse, snake_case). `external_id` LawSearchResult/SourceRef'in `id`'sidir.
export type SavedDecision = {
  id: string;
  source: string;
  external_id: string;
  title: string;
  date?: string | null;
  case_number?: string | null;
  decision_number?: string | null;
  court?: string | null;
  chamber?: string | null;
  url?: string | null;
  snippet?: string | null;
  created_at: string;
};

// Turun ürettiği/güncellediği taslak belge — chat'teki belge kartı + editörü açma sinyali.
export type DocumentRef = { doc_id: number; title: string; summary: string | null };

export type ChatMessage = {
  role: "user" | "assistant";
  content: string;
  // Asistan turunun dayandığı kaynaklar (varsa) — geçmiş yüklemede sunucudan, canlı turda
  // SSE `sources` çerçevesinden gelir.
  sources?: SourceRef[] | null;
  // Yalnız canlı turda bilinir (SSE `tool` çerçevelerinden sayılır); geçmişte persist edilmez.
  searches?: number;
  // Bu turda hazırlanan taslak belge (SSE `document` çerçevesi) — yalnız canlı turda bilinir;
  // geçmişte belge kartı Belgeler listesinden (conversation_id eşleşmesi) türetilir.
  document?: DocumentRef | null;
  // Araç çağrılarından ÖNCE akan ara anlatım parçaları ("düşünme" izi) — balona değil, üstteki
  // silik katlanabilir bloğa gider. Yalnız canlı turda bilinir; persist Faz B (parts_json) işi.
  thinking?: string[];
  // Bu turda çalışan araç çağrıları, argüman + sonuçlarıyla (SSE `tool_call`/`tool_result`
  // çerçeveleri) — ToolCallCard'da gösterilir. Yalnız canlı turda bilinir; persist edilmez.
  toolCalls?: ToolCallInfo[];
};
export type ToolCallInfo = { name: string; arguments: unknown; result?: { content: string; isError: boolean } };
export type SessionBrief = {
  id: number;
  title: string;
  matter_id: number | null;
  created_by_name: string | null;
  updated_at: string | null;
};
export type SessionDetail = SessionBrief & { messages: ChatMessage[] };
export type SessionPage = { items: SessionBrief[]; total: number };
export type DocVersion = { id: number; author: string; note: string | null; created_at: string | null };
export type PendingEdit = { id: number; content_md: string; summary: string | null };
export type DocumentData = {
  id: number;
  title: string;
  content_md: string;
  status: string;
  versions: DocVersion[];
  pending: PendingEdit | null;
};
export type DocKind = "dilekce" | "sozlesme" | null;
export type DocBrief = {
  id: number;
  conversation_id: number;
  title: string;
  kind: DocKind;
  updated_at: string | null;
};
export type StyleFields = {
  letterhead: string;
  signature: string;
  tone: string;
  boilerplate: string;
};
export type StyleData = {
  office: StyleFields;
  user: StyleFields;
  effective: StyleFields;
  can_edit_office: boolean;
};
export type PetitionType = { id: string; ad: string; mahkeme: string | null; sik: boolean };
export type PetitionCategory = { kategori: string; dilekceler: PetitionType[] };
export type ExportFormat = "udf" | "docx";
// Office-wide mail preference. The system sends mail from ONE operator account; this only
// records where notification mail goes and whether informational mail is enabled at all.
export type MailPreference = {
  notifications_enabled: boolean;
  notify_email: string | null;
  can_edit: boolean;
};
export type MailPreferenceInput = {
  notifications_enabled: boolean;
  notify_email: string | null;
};
export type PersonaData = {
  tone: string;
  expertise: string;
  extra_rules: string;
  can_edit: boolean;
};
export type PersonaInput = { tone: string; expertise: string; extra_rules: string };

export type MemoryItem = {
  id: number;
  kind: string;
  text: string;
  status: string; // "active" | "pending"
  author: string | null;
  created_at: string | null;
};

// ---- matter domain (dosya / müvekkil / duruşma) ----
export type Hearing = {
  id: number;
  matter_id: number;
  date: string | null;
  time: string | null;
  location: string | null;
  type: string | null;
  result: string | null;
  reminder_status: string;
  notes: string | null;
};
export type ClientBrief = {
  id: string;
  full_name: string;
  /** "individual" | "corporate" — tüzel kişiler listede rozetle ayrışır. */
  kind?: string;
  phone: string | null;
  email: string | null;
  national_id: string | null;
  address: string | null;
  date_of_birth: string | null;
  /** Telefon, e-posta ve adres bu kullanıcıdan gizli (izni yok). */
  contact_hidden?: boolean;
  /** TCKN ve doğum tarihi bu kullanıcıdan gizli (izni yok). */
  identity_hidden?: boolean;
};
export type ClientPatch = {
  full_name?: string;
  national_id?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  date_of_birth?: string | null;
};
export type Invoice = {
  id: number;
  client_id: number | null;
  matter_id: number | null;
  amount: number;
  vat_rate: number;
  gross: number;
  description: string | null;
  status: string;
  issued_at: string | null;
};
export type Expense = {
  id: number;
  matter_id: number | null;
  client_id: number | null;
  amount: number;
  kind: string | null;
  description: string | null;
  spent_at: string | null;
};
export type FinanceSummary = {
  invoiced: number;
  paid: number;
  outstanding: number;
  expenses: number;
};
export type Task = {
  id: number;
  category: string; // gorev | hatirlatma | randevu
  title: string;
  description: string | null;
  type: string | null;
  assignee_id: number | null;
  assignee_name: string | null;
  matter_id: number | null;
  client_id: number | null;
  status: string;
  due_date: string | null;
  end_at: string | null;
  all_day: boolean;
  location: string | null;
  rrule: string | null;
  remind_before_min: number | null;
  reminder_status: string;
  created_by?: string | null;
};

// Body for creating an agenda record (görev / hatırlatma / randevu).
export type TaskInput = {
  category?: string;
  title: string;
  description?: string | null;
  type?: string | null;
  matter_id?: number | null;
  assignee_id?: number | null;
  client_id?: number | null;
  due_date?: string | null;
  end_at?: string | null;
  all_day?: boolean;
  location?: string | null;
  rrule?: string | null;
  remind_before_min?: number | null;
};
export type DocumentRecord = {
  id: number;
  matter_id: number | null;
  direction: string; // incoming | outgoing | internal
  title: string;
  reference_no: string | null;
  doc_date: string | null;
  notes: string | null;
};
export type ReportsOverview = {
  matters: { total: number; by_status: Record<string, number> };
  clients: { total: number };
  finance: FinanceSummary;
  tasks: { open: number; done: number; overdue: number };
  agenda: {
    days: number;
    hearings_upcoming: number;
    deadlines_upcoming: number;
    deadlines_overdue: number;
  };
  evrak: { total: number; by_direction: Record<string, number> };
};
export type MatterBrief = {
  id: string;
  title: string;
  client_name: string | null;
  file_no: string | null;
  court: string | null;
  case_type: string | null;
  counterparty: string | null;
  status: string;
  opened_at: string | null;
  updated_at: string | null;
};
export type Deadline = {
  id: number;
  matter_id: number;
  due_date: string | null;
  title: string;
  type: string | null;
  status: string;
  notes: string | null;
};
export type DeadlineInput = { due_date: string; title: string; type?: string | null; notes?: string | null };
export type AgendaItem = {
  kind: "hearing" | "deadline";
  date: string;
  id: number;
  matter_id: number;
  type: string | null;
  title?: string; // deadline
  time?: string | null; // hearing
  location?: string | null; // hearing
  status?: string; // deadline
};
export type CalendarEvent = {
  id: number;
  category: string; // hearing | deadline | appointment | reminder
  type: string | null;
  title: string;
  start_at: string | null;
  end_at: string | null;
  all_day: boolean;
  matter_id: number | null;
  matter_title: string | null;
  client_id: number | null;
  client_name: string | null;
  assignee_id: number | null;
  assignee_name: string | null;
  location: string | null;
  status: string;
  result: string | null;
  notes: string | null;
  rrule: string | null;
  remind_before_min: number | null;
  reminder_status: string;
  recurring?: boolean; // true when this row is an expanded occurrence of a recurring event
};
export type MatterDocument = {
  id: number;
  conversation_id: number;
  title: string;
  status: string;
  updated_at: string | null;
};
export type MatterDetail = MatterBrief & {
  client: ClientBrief | null;
  notes: string | null;
  hearings: Hearing[];
  deadlines: Deadline[];
  documents: MatterDocument[];
};
export type MatterInput = {
  title: string;
  // The matter is always bound to an existing client by id — no free-text client name.
  client_id: string;
  file_no?: string | null;
  court?: string | null;
  case_type?: string | null;
  counterparty?: string | null;
};
export type MatterPatch = Partial<MatterInput> & { status?: string; notes?: string };
export type HearingInput = {
  date: string;
  time?: string | null;
  location?: string | null;
  type?: string | null;
  notes?: string | null;
};
export type Coverage = {
  total_decisions: number;
  by_court: { label: string; total: number }[];
  decision_sources: string[];
  legislation: string;
};

// GET /api/calc/schema (KDM roadmap Faz 3): backend'in [Range] Contract attribute'larından
// reflection'la türettiği canlı required/min/max — CalculatorForm bunu calcDefinitions.ts'teki
// elle yazılmış değerlerin üstüne uygular, böylece render edilen kısıt backend'le hiç drift etmez.
export type CalcFieldConstraint = { required: boolean; minimum: number | null; maximum: number | null };
export type CalcSchemaResponse = Record<string, Record<string, CalcFieldConstraint>>;

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

// Protected calls go through the session layer: a 401 triggers exactly one
// single-flight refresh + one retry, then hard expiry — never a loop (KDM-030).
async function request<T>(path: string, opts: RequestInit = {}): Promise<T> {
  const { sessionRequest } = await import("./session");
  return sessionRequest<T>(path, opts, { auth: true });
}

// Session probe (bootstrap): a plain call with NO refresh-retry, so a genuinely
// anonymous visitor resolves to 401 → anon without a wasted refresh or redirect loop.
async function probe<T>(path: string, opts: RequestInit = {}): Promise<T> {
  const { sessionRequest } = await import("./session");
  return sessionRequest<T>(path, opts, { auth: false });
}

// Streaming/multipart/blob endpoints can't ride sessionRequest (they need the raw
// Response), but they still get the same 401 → refresh → retry-once contract.
async function protectedFetch(path: string, opts: RequestInit = {}): Promise<Response> {
  const { sessionFetch } = await import("./session");
  return sessionFetch(path, opts);
}

export const api = {
  me: () => probe<User>("/api/me"),
  coverage: () => request<Coverage>("/api/coverage"),
  members: () => request<Member[]>("/api/members"),
  addMember: (email: string, username: string, full_name: string, bar_registry_no?: string) =>
    request<Member>("/api/members", {
      method: "POST",
      body: JSON.stringify({ email, username, full_name, bar_registry_no }),
    }),
  removeMember: (id: number) =>
    request<{ ok: boolean }>(`/api/members/${id}`, { method: "DELETE" }),
  invites: () => request<Invite[]>("/api/invites"),
  createInvite: (input: { email?: string | null; full_name?: string | null; role?: string }) =>
    request<NewInvite>("/api/invites", { method: "POST", body: JSON.stringify(input) }),
  // Davet linkine tıklayan kişinin katılım ekranı: token tüketilmeden davet bilgisi.
  peekInvite: (token: string) =>
    request<{ office_name: string; email: string; full_name: string | null; role: string }>(
      `/api/invites/peek?token=${encodeURIComponent(token)}`,
    ),
  // Parola belirleyerek katılım: hesap açılır, davet tüketilir, oturum çerezleri set edilir.
  acceptInviteLocal: (token: string, password: string, fullName?: string | null) =>
    request<{ ok: boolean; email: string }>("/api/invites/accept-local", {
      method: "POST",
      body: JSON.stringify({ token, password, full_name: fullName ?? null }),
    }),
  revokeInvite: (id: number) =>
    request<{ ok: boolean }>(`/api/invites/${id}`, { method: "DELETE" }),
  updateMe: (patch: { full_name?: string; bar_registry_no?: string | null }) =>
    request<User>("/api/me", { method: "PATCH", body: JSON.stringify(patch) }),
  getStyle: () => request<StyleData>("/api/style"),
  saveUserStyle: (fields: StyleFields) =>
    request<StyleFields>("/api/style/user", { method: "PUT", body: JSON.stringify(fields) }),
  saveOfficeStyle: (fields: StyleFields) =>
    request<StyleFields>("/api/style/office", { method: "PUT", body: JSON.stringify(fields) }),
  getPersona: () => request<PersonaData>("/api/persona"),
  savePersona: (input: PersonaInput) =>
    request<PersonaData>("/api/persona", { method: "PUT", body: JSON.stringify(input) }),
  listMemory: () => request<{ items: MemoryItem[] }>("/api/memory"),
  addMemory: (text: string, kind: string) =>
    request<MemoryItem>("/api/memory", {
      method: "POST",
      body: JSON.stringify({ text, kind }),
    }),
  approveMemory: (id: number) =>
    request<MemoryItem>(`/api/memory/${id}/approve`, { method: "POST" }),
  deleteMemory: (id: number) =>
    request<{ ok: boolean }>(`/api/memory/${id}`, { method: "DELETE" }),
  matters: (status?: string) =>
    request<{ items: MatterBrief[]; total: number }>(
      `/api/matters${status ? `?status=${encodeURIComponent(status)}` : ""}`,
    ),
  getMatter: (id: string) => request<MatterDetail>(`/api/matters/${id}`),
  createMatter: (input: MatterInput) =>
    // The create endpoint returns the brief shape (no hearings/deadlines/documents/client) —
    // callers that need the full detail should follow up with getMatter(id).
    request<MatterBrief>("/api/matters", { method: "POST", body: JSON.stringify(input) }),
  updateMatter: (id: string, patch: MatterPatch) =>
    request<MatterDetail>(`/api/matters/${id}`, { method: "PATCH", body: JSON.stringify(patch) }),
  deleteMatter: (id: string) =>
    request<{ ok: boolean }>(`/api/matters/${id}`, { method: "DELETE" }),
  addHearing: (id: string, input: HearingInput) =>
    request<Hearing>(`/api/matters/${id}/hearings`, { method: "POST", body: JSON.stringify(input) }),
  agenda: (days = 14) =>
    request<{ items: AgendaItem[]; days: number }>(`/api/calendar/upcoming?days=${days}`),
  addDeadline: (id: string, input: DeadlineInput) =>
    request<Deadline>(`/api/matters/${id}/deadlines`, { method: "POST", body: JSON.stringify(input) }),
  completeDeadline: (did: number) =>
    request<Deadline>(`/api/deadlines/${did}/complete`, { method: "POST" }),
  clients: () => request<{ items: ClientBrief[] }>("/api/clients"),
  createClient: (input: ClientPatch & { full_name: string }) =>
    request<ClientBrief>("/api/clients", { method: "POST", body: JSON.stringify(input) }),
  updateClient: (id: string, patch: ClientPatch) =>
    request<ClientBrief>(`/api/clients/${id}`, { method: "PATCH", body: JSON.stringify(patch) }),
  invoices: () => request<{ items: Invoice[] }>("/api/finance/invoices"),
  createInvoice: (input: Partial<Invoice>) =>
    request<Invoice>("/api/finance/invoices", { method: "POST", body: JSON.stringify(input) }),
  updateInvoice: (id: number, patch: { status?: string; amount?: number }) =>
    request<Invoice>(`/api/finance/invoices/${id}`, { method: "PATCH", body: JSON.stringify(patch) }),
  deleteInvoice: (id: number) =>
    request<{ ok: boolean }>(`/api/finance/invoices/${id}`, { method: "DELETE" }),
  expenses: () => request<{ items: Expense[] }>("/api/finance/expenses"),
  createExpense: (input: Partial<Expense>) =>
    request<Expense>("/api/finance/expenses", { method: "POST", body: JSON.stringify(input) }),
  deleteExpense: (id: number) =>
    request<{ ok: boolean }>(`/api/finance/expenses/${id}`, { method: "DELETE" }),
  financeSummary: () => request<FinanceSummary>("/api/finance/summary"),
  tasks: (status?: string, category?: string) => {
    const q = new URLSearchParams();
    if (status) q.set("status", status);
    if (category) q.set("category", category);
    const qs = q.toString();
    return request<{ items: Task[] }>(`/api/tasks${qs ? `?${qs}` : ""}`);
  },
  createTask: (input: TaskInput) =>
    request<Task>("/api/tasks", { method: "POST", body: JSON.stringify(input) }),
  updateTask: (id: number, patch: Partial<TaskInput> & { status?: string }) =>
    request<Task>(`/api/tasks/${id}`, { method: "PATCH", body: JSON.stringify(patch) }),
  deleteTask: (id: number) =>
    request<{ ok: boolean }>(`/api/tasks/${id}`, { method: "DELETE" }),
  evrak: (direction?: string) =>
    request<{ items: DocumentRecord[] }>(`/api/evrak${direction ? `?direction=${direction}` : ""}`),
  createEvrak: (input: {
    direction: string;
    title: string;
    reference_no?: string | null;
    doc_date?: string | null;
  }) => request<DocumentRecord>("/api/evrak", { method: "POST", body: JSON.stringify(input) }),
  deleteEvrak: (id: number) =>
    request<{ ok: boolean }>(`/api/evrak/${id}`, { method: "DELETE" }),
  reportsOverview: () => request<ReportsOverview>("/api/reports/overview"),
  calendarEvents: (params?: {
    start?: string;
    end?: string;
    category?: string;
    matter_id?: number;
  }) => {
    const q = new URLSearchParams();
    if (params?.start) q.set("start", params.start);
    if (params?.end) q.set("end", params.end);
    if (params?.category) q.set("category", params.category);
    if (params?.matter_id != null) q.set("matterId", String(params.matter_id));
    const qs = q.toString();
    return request<{ items: CalendarEvent[] }>(`/api/calendar/events${qs ? `?${qs}` : ""}`);
  },
  exportMarkdown: async (format: ExportFormat, title: string, markdown: string): Promise<Blob> => {
    const res = await protectedFetch("/api/export", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ format, title, markdown }),
    });
    if (!res.ok) throw new ApiError(res.status, "Dışa aktarma başarısız");
    return res.blob();
  },
  logout: () => request<{ ok: boolean }>("/api/logout", { method: "POST" }),

  // --- .NET auth contract (KDM-030). Gated behind API_MODE; cookie sessions only. ---
  // Local password sign-in. Returns { totp_required: true } to demand the 2FA step,
  // otherwise the session cookie is set and the caller is signed in.
  login: (email: string, password: string) =>
    probe<{ totp_required: boolean }>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    }),
  // Policy-driven TOTP challenge. `recovery` accepts a single-use recovery code.
  verifyTotp: (code: string, recovery = false) =>
    probe<{ ok: boolean }>("/api/auth/2fa", {
      method: "POST",
      body: JSON.stringify(recovery ? { recovery_code: code } : { code }),
    }),
  // The signed-in member changes their own password; the session is issued again, so they stay in.
  changePassword: (current_password: string, new_password: string) =>
    request<void>("/api/me/password", {
      method: "POST",
      body: JSON.stringify({ current_password, new_password }),
    }),
  requestPasswordReset: (email: string) =>
    probe<{ ok: boolean }>("/api/auth/password/reset-request", {
      method: "POST",
      body: JSON.stringify({ email }),
    }),
  resetPassword: (token: string, password: string) =>
    probe<{ ok: boolean }>("/api/auth/password/reset", {
      method: "POST",
      body: JSON.stringify({ token, password }),
    }),
  sessions: (limit = 20, offset = 0, matterId?: string) =>
    request<SessionPage>(
      `/api/sessions?limit=${limit}&offset=${offset}${matterId ? `&matterId=${encodeURIComponent(matterId)}` : ""}`,
    ),
  createSession: (matterId?: string) =>
    request<SessionBrief>(
      `/api/sessions${matterId ? `?matterId=${matterId}` : ""}`,
      { method: "POST" },
    ),
  getSession: (id: number) => request<SessionDetail>(`/api/sessions/${id}`),
  deleteSession: (id: number) =>
    request<{ ok: boolean }>(`/api/sessions/${id}`, { method: "DELETE" }),
  renameSession: (id: number, title: string) =>
    request<SessionBrief>(`/api/sessions/${id}`, {
      method: "PATCH",
      body: JSON.stringify({ title }),
    }),
  cloneSession: (id: number) =>
    request<SessionBrief>(`/api/sessions/${id}/clone`, { method: "POST" }),
  getMailPreference: () => request<MailPreference>("/api/mail-preferences"),
  saveMailPreference: (input: MailPreferenceInput) =>
    request<MailPreference>("/api/mail-preferences", {
      method: "PUT",
      body: JSON.stringify(input),
    }),
  sessionChat: (id: number, text: string) =>
    request<{
      reply: string;
      title: string;
      open_document: number | null;
      pending_email: number | null;
      sources: SourceRef[] | null;
      document?: DocumentRef | null;
    }>(`/api/sessions/${id}/chat`, { method: "POST", body: JSON.stringify({ text }) }),
  // Streaming twin of sessionChat: activity steps + answer tokens arrive live over SSE. `onStep`
  // and `onToken` fire as events land; `onToolCall` fires with a tool's raw name + JSON arguments
  // right before it runs, `onToolResult` with the same name + result content/error flag right
  // after (a structured signal for tool-call cards, unlike the human-facing `onStep` copy);
  // `onSources` gets the turn's legal sources just before `done`. The promise resolves with the
  // same payload as sessionChat (or rejects with an ApiError carrying the friendly detail from an
  // `error` event).
  sessionChatStream: async (
    id: number,
    text: string,
    on: {
      onStep?: (label: string) => void;
      onToken?: (t: string) => void;
      onToolCall?: (name: string, args: unknown) => void;
      onToolResult?: (name: string, content: string, isError: boolean) => void;
      onSources?: (items: SourceRef[]) => void;
      onDocument?: (doc: DocumentRef) => void;
    },
  ) => {
    const res = await protectedFetch(`/api/sessions/${id}/chat/stream`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ text }),
    });
    if (!res.ok || !res.body) {
      let detail = "yanıt alınamadı";
      try {
        const b = await res.json();
        if (b?.detail) detail = b.detail;
      } catch {
        /* keep default */
      }
      throw new ApiError(res.status, detail);
    }

    type Done = {
      reply: string;
      title: string;
      open_document: number | null;
      pending_email: number | null;
      sources: SourceRef[] | null;
      document?: DocumentRef | null;
    };
    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buf = "";
    let done: Done | null = null;
    let errorDetail: string | null = null;

    const dispatch = (frame: string) => {
      let event = "message";
      let data = "";
      for (const line of frame.split("\n")) {
        if (line.startsWith("event:")) event = line.slice(6).trim();
        else if (line.startsWith("data:")) data += line.slice(5).trim();
      }
      if (!data) return;
      const parsed = JSON.parse(data);
      if (event === "step") on.onStep?.(parsed.label as string);
      else if (event === "token") on.onToken?.(parsed.text as string);
      else if (event === "tool_call") on.onToolCall?.(parsed.name as string, parsed.arguments);
      else if (event === "tool_result") on.onToolResult?.(parsed.name as string, parsed.content as string, parsed.is_error as boolean);
      else if (event === "sources") on.onSources?.(parsed.items as SourceRef[]);
      else if (event === "document") on.onDocument?.(parsed as DocumentRef);
      else if (event === "done") done = parsed as Done;
      else if (event === "error") errorDetail = parsed.detail as string;
    };

    for (;;) {
      const { done: finished, value } = await reader.read();
      if (finished) break;
      buf += decoder.decode(value, { stream: true });
      let idx: number;
      while ((idx = buf.indexOf("\n\n")) !== -1) {
        dispatch(buf.slice(0, idx));
        buf = buf.slice(idx + 2);
      }
    }
    if (errorDetail !== null) throw new ApiError(503, errorDetail);
    if (done === null) throw new ApiError(500, "yanıt tamamlanamadı");
    return done as Done;
  },
  uploadToSession: async (id: number, file: File, text: string) => {
    const fd = new FormData();
    fd.append("file", file);
    fd.append("text", text);
    // No JSON Content-Type — the browser sets the multipart boundary.
    const res = await protectedFetch(`/api/sessions/${id}/upload`, {
      method: "POST",
      body: fd,
    });
    if (!res.ok) {
      let detail = "Yükleme başarısız";
      try {
        const b = await res.json();
        if (b?.detail) detail = b.detail;
      } catch {
        /* keep default */
      }
      throw new ApiError(res.status, detail);
    }
    return (await res.json()) as {
      reply: string;
      title: string;
      user_display: string;
      sources: SourceRef[] | null;
    };
  },
  // Karar Arama sayfası: tek bir kaynakta (yargitay/danistay/emsal/kyb/anayasa/mevzuat...) arama.
  // Alan seti backend'in LawClientsSearchRequest'iyle birebir — her kaynak yalnız desteklediği
  // filtreyi okur, gerisini yok sayar (bkz. Korkut.Tools/Law/SearchDecisionsTool.cs şeması).
  legalSearch: (params: {
    source: string;
    query: string;
    page?: number;
    limit?: number;
    sort?: "relevance" | "date";
    dateStart?: string;
    dateEnd?: string;
    chamber?: string;
    decisionType?: string;
    applicationType?: string;
    scope?: string;
    legislationType?: string;
    searchScope?: string;
    tamCumle?: boolean;
    mevzuatNo?: string;
    gazetteNumber?: string;
  }) => {
    const qs = new URLSearchParams({ source: params.source, query: params.query });
    if (params.page) qs.set("page", String(params.page));
    if (params.limit) qs.set("limit", String(params.limit));
    if (params.sort) qs.set("sort", params.sort);
    if (params.dateStart) qs.set("dateStart", params.dateStart);
    if (params.dateEnd) qs.set("dateEnd", params.dateEnd);
    if (params.chamber) qs.set("chamber", params.chamber);
    if (params.decisionType) qs.set("decisionType", params.decisionType);
    if (params.applicationType) qs.set("applicationType", params.applicationType);
    if (params.scope) qs.set("scope", params.scope);
    if (params.legislationType) qs.set("legislationType", params.legislationType);
    if (params.searchScope) qs.set("searchScope", params.searchScope);
    if (params.tamCumle) qs.set("tamCumle", "true");
    if (params.mevzuatNo) qs.set("mevzuatNo", params.mevzuatNo);
    if (params.gazetteNumber) qs.set("gazetteNumber", params.gazetteNumber);
    return request<LawSearchResponse>(`/api/legal/search?${qs.toString()}`);
  },
  // Bir SourceRef'in (source+id) tam metni — İlgili Kararlar drawer'ındaki detay görünümü.
  // articleId verilirse (mevzuat madde ağacından bir madde_id) yalnız o maddenin metni döner.
  legalDocument: (source: string, id: string, articleId?: string) => {
    const qs = new URLSearchParams({ source, id });
    if (articleId) qs.set("articleId", articleId);
    return request<{ source: string; id: string; content_md: string }>(`/api/legal/documents?${qs.toString()}`);
  },
  // Bir mevzuatın madde ağacı (kısım/bölüm/madde) — Mevzuat detayının AĞAÇ YAPISI görünümünü besler.
  legalMaddeTree: (mevzuatId: string) =>
    request<MaddeTreeResponse>(`/api/legal/madde-tree?mevzuatId=${encodeURIComponent(mevzuatId)}`),
  // Kararlarım: kaydedilmiş kararların listesi, kaydetme (idempotent) ve kaldırma.
  savedDecisions: () => request<{ items: SavedDecision[] }>("/api/saved-decisions"),
  saveDecision: (input: {
    source: string;
    externalId: string;
    title: string;
    date?: string | null;
    caseNumber?: string | null;
    decisionNumber?: string | null;
    court?: string | null;
    chamber?: string | null;
    url?: string | null;
    snippet?: string | null;
  }) =>
    request<SavedDecision>("/api/saved-decisions", {
      method: "POST",
      body: JSON.stringify({
        source: input.source,
        external_id: input.externalId,
        title: input.title,
        date: input.date ?? null,
        case_number: input.caseNumber ?? null,
        decision_number: input.decisionNumber ?? null,
        court: input.court ?? null,
        chamber: input.chamber ?? null,
        url: input.url ?? null,
        snippet: input.snippet ?? null,
      }),
    }),
  removeSavedDecision: (id: string) => request<void>(`/api/saved-decisions/${id}`, { method: "DELETE" }),
  // Hesap Makineleri (Araçlar sayfası): 33 ICalculatorService metodunun hepsi tek generic çağrıyla
  // sürülür (bkz. calcDefinitions.ts) — response şekli hesap makinesine göre değiştiği için tip
  // introspect ederek render edilir (CalculatorForm.tsx). GET'te dizi değerler (örn. para_birimleri)
  // aynı anahtarla tekrarlanan query parametreleri olarak eklenir (.NET'in query-array bağlama biçimi).
  calc: (endpoint: string, body: Record<string, unknown>, method: "GET" | "POST" = "POST") => {
    if (method === "GET") {
      const qs = new URLSearchParams();
      for (const [key, value] of Object.entries(body)) {
        if (value === undefined || value === null) continue;
        if (Array.isArray(value)) value.forEach((v) => qs.append(key, String(v)));
        else qs.set(key, String(value));
      }
      const suffix = qs.toString();
      return request<Record<string, unknown>>(`${endpoint}${suffix ? `?${suffix}` : ""}`);
    }
    return request<Record<string, unknown>>(endpoint, { method: "POST", body: JSON.stringify(body) });
  },
  // Tek uçuşluk cache: panel açıkken tekrar tekrar hesap makinesi değiştirmek yeniden fetch etmesin.
  calcSchema: (() => {
    let cache: Promise<CalcSchemaResponse> | null = null;
    return () => (cache ??= request<CalcSchemaResponse>("/api/calc/schema"));
  })(),
  // AI taslak belgeleri (Belgeler sekmesi) — /api/documents operasyonel evrak kütüphanesidir,
  // taslak listesi kendi yolundan gelir (gateway path'e göre yönlendirir).
  documents: () => request<DocBrief[]>("/api/draft-documents"),
  petitionTypes: () => request<PetitionCategory[]>("/api/petition-types"),
  getDocument: (sessionId: number) =>
    request<DocumentData>(`/api/sessions/${sessionId}/document`),
  draftDocument: (docId: number, instruction: string, petitionType?: string | null) =>
    request<PendingEdit>(`/api/documents/${docId}/draft`, {
      method: "POST",
      body: JSON.stringify({ instruction, petition_type: petitionType ?? null }),
    }),
  acceptEdit: (docId: number) =>
    request<DocumentData>(`/api/documents/${docId}/accept`, { method: "POST" }),
  revertEdit: (docId: number) =>
    request<DocumentData>(`/api/documents/${docId}/revert`, { method: "POST" }),
  saveDocument: (docId: number, title: string, content_md: string) =>
    request<DocumentData>(`/api/documents/${docId}`, {
      method: "PUT",
      body: JSON.stringify({ title, content_md }),
    }),
  exportUrl: (docId: number, format: "docx" | "udf") =>
    `/api/documents/${docId}/export?format=${format}`,
};

// --- AI settings: the model gateway (llmr) management API, through the owner-only proxy. ---
export type AiProviderType = {
  id: string;
  name: string;
  credential: "required" | "optional" | "none";
  default_base_url: string | null;
  default_reach: string | null;
  reach_required: boolean;
  kinds: string[];
};
export type AiProvider = {
  id: string;
  type: string;
  base_url: string | null;
  reach: string | null;
  enabled: boolean;
  credential: string | null;
  problem?: string;
};
export type AiModel = {
  id: string;
  route: string;
  kind: string;
  enabled: boolean;
  listed: boolean | null;
  capabilities_source: string | null;
};
export type AiRouteSet = { name: string; routes: string[]; usable?: string[]; unavailable?: { route: string; why: string }[] };
export type AiTestResult = { provider: string; access: "ready" | "denied" | "unknown"; detail?: string; models_listed?: number };
export type AiCost = { currency: string; amount: string };
export type AiUsageRow = {
  model: string | null;
  requests: number;
  tokens: { total: number };
  cost: AiCost[];
};
export type AiUsage = { total: AiUsageRow & { cost_complete?: boolean }; data: AiUsageRow[]; cost_complete: boolean };

const AI = "/api/settings/ai/manage";
const json = (body: unknown): RequestInit => ({ body: JSON.stringify(body) });

export const aiApi = {
  providerTypes: () => request<{ data: AiProviderType[] }>(`${AI}/provider-types`).then((r) => r.data),
  providers: () => request<{ data: AiProvider[] }>(`${AI}/providers`).then((r) => r.data),
  addProvider: (body: { id: string; type: string; credential?: string; base_url?: string; reach?: string }) =>
    request<AiProvider>(`${AI}/providers`, { method: "POST", ...json(body) }),
  removeProvider: (id: string) => request<unknown>(`${AI}/providers/${encodeURIComponent(id)}`, { method: "DELETE" }),
  testProvider: (id: string) => request<AiTestResult>(`${AI}/providers/${encodeURIComponent(id)}/test`, { method: "POST" }),
  models: (provider: string) =>
    request<{ data: AiModel[]; listing_error: string | null }>(`${AI}/providers/${encodeURIComponent(provider)}/models`),
  enableModel: (provider: string, model: string, kind: "chat" | "embedding", describe: boolean) =>
    request<unknown>(`${AI}/providers/${encodeURIComponent(provider)}/models/${encodeURIComponent(model)}`, {
      method: "PUT",
      // A model llmr knows needs only enabling; one it does not (an OpenAI-compatible host) has to
      // say what it can do, and Korkut's chat needs tools and streaming.
      ...json(describe && kind === "chat"
        ? { enabled: true, kind, capabilities: { tools: true, streaming: true } }
        : { enabled: true, kind }),
    }),
  routes: () => request<{ data: AiRouteSet[] }>(`${AI}/routes`).then((r) => r.data),
  setRoute: (name: string, routes: string[]) =>
    request<AiRouteSet>(`${AI}/routes/${encodeURIComponent(name)}`, { method: "PUT", ...json({ routes }) }),
  usage: (fromUnixSeconds: number) =>
    request<AiUsage>(`${AI}/usage?group_by=model&from=${fromUnixSeconds}`),
};

// ---- Telegram: the office bot (owner) and each member's own link -----------------------------
export type TelegramLinkedMember = { user_id: string; name: string; telegram_username: string | null; linked_at: string };
export type TelegramSettings = {
  configured: boolean;
  enabled: boolean;
  bot_username: string | null;
  consent_at: string | null;
  members: TelegramLinkedMember[];
  problem: string | null;
};
export type TelegramLink = {
  available: boolean;
  bot_username: string | null;
  linked: boolean;
  telegram_username: string | null;
  linked_at: string | null;
};
export type TelegramLinkCode = { code: string; url: string; expires_at: string };

export const telegramApi = {
  settings: () => request<TelegramSettings>("/api/settings/telegram"),
  saveSettings: (body: { token?: string; enabled: boolean; consent: boolean }) =>
    request<TelegramSettings>("/api/settings/telegram", { method: "PUT", ...json(body) }),
  removeBot: () => request<TelegramSettings>("/api/settings/telegram", { method: "DELETE" }),
  unlinkMember: (userId: string) =>
    request<TelegramSettings>(`/api/settings/telegram/members/${encodeURIComponent(userId)}`, { method: "DELETE" }),
  link: () => request<TelegramLink>("/api/telegram/link"),
  createCode: () => request<TelegramLinkCode>("/api/telegram/link/code", { method: "POST" }),
  unlink: () => request<TelegramLink>("/api/telegram/link", { method: "DELETE" }),
};

// ---- Roles: named permission sets the office manages (Ayarlar → Roller) ----------------------
export type PermissionInfo = { key: string; label: string; hint: string | null };
export type PermissionGroup = { name: string; permissions: PermissionInfo[] };
export type Role = {
  key: string;
  name: string;
  description: string | null;
  permissions: string[];
  is_system: boolean;
  editable: boolean;
  member_count: number;
};
export type RolesResponse = { catalog: PermissionGroup[]; roles: Role[] };
export type SaveRole = { name: string; description?: string | null; permissions: string[] };

export type AuditEvent = {
  id: string;
  at: string;
  actor_id: string | null;
  actor_name: string | null;
  action: string;
  entity_type: string;
  entity_id: string | null;
  details: string | null;
};
export type AuditPage = { items: AuditEvent[]; total: number };

export const auditApi = {
  list: (limit = 50, offset = 0, action?: string) =>
    request<AuditPage>(
      `/api/audit?limit=${limit}&offset=${offset}${action ? `&action=${encodeURIComponent(action)}` : ""}`,
    ),
};

export const rolesApi = {
  list: () => request<RolesResponse>("/api/roles"),
  create: (body: SaveRole) => request<Role>("/api/roles", { method: "POST", ...json(body) }),
  update: (key: string, body: SaveRole) =>
    request<Role>(`/api/roles/${encodeURIComponent(key)}`, { method: "PUT", ...json(body) }),
  remove: (key: string) => request<void>(`/api/roles/${encodeURIComponent(key)}`, { method: "DELETE" }),
  setMemberRole: (memberId: string | number, role: string) =>
    request<void>(`/api/members/${memberId}`, { method: "PATCH", ...json({ role }) }),
};

// ---- A matter's team: who works on it (and so sees it) ---------------------------------------
export type MatterTeamMember = { user_id: string; name: string; is_lead: boolean; added_at: string };
export type MatterTeam = { members: MatterTeamMember[]; can_manage: boolean };
export type MatterTeamCandidate = { user_id: string; name: string };

export type Note = {
  id: string;
  body: string;
  channel: string;
  matter_id: string | null;
  client_id: string | null;
  occurred_at: string;
  created_at: string;
};

export const notesApi = {
  list: (matterId: string) => request<Note[]>(`/api/notes?matterId=${encodeURIComponent(matterId)}`),
  create: (matterId: string, body: string) =>
    request<Note>("/api/notes", { method: "POST", ...json({ body, matter_id: matterId }) }),
};

export const teamApi = {
  get: (matterId: string) => request<MatterTeam>(`/api/matters/${matterId}/team`),
  candidates: (matterId: string) => request<MatterTeamCandidate[]>(`/api/matters/${matterId}/team/candidates`),
  add: (matterId: string, userId: string, isLead = false) =>
    request<MatterTeam>(`/api/matters/${matterId}/team`, { method: "POST", ...json({ user_id: userId, is_lead: isLead }) }),
  setLead: (matterId: string, userId: string, isLead: boolean) =>
    request<MatterTeam>(`/api/matters/${matterId}/team/${userId}`, { method: "PATCH", ...json({ is_lead: isLead }) }),
  remove: (matterId: string, userId: string) =>
    request<MatterTeam>(`/api/matters/${matterId}/team/${userId}`, { method: "DELETE" }),
};
