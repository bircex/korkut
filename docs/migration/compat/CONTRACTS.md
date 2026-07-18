# Compatibility contract (KDM-003)

Versioned semantics the .NET port must reproduce for API/config/data parity with
the Python runtime. Machine-checked by `backend/tests/compat/` (OpenAPI snapshot
guard + differential fixtures). Values below are observed from the live Python
app; a change to any of them must go through a reviewed snapshot/expected update.

**Contract version: `v1` (Python baseline, 2026-07-18).**

## 1. OpenAPI surface

- OpenAPI **3.1.0**, **58 paths**, **30 component schemas** (see
  `tests/compat/contracts/openapi.json`).
- Guard: `test_openapi_matches_snapshot` fails on any drift; intentional changes
  require `KORKUT_CONTRACT_UPDATE=1 python -m tests.compat.openapi_snapshot` and
  review of the diff. This is the "contract change must not pass CI without
  explicit approval" gate.

## 2. Error / Problem responses

Two shapes, both `application/json`:

| Case | Status | Body |
|---|---|---|
| App error (`HTTPException`) | 401/403/404/400/503/429 | `{ "detail": "<localized string>" }` |
| Request validation | 422 | `{ "detail": [ { "type", "loc": [..], "msg", "input" } ] }` |

- Localized messages are part of the contract (e.g. 401 `"Oturum gerekli"`, 404
  `"Dosya bulunamadı"`). The .NET port must emit the **same status + same shape**;
  message-string parity is asserted via the differential fixtures.
- **Cross-tenant access returns 404, never 403** (non-disclosure). Fixture
  `matter_idor_cross_office` locks this in.
- The 422 shape is FastAPI/Pydantic-specific. If the .NET port cannot reproduce
  it byte-for-byte, that is the canonical case for a **legacy-diff allowlist**
  entry (owner + expiry + tracking issue), resolved in KDM-006/KDM-034 — not a
  silent divergence.

## 3. Pagination

- List endpoints return `{ "items": [ ... ], "total": <int> }` (e.g.
  `/api/sessions`, `/api/matters`). `total` is the count for the current scope.
  Preserve the envelope keys and types.

## 4. Date / time

- Timestamps are ISO-8601 strings (`YYYY-MM-DDThh:mm:ss...`). The differential
  normalizer masks them to `<iso8601>` so *format*, not value, is compared. The
  .NET port must serialize the same ISO-8601 format (not Unix epochs).

## 5. Enums

- `users.role`: `owner` | `member`.
- `matters.status`: `open` (default) | ... (full set enumerated from the ORM in
  KDM-023); parity asserted as values flow through fixtures.
- `calendar_events.category`: `hearing` | `deadline` | `appointment` | `reminder`.
- Enum **string values** are contractual — preserve casing exactly.

## 6. Auth cookie / header contract

- Session cookie name **`korkut_session`**, `HttpOnly`, `SameSite=Strict`,
  `Secure` gated on `KORKUT_SECURE_COOKIES`. Signed JWT (HS256) — claims
  `sub/oid/role/ep/iat/exp`, 12h TTL (see `ENV_CONTRACT.md` / `PYTHON_BASELINE.md`).
- The differential harness authenticates by minting the same session cookie for
  both runtimes (`adapters.PythonAdapter.cookie_for`; the .NET adapter must mint
  an equivalent cookie at KDM-034).
- **Refresh tokens & TOTP are new in .NET** (KDM-017/KDM-018) — they have **no
  Python baseline**. Their fixtures are added when those issues land; this
  contract records only what exists today.

## 7. Google callback contract

- `GET /api/auth/google/start` → 307 redirect to Google consent (503 if
  unconfigured). `GET /api/auth/google/callback` → verifies `state` + RS256 ID
  token, then 303 → `/` (session set) or `/giris?error=...`. The .NET port
  (KDM-016) must preserve these status codes and redirect targets.

## 8. Env / model catalog & DB snapshot comparison

- Env-name contract: `docs/migration/baseline/ENV_CONTRACT.md` (KDM-002).
- Model catalog is DB-driven (`RegisteredModel`/`OfficeModel`) +
  `config/model-routing.yaml`; `KORKUT_MODELS_FILE` is unused. Catalog/DB-shape
  comparison tooling is exercised by the differential fixtures over the model and
  matter endpoints; a fuller DB-snapshot diff runs during the KDM-034 data
  migration rehearsal against real PostgreSQL.

## Change control

Any change to §1–§8 must update the corresponding snapshot/expected file **in the
same PR** and be called out for review. Unreviewed contract drift fails
`tests/compat/`.
