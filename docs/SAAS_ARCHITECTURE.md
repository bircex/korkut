# Korkut SaaS — Multi-Tenant Architecture

Status: **design locked, build in progress**. This is the blueprint for turning Korkut from a
single-tenant, self-hosted, open-source app into a **closed-source multi-tenant cloud SaaS** that
runs **~200 law offices concurrently, fully isolated, with maximum data security**.

Supersedes the "one office per install" model in [ARCHITECTURE.md](ARCHITECTURE.md).

## Locked decisions

| Area | Decision |
|---|---|
| Tenancy | **Pooled** — one Postgres, `office_id` on every row, **Postgres RLS** enforces isolation, per-tenant field/blob encryption |
| Auth | **Google OIDC only** — no password / TOTP / recovery codes; 2FA delegated to Google |
| Onboarding | **Invite-token** — operator issues an office-registration token; owner signs in with Google and redeems it |
| Model selection | **Operator-controlled in code/secrets** — tenants do not supply keys or pick providers; cloud-bound text still redacted first |
| Telegram | **One shared bot**, per-office isolation via **token-based linking** |
| Deploy | **Single VPS**, GitHub Actions → GHCR image → SSH → `docker compose pull && up` |
| Source | **Closed** — website drops open-source/clone messaging |
| Migrations | **None yet** — fresh schema (no legacy data to preserve); one Alembic baseline once schema stabilizes |

## 1. Tenancy model — pooled + RLS + encryption

One Postgres database. Every tenant-owned row carries `office_id`. Isolation is enforced at **two
layers** so a forgotten `WHERE` can never leak data:

1. **Postgres Row-Level Security (RLS)** — every tenant table has a policy
   `USING (office_id = current_setting('app.current_office')::bigint)`. The app connects as a
   **non-superuser** role (RLS is not bypassed). Per request we run
   `SET LOCAL app.current_office = :oid` inside the transaction; every query is then automatically
   scoped by Postgres itself.
2. **Application scoping** — a request-scoped tenant context + SQLAlchemy `with_loader_criteria`
   global filter (also covers the SQLite dev path, which has no RLS).

Rationale: matches the existing single-`Base` SQLAlchemy design, scales cleanly to 200 offices, and
RLS gives DB-enforced defense-in-depth. DB-per-tenant remains available later as an enterprise tier
(same code, different `KORKUT_DB_URL` resolved from tenant context) for firms that demand physical
isolation.

## 2. Data model

`Office` stops being a singleton and becomes the tenant root. **Every** domain table gains
`office_id BIGINT NOT NULL REFERENCES offices(id)`:

`users, clients, matters, conversations, messages, documents, document_versions, proposed_edits,
calendar_events, invoices, expenses, tasks, document_records, outbound_messages, integration_links,
integration_threads, memory_items, personas, office_channels, style_profiles, template_overrides,
recovery_codes(via user)`.

Uniqueness moves from global to per-tenant:
- `users`: `UNIQUE (office_id, google_sub)` and `UNIQUE (office_id, email)` (drop global username unique)
- `office_channels`: `UNIQUE (office_id, kind)`
- `integration_links`: `UNIQUE (office_id, channel, external_id)` and `UNIQUE (channel, external_id)` is **removed**
- `personas`, `office_models` (operator-managed now): keyed by `office_id`

Removed/changed tables: `office_models` becomes operator config (see §5) — no per-tenant API keys.
Password/TOTP columns and `recovery_codes` are dropped (Google auth). New tables:
- `office_invites` — `(id, token_hash, office_name, email?, created_by, expires_at, redeemed_at)`
- `telegram_link_tokens` — `(id, office_id, user_id, token_hash, expires_at, used_at)`
- `audit_events` — per-office append-only (replaces the global `auth_events`)

Every write auto-stamps `office_id` from the request tenant context; RLS rejects cross-tenant writes.

## 3. Request lifecycle & tenant context

```
Request → resolve session (Google-issued) → office_id in signed session
        → open DB transaction → SET LOCAL app.current_office = office_id
        → handlers/tools/agent all read office_id from a contextvar
        → RLS + app filter scope every query → response
```

- `office_id` lives in a `contextvars.ContextVar` set by ASGI middleware, so **no global singleton
  leaks across concurrent tenants** — safe for 200 simultaneous requests/agent runs.
- `api/store.py`'s module-level session factory gains a `tenant_session(office_id)` that sets the
  RLS GUC on checkout.
- Tenant resolution: subdomain `{office-slug}.app.korkutai.com` **or** an active-office claim in the
  session (workspace switch). Slug stored on `offices`.

## 4. Auth — Google OIDC only

- Standard OIDC Authorization Code + PKCE against Google. Backend verifies the ID token, reads
  `sub`, `email`, `email_verified`, `hd` (Workspace domain).
- **No passwords, TOTP, recovery codes, or lockout machinery** — delete `api/totp.py`, the password
  columns, and the brute-force code in `api/app.py`. 2FA is Google's responsibility.
- Session: signed cookie (existing HMAC scheme) carrying `user_id + office_id + token_epoch`; server
  revocation via `token_epoch`. `KORKUT_SECRET` stays (operator-global, stable, from secrets).
- A Google identity with no matching `users` row can only proceed by redeeming an invite token
  (§6) — otherwise 403. This keeps the SaaS closed.

## 5. Model routing — operator-controlled

- Providers/keys (`ANTHROPIC_API_KEY`, `GROQ_API_KEY`, `OPENAI_API_KEY`, `TAVILY_API_KEY`) live in
  **operator secrets**, not per-tenant. `config/model-routing.yaml` + `config/tools.yaml` are the
  single operator policy. The `OfficeModel`/"Settings → Model" surface is removed from the UI.
- The router (`core/router.py`) is instantiated **per request with tenant context** (not a global
  singleton), so concurrent tenants never share mutable provider state.
- Redaction before any cloud call **stays** (`core/redactor.py`), seeded per-request with the
  office's own known entities (clients/matters) so masking is tenant-correct.
- Usage is metered per `office_id` for billing (token counts logged with tenant id).

## 6. Onboarding — invite tokens

1. Operator (admin CLI / console) issues an **office invite**: `python -m korkut.api.invite --office
   "Demir Hukuk" [--email owner@firm.com]` → prints a one-time token (only its hash is stored).
2. Prospective owner opens `app.korkutai.com`, **Sign in with Google**, then pastes the token.
3. Backend validates the token, creates the `Office` (+ slug) and the owner `User`, marks the invite
   redeemed. Owner is `role=owner`.
4. Owner invites members: generates member-invite tokens; each member signs in with Google + redeems
   → joins that office as `member`. Google `hd` (Workspace domain) can optionally auto-restrict.

## 7. Telegram — one bot, per-office isolation

- **One** operator-owned bot (single `TELEGRAM_BOT_TOKEN` in secrets). The integrations runner polls
  this one bot for all offices.
- **Linking by token**: in-app, a member generates a `telegram_link_tokens` one-time code. They DM
  the bot `/start <token>`. The runner resolves the token → `(office_id, user_id)`, writes an
  `integration_links` row `(office_id, channel='telegram', external_id=<telegram_user_id>)`.
- **Routing**: every inbound Telegram message resolves `external_id → integration_link → office_id`
  first. If unlinked → the bot replies "bağlantı için uygulamadan token alın". With `office_id`
  resolved, the tenant context is set and the message is answered by that office's brain against that
  office's data only. `integration_threads` maps chat → the office's `Conversation`. No cross-office
  mixing is possible because the link table is the sole router and RLS backstops every query.
- Same pattern extends to WhatsApp/email later.

## 8. Agent runtime — tenant-safe for 200 concurrent

- Kill the process-wide singletons (`brain/agent.py::_router`, redactor/registry caches). Router and
  redactor are built **per request** from tenant context; the tool registry's *discovery* is cached
  (immutable) but *availability/keys* come from operator config, not env-at-import.
- `agent.answer(...)`, `resilience.guarded_answer(...)`, `integrations/gateway.handle(...)` all take
  an explicit `office_id`; nothing reads "the office" implicitly.
- Concurrency: async endpoints, a sized Postgres pool (e.g. PgBouncer in front for 200 tenants),
  per-office in-flight/rate caps in **Redis** (shared across app instances), and a bounded worker
  pool so one noisy tenant can't starve others.

## 9. Secrets & encryption (max data security)

- **In transit**: TLS everywhere (reverse proxy), HSTS, secure cookies.
- **At rest**: Postgres volume + object storage encrypted. Sensitive per-tenant fields
  (integration secrets, tokens) use **application-level envelope encryption** — a master key from the
  secrets store wraps per-office data keys; ciphertext stored in DB.
- **Isolation**: app connects as a non-superuser RLS role; least-privilege DB grants; per-tenant
  object-storage prefixes; audit log per office; automated **cross-tenant isolation tests** in CI
  (attempt to read office B as office A → must 404/deny).
- **KVKK**: TR/EU data residency, per-tenant **export** and **erasure** endpoints, DPA + aydınlatma
  metni, retained redaction-before-cloud. Operator is now a *veri işleyen* for many offices.

## 10. Documents / object storage

Uploads are currently OCR'd then discarded. For the archive direction (see architecture-notes:
MinIO/document-graph), store originals in **Cloudflare R2** under `office/{office_id}/...` keys, with
a `documents`/`document_records` row referencing the key. R2 chosen because DNS/CDN is already on
Cloudflare. Per-tenant prefixes + signed URLs; never a shared bucket path.

## 11. Frontend (korkut-ui) changes

- Replace the login screen with **Sign in with Google** + a **redeem invite token** step.
- Remove **Settings → Model** (operator controls models now).
- Add a **workspace/office** indicator (name, member management for owners, invite generation,
  Telegram-link token generation).
- All API calls already go through `/api`; tenant is implicit in the session.

## 12. Website (korkut-website) changes

- Remove open-source / MIT / `git clone` messaging and the GitHub links.
- Reframe as a commercial SaaS: value prop, **"Giriş yap" (app.korkutai.com)**, **demo/erişim
  talebi** (invite request), pricing, KVKK/DPA + aydınlatma pages.

## 13. Deployment — single VPS auto-deploy

- **CI/CD**: on push to `main`, GitHub Actions runs the quality gate, builds the image, pushes to
  **GHCR**, then SSHes into the VPS and runs `docker compose -f deploy/docker-compose.prod.yml pull
  && up -d`. Required repo secrets: `GHCR` (via `GITHUB_TOKEN`), `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`.
- **Prod topology** on the VPS: reverse proxy (Caddy/Traefik, TLS via Let's Encrypt) → `korkut` app
  (127.0.0.1:8080) → Postgres + Redis + `dispatcher` + `integrations` + the single `telegram` poller,
  all in `docker-compose.prod.yml`. Cloudflare in front (DNS/WAF/CDN). R2 for documents. Managed or
  self-hosted Postgres with encrypted volume + nightly backups.

## 14. Build phases

0. **Infra/CI** — prod compose (Postgres+Redis+workers), VPS auto-deploy pipeline, Google OAuth app,
   R2 bucket, secrets.
1. **Tenancy core** — fresh schema with `office_id` everywhere + RLS policies + tenant contextvar +
   `tenant_session`; drop old migrations; isolation tests.
2. **Auth** — Google OIDC, drop password/TOTP; session `office_id`; invite-token office creation +
   member invites.
3. **Runtime tenant-safety** — de-singleton router/redactor/registry; thread `office_id` through
   agent/gateway/workers; Redis rate/limits; operator model config.
4. **Telegram single-bot** — token linking + per-office routing; then WhatsApp/email.
5. **Docs/UI/Website** — R2 documents, UI login+workspace, website SaaS reframe, admin console.
6. **Hardening** — audit, backups, export/erasure, isolation-test suite, load test at 200 tenants.
