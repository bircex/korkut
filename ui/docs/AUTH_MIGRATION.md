# Auth & Office-Core API Migration (KDM-030)

Migrates the user UI onto the .NET auth + office-core contracts without losing
behaviour or accessibility.

## Session model — cookie-only, no stored tokens

- Sessions ride in **httpOnly cookies**. No access/refresh token is ever read from
  or written to `localStorage`/`sessionStorage` (an e2e assertion enforces this).
- All protected calls go through `sessionRequest` (`src/session.ts`):
  - a `401` triggers **one** single-flight refresh (`refreshSession`) shared across
    concurrent callers, then **one** retry of the original request;
  - a second `401` — or a failed refresh — ends the session via `onSessionExpired`
    and never retries again, so there is **no infinite refresh/retry loop**;
  - the bootstrap `me()` probe opts out of refresh (`auth: false`) so a genuinely
    anonymous visitor resolves cleanly to the sign-in screen.

## Feature flag — `VITE_API_MODE`

`python` (default) keeps the legacy Google-only flow; `dotnet` enables local
email/password sign-in + policy-driven TOTP + `/api/auth/refresh`. The Python branch
is isolated to `src/session.ts` (`routes`, `API_MODE`) and is **ready to delete** at
cutover. A runtime override (`window.__KORKUT_API_MODE__`) exists for e2e/staging;
the build-time env wins in production.

## Screens

- `LocalAuth` — email/password → TOTP (or recovery code) challenge, built on the
  KDM-029 accessible primitives.
- `AsyncState` — the five standard states every office-core screen must handle:
  loading, error, empty, **permission-denied (403)** and **concurrent-update (409)**.

## Tests

- **Unit** (`src/session.test.ts`): refresh-once, retry-once, no-loop, single-flight
  coalescing, probe opt-out.
- **E2E** (`e2e/auth.spec.ts`): local login → TOTP → session re-probe, recovery-code
  toggle, wrong-password error — all axe-clean and keyboard-operable, asserting no
  token lands in storage.
