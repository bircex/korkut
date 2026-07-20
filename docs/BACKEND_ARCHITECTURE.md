# Korkut .NET Backend — Target Architecture

Status: **proposed** (agreed 2026-07-20). Guides the refactor away from concrete
`*Store` data-access toward ports-and-adapters (Clean Architecture) and splits the AI
**agent** into its own independently scalable, stateless host. Supersedes the flat-store
layout; the older [ARCHITECTURE.md](ARCHITECTURE.md) describes the retired Python app.

## Principles

1. **Ports & adapters.** The Application layer owns *interfaces* — one repository per
   aggregate (`IInvoiceRepository`), one service per use-case (`IFinanceService`), plus
   the infra ports that already exist (`ILlmClient`, `IObjectStore`, `ISecretEncryptor`,
   …). Infrastructure implements them. Dependencies point inward:
   `Domain ← Application ← Infrastructure ← Hosts`. Enforced by `Korkut.Architecture.Tests`.
2. **Feature-first organization.** Group by feature (Finance, Calendar, Agent…) inside
   each layer instead of one flat `Persistence/` / `Endpoints/` folder.
3. **Two stateless hosts, shared libraries.** `Korkut.Api` (CRUD) and `Korkut.Agent`
   (AI) reference the same `Domain` / `Application` / `Infrastructure` class libraries.
   Repositories and common services are shared; **the agent host owns every AI
   operation** and the API host owns none of them.
4. **Stateless everywhere.** No in-process session state. Conversation/message/document
   state lives in Postgres; SSE streams are single requests; auth is a self-contained
   HS256 JWT. Either host scales by adding replicas behind a load balancer.

## Layers (shared class libraries)

```
Korkut.Domain          entities, value objects, invariants — no EF, no I/O
Korkut.Application     use-cases + PORTS (repository/service interfaces), DTOs, validators
Korkut.Infrastructure  adapters: DbContext, EF configs, repositories, service impls, LLM/storage
Korkut.SharedKernel    primitives (ids, clock, time)
```

### Folder shape (per feature)

```
Korkut.Domain/Finance/Invoice.cs                        entity (moves out of Infrastructure)
Korkut.Application/
  Abstractions/Persistence/IInvoiceRepository.cs        port
  Abstractions/Persistence/IUnitOfWork.cs
  Features/Finance/IFinanceService.cs                   use-case interface
  Features/Finance/FinanceService.cs                    orchestration (calls repositories)
  Features/Finance/Dtos/…                               request/response DTOs
  Features/Finance/Validators/…
Korkut.Infrastructure/
  Persistence/KorkutDbContext.cs
  Persistence/Configurations/InvoiceConfiguration.cs    (split out of OnModelCreating)
  Persistence/Repositories/InvoiceRepository.cs         : IInvoiceRepository (was FinanceStore)
```

Endpoint flow becomes `endpoint → IFinanceService → IInvoiceRepository → DbContext`
(today: `endpoint → FinanceStore(DbContext)`). No `DbContext` in endpoints.

## Hosts

```
                         ┌───────────────────┐
  browser (cookie JWT) ► │  Korkut.Api         │─┐  finance, matters, clients, calendar,
                         │  N replicas         │ │  settings, reports, channels, templates,
                         └───────────────────┘ │  export, coverage, evrak, members, admin,
                                                │  auth, onboarding
                         ┌───────────────────┐ ├─► Postgres (RLS, korkut_app) via PgBouncer
  /api/sessions/*        │  Korkut.Agent       │─┘
  /api/documents/*/draft │  M replicas         │──► Anthropic API
  /chat/stream         ► │  (STATELESS)        │
                         └───────────────────┘
        both hosts reference Korkut.Application + Korkut.Infrastructure
        migrations: ONLY Korkut.Migrator (neither host migrates on start)
```

### Korkut.Agent (new host)

Owns **all** agent operations, served *only* here:

- `GET/POST /api/sessions`, `GET/PATCH/DELETE /api/sessions/{id}`, `POST /{id}/clone`
- `POST /api/sessions/{id}/chat`, `POST /api/sessions/{id}/chat/stream` (SSE)
- `POST /api/sessions/{id}/upload`
- `GET /api/sessions/{id}/document`, `POST /api/documents/{id}/draft|accept|revert`,
  `PUT /api/documents/{id}`, `GET /api/documents/{id}/export`

Application services (`IChatService`, `IDraftingService`) + their repositories
(`ISessionRepository`, `IDraftDocumentRepository`) live in the shared libraries; the
agent host wires and maps them. The `templates/` catalog ships in the agent image (the
drafter reads it). Same auth handler + `KORKUT_SECRET`, same RLS tenant scope.

## Scaling & operational notes

- **Independent replicas.** Chat/streaming has a different profile (seconds-to-minutes
  connections, provider-bound). Isolating it means an LLM slowdown never starves CRUD.
- **Connection pooling.** M agent replicas × pool size must stay under Postgres
  `max_connections` — front the DB with **PgBouncer** (transaction pooling); long-lived
  streams hold connections.
- **SSE is stateless** — no sticky sessions; the ingress must disable response buffering
  for `text/event-stream`.
- **Provider rate limit is the real ceiling** — add a per-office concurrency cap later.
- **Observability** — both hosts use `AddKorkutObservability(<name>)`.

## Migration plan (phased, each a reviewed PR; suite stays green)

1. **Foundation** — `Abstractions/Persistence` ports + `IUnitOfWork`; split
   `OnModelCreating` into `Configurations/`.
2. **Reference feature — Finance** — Invoice/Expense: entity → Domain, `FinanceStore` →
   `InvoiceRepository`/`ExpenseRepository` behind ports, `IFinanceService`, endpoint
   calls the service. Proves the CRUD pattern.
3. **Reference host — Korkut.Agent** — stand up the host; move the agent feature
   (sessions/chat first) behind `ISessionRepository`/`IChatService`; remove agent
   endpoints from `Korkut.Api`; split agent integration tests onto the agent host
   (a second `WebApplicationFactory<Korkut.Agent.Program>`).
4. **Roll out** the repository/service pattern across the remaining CRUD modules.
5. **Move remaining agent ops** (upload, drafting, outbox) fully onto `Korkut.Agent`.
6. **Compose/deploy** — add the `agent` service + PgBouncer to the deployment compose.
