# Python backend behavioral baseline (KDM-002)

Frozen inventory of the current Python backend's active code paths, each
**classified with an owner and a migration decision** (the KDM issue that ports
it). This is the parity source of truth for the .NET port. **No behavior is
changed by KDM-002.** Env details live in [`ENV_CONTRACT.md`](ENV_CONTRACT.md);
unused paths in [`DEADCODE_CANDIDATES.md`](DEADCODE_CANDIDATES.md).

- **Runtime:** Python 3.12+, FastAPI + SQLAlchemy 2.0. Entry
  `korkut.api.app:app` (`python -m korkut.api`).
- **Owner (all paths):** `@recepkizilarslan` (per `MIGRATION_GOVERNANCE.md` §3).
- **Migration decision legend:** `PORT@KDM-xxx` = re-implement in .NET under that
  issue; `DROP` = do not port (dead/absent); `DEFER` = out of migration scope.

## 1. HTTP API surface

Single FastAPI app in `api/app.py` (2704 lines; no `APIRouter` sub-routers).
Auth via `HttpOnly` cookie `korkut_session` read as a `Cookie(...)` param per
handler. Levels: **public**, **authenticated** (`_require_user`, `app.py:446`),
**owner** (`_require_owner`, `app.py:453`). Error contract: 401 `"Oturum
gerekli"`, 403 owner-only, **404 on cross-tenant miss (never 403)**, 400/422
validation, 503 unconfigured provider/Google, 429 rate-limit.

| Area | Endpoints (see `app.py`) | Auth | Migration decision |
|---|---|---|---|
| Google OAuth / bootstrap | `GET /api/auth/google/start` (622), `/callback` (648), `GET /api/signup/pending` (729), `POST /api/demo-request` (740), `GET /api/config` (812) | public | `PORT@KDM-016` (OIDC), `KDM-006` (config) |
| Session lifecycle | `POST /api/logout` (1102), `GET/PATCH /api/me` (1114/1121) | auth | `PORT@KDM-015/018` |
| Members / invites | `GET/POST /api/members` (840/849), `DELETE /api/members/{uid}` (885), `POST/GET/DELETE /api/invites` (911/935/944) | owner writes | `PORT@KDM-019` |
| Style / persona / memory | `/api/style*` (972-994), `/api/persona*` (1014-1023), `/api/memory*` (1053-1090) | auth/owner | `PORT@KDM-023` (memory endpoints migrate CRUD only; brain/RAG deferred) |
| Chat sessions | `/api/sessions*` (1161-1252), `POST /api/sessions/{cid}/chat` (1945) + `/chat/stream` SSE (1953), `/upload` (2046) | auth | `PORT@KDM-024` (CRUD) / `KDM-027` (upload/extract); chat-brain internals `DEFER` |
| Matter domain | matters (1268-1315), hearings/deadlines (1325-1375), calendar (1345-1463), clients (1471-1499) | auth | `PORT@KDM-023/024` |
| Finance | invoices (1529-1584), expenses (1595-1624), summary (1635), reports (1647) | auth | `PORT@KDM-024` |
| Tasks / evrak | `/api/tasks*` (1657-1719), `/api/evrak*` (1735-1769) | auth | `PORT@KDM-024` |
| Documents / Canvas | petition/contract types (2195-2202), templates (2214-2299), documents + draft/accept/revert/save (2332-2614), export (2637-2656) | auth | `PORT@KDM-025/028` (docs), `KDM-033` (templates/export) |
| Static SPA | `/assets/*` mount (2683), catch-all `GET /{full_path}` (2687) | public | `PORT@KDM-006` |

Pydantic request bodies are defined `app.py:177-379` — enumerate for DTO parity
in KDM-006.

## 2. Auth & session

- **Stateless JWT session** (`api/session.py`): HS256 over `KORKUT_SECRET`, claims
  `sub/oid/role/ep/iat/exp`, **12h TTL**. `office_id` and `role` are **signed
  claims** — not client-editable. Caller additionally checks `ep ==
  user.token_epoch` (`app.py:432`). **No server-side session store.**
- **Revocation** = bump `users.token_epoch` (logout, member removal, role/demo
  changes). Whole-session-family invalidation is a counter, not per-token.
- **Google OIDC only — no local passwords anywhere.** Code flow in
  `api/google_auth.py` (signed CSRF `state`, 10-min TTL), strict RS256 ID-token
  verification against Google JWKS in `api/google_oidc.py` (`aud`/`iss`/exp/
  `email_verified`). Unconfigured ⇒ 503.
- **Signup ticket**: 15-min `typ="signup"` JWT for the self-serve demo flow.
- **Cookie flags**: session cookie `httponly`, `samesite=strict`, `secure` gated
  on `KORKUT_SECURE_COOKIES`. Invite/signup cookies `samesite=lax`, scoped paths.
- **CSRF**: OAuth `state` protects the round-trip; API writes rely on
  cookie + `SameSite=Strict` (no separate CSRF token today).
- **PBKDF2 present** only for at-rest secrets (invite token hashing), not user
  passwords.
- **Rate limiting**: in-memory per-IP on callback + demo-request (window 300s /
  15 hits, `app.py:409`).

> Migration: `PORT@KDM-015` (local auth is **new** for .NET per decision docs —
> the Python baseline has none), `KDM-016` (Google OIDC parity — link by
> provider `sub`, preserve callback behavior), `KDM-017` (TOTP is **new**;
> baseline `require_2fa` column is dead — see dead-code #3), `KDM-018` (the
> decision docs move to short access + opaque rotating refresh tokens; the
> baseline's single 12h JWT is the compatibility reference, not the target).

## 3. Background jobs / workers

- **Dispatcher** (`api/dispatcher.py`, `python -m korkut.api.dispatcher`):
  one-shot pass then exits; driven every 60s by a compose service / cron. Sends
  queued `OutboundMessage` rows (`dispatch_due`, `store.py:418`) and calendar
  reminders (`dispatch_due_reminders`, `store.py:455`). Idempotent (each row →
  terminal state). → `PORT@KDM-008` (worker + outbox + idempotent jobs).
- **Email** is the **only implemented outbound channel**: `GmailChannel` SMTP
  (`smtp.gmail.com:587` STARTTLS, App Password) via `messaging.build_channel`.
  → `PORT@KDM-024`.
- **Two-way integrations (Telegram/WhatsApp)**: referenced by deploy compose +
  crontab (`korkut.integrations.runner`, Node `sidecar`) but **package absent
  from this checkout**. → `DROP` from migration scope until the source is
  located; flagged to owner (dead-code #4).
- No in-process scheduler; scheduling is external (cron/compose loop).

## 4. Database schema

- ORM in `api/models.py`; `core.db.Base`; tenant tables inherit
  `core.tenancy.TenantMixin`. **Schema built by `Base.metadata.create_all()`**
  (`core/db.py:23`) + a lightweight NULLable column-add shim (`store.py:104`).
- **Alembic is non-authoritative and dead**: only orphaned `.pyc` remain
  (logical head `b7e2c4a19f30`), no `alembic.ini`, never invoked. It has drifted
  from the ORM (contains `office_channels`, `users.require_2fa` that the ORM
  lacks). **Use the ORM as the migration baseline**, not Alembic.
- **Tenancy already present on every tenant table**: `TenantMixin` adds
  `office_id` (FK→offices, `ondelete=CASCADE`) + non-enumerable `uid` UUID.
  Isolation = Postgres **RLS** (`SET LOCAL app.current_office`, fail-closed
  policy) **+** app-layer `with_loader_criteria` filter (`tenancy.py:116`).
  Cross-tenant reads only via 2 `SECURITY DEFINER` resolvers (sign-in, invite).
  Composite FKs include `office_id` (e.g. `(office_id, matter_id)`).
- **Tables (~24)**: `offices`, `style_profiles`, `models` (global), `office_models`,
  `personas`, `memory_items`, `users`, `office_invites`, `conversations`,
  `messages`, `documents`, `document_versions`, `proposed_edits`,
  `template_defaults` (global), `template_overrides`, `outbound_messages`,
  `clients`, `matters`, `calendar_events`, `invoices`, `expenses`, `tasks`,
  `document_records`, `auth_events` (global audit).

> Migration: `PORT@KDM-011` (EF Core + UUIDv7 + indexes from current ORM shape),
> `KDM-012/013` (tenancy + RLS + real-Postgres negative tests — the baseline's
> RLS DDL and the two `SECURITY DEFINER` resolvers are the parity reference),
> `KDM-014` (audit — `auth_events` today).

## 5. Providers / models

`core/providers/`: `claude.py` (Anthropic, default cloud), `gpt.py` (OpenAI base
class), `groq.py` (Groq via OpenAI-compat), `openai_compat.py` (arbitrary
base_url — Ollama/vLLM/OpenAI). Default router built in `brain/agent.py`
(`local_available=lambda: False` ⇒ always cloud). **Per-office model is DB-driven**
(`OfficeModel`→`RegisteredModel`, `store.office_model_cfg`), **routing** from
`config/model-routing.yaml`. Cloud-bound PII redaction is **mandatory and
centralized** in `Router.respond` (`core/redactor.py`). → `PORT@KDM-033`
(provider/model/template/tool compatibility **only**; no new brain).

## 6. Templates

Root `templates/`, synced into `template_defaults` by `store.sync_template_defaults`
(SHA-256 keyed). Counts (verified): **126** petition examples, **58** contract
examples, **126** intake JSON schemas; plus `dava-workflow.json`,
`mahkemeler.json`, `contract/catalog.json`. Resolution user > office > default
(`TemplateDefault` global, `TemplateOverride` tenant). → `PORT@KDM-033`.

## 7. Toolset

`tools/*`: **23 tool folders** (16 `search_*`, 7 `fetch_*`), each a
`tool.yaml` + `client.py::run_tool`. Registry `core/tool_registry.py` discovers,
hides tools whose `requires_key` env is unset (only `TAVILY_API_KEY` gates
`search_bddk/kvkk/sigorta_tahkim`), and hides tools superseded by a composite
(`search_decisions`, `fetch_document`). Separate code-defined tool families:
inline agent tools in `app.py` (`dilekce_canvas`, `send_email`, `remember`) and
office practice-management tools (`api/office_tools.py`, create/read/update only).
→ `PORT@KDM-033` (compatibility migration only).

## 8. Test suite

34 `test_*` modules + `conftest.py` (autouse pins office 1). `pyproject.toml`:
`testpaths=tests`, `pythonpath=src`, `live` marker deselected by default. Default
suite runs **standalone** (in-memory SQLite, `respx` HTTP mocks) — **396 passed,
18 skipped**. **Exception**: `test_tenancy_rls.py` and
`test_tenant_rls_real_schema.py` need a **real Postgres** (skipped/failed
without one) — these are the parity anchor for `KDM-013`.
