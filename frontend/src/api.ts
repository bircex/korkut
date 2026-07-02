export type LoginStep = "mfa" | "enroll";
export type User = {
  username: string;
  full_name: string | null;
  bar_registry_no: string | null;
  role: "owner" | "member";
  office: string | null;
  has_2fa?: boolean;
  require_2fa?: boolean;
};
// Login/register either hand back a full session ("done") or ask for a 2FA step.
export type LoginResult = { step: LoginStep } | { step: "done"; user: User };
export type Member = {
  id: number;
  username: string;
  full_name: string | null;
  bar_registry_no: string | null;
  role: "owner" | "member";
  is_active: boolean;
  enrolled: boolean;
  mfa_required: boolean;
  is_self: boolean;
};
export type ChatMessage = { role: "user" | "assistant"; content: string };
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
export type TemplateKind = "dilekce" | "sozlesme";
export type TemplateScope = "user" | "office";
export type ExportFormat = "udf" | "docx";
export type TemplateTypeRow = {
  id: string;
  ad: string;
  ref: string | null;
  hasOfficeOverride: boolean;
  hasUserOverride: boolean;
};
export type TemplateCategory = { kategori: string; tipler: TemplateTypeRow[] };
export type TemplateList = { kind: TemplateKind; kategoriler: TemplateCategory[] };
export type TemplateDetail = {
  id: string;
  ad: string;
  kind: string;
  default: string | null;
  officeOverride: string | null;
  userOverride: string | null;
  effective: string | null;
};
export type EmailChannel = {
  kind: string;
  enabled: boolean;
  from_address: string;
  from_name: string;
  hasPassword: boolean;
  can_edit: boolean;
};
export type EmailChannelInput = {
  enabled: boolean;
  from_address: string;
  from_name: string;
  app_password: string | null; // null → keep stored password; "" → clear
};
export type IntegrationKind = "telegram" | "whatsapp" | "email";
export type IntegrationChannel = {
  kind: IntegrationKind;
  enabled: boolean;
  configured: boolean;
  base_url: string;
  linked: boolean;
  linked_id: string;
  can_edit: boolean;
};
export type TelegramChannelInput = {
  enabled: boolean;
  bot_token: string | null; // null → keep stored token; "" → clear
};
export type WhatsAppChannelInput = {
  enabled: boolean;
  base_url: string;
  token: string | null; // null → keep stored secret; "" → clear
};
export type OutboxStatus = "draft" | "queued" | "sent" | "failed" | "cancelled";
export type OutboxMessage = {
  id: number;
  kind: string;
  to: string;
  subject: string;
  body: string;
  send_at: string | null;
  status: OutboxStatus;
  error: string | null;
  created_at: string | null;
  sent_at: string | null;
};
export type OutboxEdit = {
  to_addr: string;
  subject: string;
  body: string;
  send_at: string | null;
};
export type ModelData = {
  enabled: boolean;
  is_local: boolean;
  model: string;
  base_url: string;
  hasApiKey: boolean;
  can_edit: boolean;
};
export type ModelInput = {
  enabled: boolean;
  is_local: boolean;
  model: string;
  base_url: string;
  api_key: string | null; // null → keep stored key; "" → clear it
};
export type ModelHealth = { connected: boolean; configured: boolean; detail: string };

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
  id: number;
  full_name: string;
  phone: string | null;
  email: string | null;
  national_id: string | null;
  address: string | null;
  date_of_birth: string | null;
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
  title: string;
  description: string | null;
  assignee_id: number | null;
  assignee_name: string | null;
  matter_id: number | null;
  status: string;
  due_date: string | null;
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
  id: number;
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
export type CalendarEventInput = {
  category: string;
  title: string;
  start_at: string;
  end_at?: string | null;
  all_day?: boolean;
  type?: string | null;
  matter_id?: number | null;
  client_id?: number | null;
  assignee_id?: number | null;
  location?: string | null;
  notes?: string | null;
  rrule?: string | null;
  remind_before_min?: number | null;
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
  client_name?: string | null;
  file_no?: string | null;
  court?: string | null;
  case_type?: string | null;
  counterparty?: string | null;
};
export type MatterPatch = Partial<Omit<MatterInput, "client_name">> & { status?: string; notes?: string };
export type HearingInput = {
  date: string;
  time?: string | null;
  location?: string | null;
  type?: string | null;
  notes?: string | null;
};
export type Provision = { secret: string; otpauth_uri: string; qr: string };
export type EnrollResult = User & { recovery_codes: string[] };
export type Coverage = {
  total_decisions: number;
  by_court: { label: string; total: number }[];
  decision_sources: string[];
  legislation: string;
};

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

async function request<T>(path: string, opts: RequestInit = {}): Promise<T> {
  const res = await fetch(path, {
    credentials: "same-origin",
    headers: { "Content-Type": "application/json" },
    ...opts,
  });
  if (!res.ok) {
    let detail = "İstek başarısız";
    try {
      const body = await res.json();
      if (body?.detail) detail = body.detail;
    } catch {
      /* keep default */
    }
    throw new ApiError(res.status, detail);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export const api = {
  me: () => request<User>("/api/me"),
  coverage: () => request<Coverage>("/api/coverage"),
  login: (username: string, password: string) =>
    request<LoginResult>("/api/login", {
      method: "POST",
      body: JSON.stringify({ username, password }),
    }),
  register: (office_name: string, full_name: string, username: string, password: string) =>
    request<LoginResult>("/api/register", {
      method: "POST",
      body: JSON.stringify({ office_name, full_name, username, password }),
    }),
  members: () => request<Member[]>("/api/members"),
  addMember: (username: string, full_name: string, password: string, bar_registry_no?: string) =>
    request<Member>("/api/members", {
      method: "POST",
      body: JSON.stringify({ username, full_name, password, bar_registry_no }),
    }),
  removeMember: (id: number) =>
    request<{ ok: boolean }>(`/api/members/${id}`, { method: "DELETE" }),
  setMember2fa: (id: number, required: boolean) =>
    request<Member>(`/api/members/${id}/2fa`, {
      method: "POST",
      body: JSON.stringify({ required }),
    }),
  updateMe: (patch: { full_name?: string; bar_registry_no?: string | null }) =>
    request<User>("/api/me", { method: "PATCH", body: JSON.stringify(patch) }),
  getStyle: () => request<StyleData>("/api/style"),
  saveUserStyle: (fields: StyleFields) =>
    request<StyleFields>("/api/style/user", { method: "PUT", body: JSON.stringify(fields) }),
  saveOfficeStyle: (fields: StyleFields) =>
    request<StyleFields>("/api/style/office", { method: "PUT", body: JSON.stringify(fields) }),
  listTemplates: (kind: TemplateKind) => request<TemplateList>(`/api/templates?kind=${kind}`),
  getTemplate: (typeId: string) =>
    request<TemplateDetail>(`/api/templates/${encodeURIComponent(typeId)}`),
  saveTemplate: (typeId: string, scope: TemplateScope, content: string) =>
    request<{ ok: boolean }>(`/api/templates/${encodeURIComponent(typeId)}`, {
      method: "PUT",
      body: JSON.stringify({ scope, content }),
    }),
  deleteTemplate: (typeId: string, scope: TemplateScope) =>
    request<{ ok: boolean }>(`/api/templates/${encodeURIComponent(typeId)}?scope=${scope}`, {
      method: "DELETE",
    }),
  getModel: () => request<ModelData>("/api/model"),
  saveModel: (input: ModelInput) =>
    request<ModelData>("/api/model", { method: "PUT", body: JSON.stringify(input) }),
  deleteModel: () => request<{ ok: boolean }>("/api/model", { method: "DELETE" }),
  modelHealth: () => request<ModelHealth>("/api/model/health"),
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
  getMatter: (id: number) => request<MatterDetail>(`/api/matters/${id}`),
  createMatter: (input: MatterInput) =>
    request<MatterDetail>("/api/matters", { method: "POST", body: JSON.stringify(input) }),
  updateMatter: (id: number, patch: MatterPatch) =>
    request<MatterDetail>(`/api/matters/${id}`, { method: "PATCH", body: JSON.stringify(patch) }),
  deleteMatter: (id: number) =>
    request<{ ok: boolean }>(`/api/matters/${id}`, { method: "DELETE" }),
  addHearing: (id: number, input: HearingInput) =>
    request<Hearing>(`/api/matters/${id}/hearings`, { method: "POST", body: JSON.stringify(input) }),
  agenda: (days = 14) =>
    request<{ items: AgendaItem[]; days: number }>(`/api/calendar/upcoming?days=${days}`),
  addDeadline: (id: number, input: DeadlineInput) =>
    request<Deadline>(`/api/matters/${id}/deadlines`, { method: "POST", body: JSON.stringify(input) }),
  completeDeadline: (did: number) =>
    request<Deadline>(`/api/deadlines/${did}/complete`, { method: "POST" }),
  clients: () => request<{ items: ClientBrief[] }>("/api/clients"),
  createClient: (input: ClientPatch & { full_name: string }) =>
    request<ClientBrief>("/api/clients", { method: "POST", body: JSON.stringify(input) }),
  updateClient: (id: number, patch: ClientPatch) =>
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
  tasks: (status?: string) =>
    request<{ items: Task[] }>(`/api/tasks${status ? `?status=${status}` : ""}`),
  createTask: (input: {
    title: string;
    assignee_id?: number | null;
    due_date?: string | null;
  }) => request<Task>("/api/tasks", { method: "POST", body: JSON.stringify(input) }),
  updateTask: (id: number, patch: { status?: string; assignee_id?: number | null }) =>
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
    if (params?.matter_id != null) q.set("matter_id", String(params.matter_id));
    const qs = q.toString();
    return request<{ items: CalendarEvent[] }>(`/api/calendar/events${qs ? `?${qs}` : ""}`);
  },
  createCalendarEvent: (input: CalendarEventInput) =>
    request<CalendarEvent>("/api/calendar/events", {
      method: "POST",
      body: JSON.stringify(input),
    }),
  updateCalendarEvent: (id: number, patch: Partial<CalendarEventInput> & { status?: string }) =>
    request<CalendarEvent>(`/api/calendar/events/${id}`, {
      method: "PATCH",
      body: JSON.stringify(patch),
    }),
  deleteCalendarEvent: (id: number) =>
    request<{ ok: boolean }>(`/api/calendar/events/${id}`, { method: "DELETE" }),
  exportMarkdown: async (format: ExportFormat, title: string, markdown: string): Promise<Blob> => {
    const res = await fetch("/api/export", {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ format, title, markdown }),
    });
    if (!res.ok) throw new ApiError(res.status, "Dışa aktarma başarısız");
    return res.blob();
  },
  provision: () => request<Provision>("/api/2fa/provision"),
  enroll: (code: string) =>
    request<EnrollResult>("/api/2fa/enroll", {
      method: "POST",
      body: JSON.stringify({ code }),
    }),
  verify: (code: string) =>
    request<User & { used_recovery: boolean }>("/api/2fa/verify", {
      method: "POST",
      body: JSON.stringify({ code }),
    }),
  logout: () => request<{ ok: boolean }>("/api/logout", { method: "POST" }),
  sessions: (limit = 20, offset = 0) =>
    request<SessionPage>(`/api/sessions?limit=${limit}&offset=${offset}`),
  createSession: (matterId?: number) =>
    request<SessionBrief>(
      `/api/sessions${matterId ? `?matter_id=${matterId}` : ""}`,
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
  getEmailChannel: () => request<EmailChannel>("/api/channels/email"),
  saveEmailChannel: (input: EmailChannelInput) =>
    request<EmailChannel>("/api/channels/email", { method: "PUT", body: JSON.stringify(input) }),
  listIntegrations: () => request<{ channels: IntegrationChannel[] }>("/api/integrations"),
  saveTelegramChannel: (input: TelegramChannelInput) =>
    request<{ ok: boolean; configured: boolean }>("/api/channels/telegram", {
      method: "PUT",
      body: JSON.stringify(input),
    }),
  saveWhatsAppChannel: (input: WhatsAppChannelInput) =>
    request<{ ok: boolean; configured: boolean }>("/api/channels/whatsapp", {
      method: "PUT",
      body: JSON.stringify(input),
    }),
  createIntegrationLink: (kind: IntegrationKind) =>
    request<{ code: string; kind: string }>(`/api/integrations/${kind}/link`, { method: "POST" }),
  deleteIntegrationLink: (kind: IntegrationKind) =>
    request<{ ok: boolean }>(`/api/integrations/${kind}/link`, { method: "DELETE" }),
  getOutbox: (id: number) => request<OutboxMessage>(`/api/outbox/${id}`),
  listOutbox: () => request<OutboxMessage[]>("/api/outbox"),
  editOutbox: (id: number, fields: OutboxEdit) =>
    request<OutboxMessage>(`/api/outbox/${id}`, { method: "PUT", body: JSON.stringify(fields) }),
  confirmOutbox: (id: number) =>
    request<OutboxMessage>(`/api/outbox/${id}/confirm`, { method: "POST" }),
  cancelOutbox: (id: number) =>
    request<OutboxMessage>(`/api/outbox/${id}/cancel`, { method: "POST" }),
  sessionChat: (id: number, text: string) =>
    request<{
      reply: string;
      title: string;
      open_document: number | null;
      pending_email: number | null;
    }>(`/api/sessions/${id}/chat`, { method: "POST", body: JSON.stringify({ text }) }),
  // Streaming twin of sessionChat: activity steps + answer tokens arrive live over SSE. `onStep`
  // and `onToken` fire as events land; the promise resolves with the same payload as sessionChat
  // (or rejects with an ApiError carrying the friendly detail from an `error` event).
  sessionChatStream: async (
    id: number,
    text: string,
    on: { onStep?: (label: string) => void; onToken?: (t: string) => void },
  ) => {
    const res = await fetch(`/api/sessions/${id}/chat/stream`, {
      method: "POST",
      credentials: "same-origin",
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
    const res = await fetch(`/api/sessions/${id}/upload`, {
      method: "POST",
      credentials: "same-origin",
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
    return (await res.json()) as { reply: string; title: string; user_display: string };
  },
  documents: () => request<DocBrief[]>("/api/documents"),
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
