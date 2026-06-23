export type LoginStep = "mfa" | "enroll";
export type User = { username: string; full_name: string | null };
export type ChatMessage = { role: "user" | "assistant"; content: string };
export type SessionBrief = {
  id: number;
  title: string;
  created_by_name: string | null;
  updated_at: string | null;
};
export type SessionDetail = SessionBrief & { messages: ChatMessage[] };
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
    request<{ step: LoginStep }>("/api/login", {
      method: "POST",
      body: JSON.stringify({ username, password }),
    }),
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
  sessions: () => request<SessionBrief[]>("/api/sessions"),
  createSession: () => request<SessionBrief>("/api/sessions", { method: "POST" }),
  getSession: (id: number) => request<SessionDetail>(`/api/sessions/${id}`),
  deleteSession: (id: number) =>
    request<{ ok: boolean }>(`/api/sessions/${id}`, { method: "DELETE" }),
  sessionChat: (id: number, text: string) =>
    request<{ reply: string; title: string; open_document: number | null }>(
      `/api/sessions/${id}/chat`,
      { method: "POST", body: JSON.stringify({ text }) },
    ),
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
  getDocument: (sessionId: number) =>
    request<DocumentData>(`/api/sessions/${sessionId}/document`),
  draftDocument: (docId: number, instruction: string) =>
    request<PendingEdit>(`/api/documents/${docId}/draft`, {
      method: "POST",
      body: JSON.stringify({ instruction }),
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
