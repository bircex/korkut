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
    request<{ reply: string; title: string }>(`/api/sessions/${id}/chat`, {
      method: "POST",
      body: JSON.stringify({ text }),
    }),
};
