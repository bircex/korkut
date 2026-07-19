import { describe, it, expect, beforeEach, vi, afterEach } from "vitest";
import { sessionRequest, refreshSession, onSessionExpired } from "./session";

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
