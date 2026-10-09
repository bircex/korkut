import { ApiError } from "./api";

// Session-aware request layer for the .NET auth migration (KDM-030).
//
// Security contract:
//   * Sessions ride in httpOnly cookies — NO access/refresh token ever touches
//     localStorage or JS-readable storage.
//   * On a 401 from a protected call we attempt exactly ONE refresh, shared across
//     concurrent callers (single-flight), then retry the original request ONCE. A
//     second 401 (or a failed refresh) ends the session — we never loop.
//   * The Python endpoints stay reachable behind API_MODE === "python"; flip the
//     flag (VITE_API_MODE=dotnet) to cut over, and delete the "python" branch once
//     the migration completes. The fallback is isolated to this module.

export type ApiMode = "python" | "dotnet";

declare global {
  interface Window {
    /** Runtime override for the API contract (e2e/staging); build env wins in prod. */
    __KORKUT_API_MODE__?: ApiMode;
  }
}

function resolveMode(): ApiMode {
  const runtime = typeof window !== "undefined" ? window.__KORKUT_API_MODE__ : undefined;
  if (runtime === "dotnet" || runtime === "python") return runtime;
  return (import.meta.env.VITE_API_MODE as ApiMode) === "dotnet" ? "dotnet" : "python";
}

export const API_MODE: ApiMode = resolveMode();

// Endpoint contracts per mode. Only auth/session differs in shape; office-core paths
// are identical, so the domain code is untouched by the cutover.
export const routes = {
  refresh: API_MODE === "dotnet" ? "/api/auth/refresh" : "/api/auth/session/refresh",
  login: "/api/auth/login",
  totp: "/api/auth/2fa",
  logout: "/api/auth/logout",
  passwordResetRequest: "/api/auth/password/reset-request",
  passwordReset: "/api/auth/password/reset",
} as const;

type Listener = () => void;
const expiredListeners = new Set<Listener>();
const authChangedListeners = new Set<Listener>();

/** Subscribe to hard session expiry (refresh exhausted). Returns an unsubscribe fn. */
export function onSessionExpired(cb: Listener): () => void {
  expiredListeners.add(cb);
  return () => expiredListeners.delete(cb);
}

function emitExpired() {
  expiredListeners.forEach((l) => l());
}

/** Subscribe to a fresh sign-in (local login / TOTP) so the app re-probes the session. */
export function onAuthChanged(cb: Listener): () => void {
  authChangedListeners.add(cb);
  return () => authChangedListeners.delete(cb);
}

/** Signal that the session cookie changed (post local-login/TOTP) — App re-probes me(). */
export function signalAuthChanged() {
  authChangedListeners.forEach((l) => l());
}

let refreshInFlight: Promise<boolean> | null = null;

/** Single-flight refresh: concurrent callers share one network round-trip. */
export function refreshSession(): Promise<boolean> {
  if (!refreshInFlight) {
    refreshInFlight = fetch(routes.refresh, { method: "POST", credentials: "same-origin" })
      .then((r) => r.ok)
      .catch(() => false)
      .finally(() => {
        refreshInFlight = null;
      });
  }
  return refreshInFlight;
}

async function parseError(res: Response): Promise<ApiError> {
  let detail = "İstek başarısız";
  try {
    const body = await res.json();
    if (body?.detail) detail = body.detail;
  } catch {
    /* keep default */
  }
  return new ApiError(res.status, detail);
}

async function raw(path: string, opts: RequestInit): Promise<Response> {
  return fetch(path, {
    credentials: "same-origin",
    headers: { "Content-Type": "application/json" },
    ...opts,
  });
}

/**
 * Raw variant of sessionRequest for endpoints whose Response must reach the
 * caller intact (SSE stream, multipart upload, blob export): same 401 →
 * single-flight refresh → retry-once contract, but no JSON parsing and no
 * forced Content-Type (multipart needs the browser's own boundary header).
 * On exhausted refresh the 401 Response is returned — the caller's normal
 * !ok path reports it, while emitExpired() already drops the app to sign-in.
 */
export async function sessionFetch(path: string, opts: RequestInit = {}): Promise<Response> {
  let res = await fetch(path, { credentials: "same-origin", ...opts });
  if (res.status === 401) {
    const refreshed = await refreshSession();
    if (!refreshed) {
      emitExpired();
      return res;
    }
    res = await fetch(path, { credentials: "same-origin", ...opts });
    if (res.status === 401) emitExpired();
  }
  return res;
}

/**
 * Perform a request with automatic refresh-and-retry-once on 401.
 * `auth: false` opts out (used by the refresh/login calls themselves) so we can
 * never recurse into a refresh loop.
 */
export async function sessionRequest<T>(path: string, opts: RequestInit = {}, { auth = true }: { auth?: boolean } = {}): Promise<T> {
  let res = await raw(path, opts);

  if (res.status === 401 && auth) {
    const refreshed = await refreshSession();
    if (!refreshed) {
      emitExpired();
      throw await parseError(res);
    }
    // Retry exactly once.
    res = await raw(path, opts);
    if (res.status === 401) {
      emitExpired();
      throw await parseError(res);
    }
  }

  if (!res.ok) {
    throw await parseError(res);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}
