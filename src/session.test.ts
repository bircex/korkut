import { describe, it, expect, beforeEach, vi, afterEach } from "vitest";
import { sessionRequest, sessionFetch, refreshSession, onSessionExpired } from "./session";

// Session refresh/retry semantics (KDM-030 acceptance 2): a 401 triggers exactly one
// single-flight refresh + one retry, then hard expiry — never an infinite loop.

type FetchMock = ReturnType<typeof vi.fn>;

function jsonResponse(status: number, body: unknown = {}): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

describe("sessionRequest", () => {
  let fetchMock: FetchMock;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("returns data without refreshing when the first call succeeds", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { ok: true }));
    const data = await sessionRequest<{ ok: boolean }>("/api/x");
    expect(data).toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("on 401 refreshes once and retries once, then succeeds", async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401)) // original
      .mockResolvedValueOnce(jsonResponse(200)) // refresh
      .mockResolvedValueOnce(jsonResponse(200, { ok: true })); // retry
    const data = await sessionRequest<{ ok: boolean }>("/api/x");
    expect(data).toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[1][0]).toContain("/auth"); // the refresh call
  });

  it("does NOT loop: a second 401 after refresh ends the session", async () => {
    const expired = vi.fn();
    const off = onSessionExpired(expired);
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401)) // original
      .mockResolvedValueOnce(jsonResponse(200)) // refresh ok
      .mockResolvedValueOnce(jsonResponse(401)); // retry still 401
    await expect(sessionRequest("/api/x")).rejects.toMatchObject({ status: 401 });
    expect(fetchMock).toHaveBeenCalledTimes(3); // original + refresh + ONE retry, no more
    expect(expired).toHaveBeenCalledOnce();
    off();
  });

  it("emits expiry and does not retry when the refresh itself fails", async () => {
    const expired = vi.fn();
    const off = onSessionExpired(expired);
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401)) // original
      .mockResolvedValueOnce(jsonResponse(401)); // refresh fails
    await expect(sessionRequest("/api/x")).rejects.toMatchObject({ status: 401 });
    expect(fetchMock).toHaveBeenCalledTimes(2); // no retry after a failed refresh
    expect(expired).toHaveBeenCalledOnce();
    off();
  });

  it("does not refresh when auth is disabled (probe semantics)", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(401));
    await expect(sessionRequest("/api/me", {}, { auth: false })).rejects.toMatchObject({ status: 401 });
    expect(fetchMock).toHaveBeenCalledTimes(1); // no refresh attempt
  });
});

// The raw variant used by the stream/upload/export endpoints (their Response must
// reach the caller intact) shares the exact refresh/retry contract: a chat message
// typed after the 15-minute access expiry must refresh + retry, not sign the user out.
describe("sessionFetch", () => {
  let fetchMock: FetchMock;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("returns the raw Response untouched when the first call succeeds", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { ok: true }));
    const res = await sessionFetch("/api/sessions/1/chat/stream", { method: "POST" });
    expect(res.status).toBe(200);
    expect(await res.json()).toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("on 401 refreshes once and retries once, then hands back the good Response", async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401)) // original
      .mockResolvedValueOnce(jsonResponse(200)) // refresh
      .mockResolvedValueOnce(jsonResponse(200, { ok: true })); // retry
    const res = await sessionFetch("/api/sessions/1/chat/stream", { method: "POST" });
    expect(res.status).toBe(200);
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[1][0]).toContain("/auth"); // the refresh call
  });

  it("emits expiry and returns the 401 when the refresh fails (no retry)", async () => {
    const expired = vi.fn();
    const off = onSessionExpired(expired);
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401)) // original
      .mockResolvedValueOnce(jsonResponse(401)); // refresh fails
    const res = await sessionFetch("/api/sessions/1/chat/stream", { method: "POST" });
    expect(res.status).toBe(401);
    expect(fetchMock).toHaveBeenCalledTimes(2); // no retry after a failed refresh
    expect(expired).toHaveBeenCalledOnce();
    off();
  });

  it("does NOT loop: a second 401 after refresh emits expiry and stops", async () => {
    const expired = vi.fn();
    const off = onSessionExpired(expired);
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401)) // original
      .mockResolvedValueOnce(jsonResponse(200)) // refresh ok
      .mockResolvedValueOnce(jsonResponse(401)); // retry still 401
    const res = await sessionFetch("/api/sessions/1/chat/stream", { method: "POST" });
    expect(res.status).toBe(401);
    expect(fetchMock).toHaveBeenCalledTimes(3); // original + refresh + ONE retry, no more
    expect(expired).toHaveBeenCalledOnce();
    off();
  });
});

describe("refreshSession (single-flight)", () => {
  it("coalesces concurrent refreshes into one network call", async () => {
    let resolve!: (r: Response) => void;
    const fetchMock = vi.fn().mockReturnValue(new Promise<Response>((r) => (resolve = r)));
    vi.stubGlobal("fetch", fetchMock);

    const a = refreshSession();
    const b = refreshSession();
    resolve(jsonResponse(200));
    const [ra, rb] = await Promise.all([a, b]);

    expect(ra).toBe(true);
    expect(rb).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(1); // shared round-trip
    vi.unstubAllGlobals();
  });
});
