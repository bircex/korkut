# Korkut .NET Backend — Target Architecture

Status: **agreed** (2026-07-20). A modular monolith of shared libraries with **two
stateless hosts** — the operational API and the AI **agent** — refactored to Clean
Architecture (ports & adapters). Supersedes the flat-`*Store` layout; the older
[ARCHITECTURE.md](ARCHITECTURE.md) describes the retired Python app.

## Principles

1. **Clean Architecture (ports & adapters).** The Application layer owns *interfaces* —
   a repository per aggregate (`IInvoiceRepository`), a service per use-case
   (`IFinanceService`), plus the infra ports (`ILlmClient`, `IObjectStore`,
   `ISecretEncryptor`, `ITextExtractor`, …). Infrastructure implements them.
   Dependencies point inward: `Domain ← Application ← Infrastructure ← Hosts`. Enforced
   by `Korkut.Architecture.Tests`.
2. **Feature-first organization.** Group by feature (Finance, Calendar, Agent…) inside
   each layer, not one flat `Persistence/` / `Endpoints/` folder.
3. **Two stateless hosts, shared libraries.** `Korkut.Api` (operational CRUD) and
   `Korkut.Agent` (AI) reference the SAME `Domain` / `Application` / `Infrastructure`
   libraries. Because both hosts run the same Application services, sharing data access
   duplicates no domain logic.
4. **Two databases, one modular monolith.**
   - **Operational DB** (Postgres + RLS): the domain. **Both hosts** connect to it
     through the shared `KorkutDbContext` + Application services (in-process, RLS-scoped)
     — the agent adds/gets clients and matters via the *same* services the API uses, so
     invariants + RLS live in one place. No cross-service HTTP hop.
   - **Agent DB** (Postgres + **pgvector**, a separate database on the same instance for
     now): the agent's private state — conversations, messages, drafts + versions, RAG
     chunks + embeddings, OCR extractions, PII audit, tool-call log. Only the agent host
     connects to it. Splits to its own instance later with no code change (own
     connection string / `AgentDbContext`).
5. **Stateless everywhere.** No in-process session state; SSE is per-request; auth is a
   self-contained HS256 JWT. Either host scales by adding replicas behind PgBouncer.
6. **Single gateway.** A YARP reverse proxy (`Korkut.Gateway`) is the ONLY public entry
   point: it validates the JWT, applies rate-limiting / CORS / TLS termination, and
   routes to the API or agent host. Downstream services still enforce permissions + RLS
   (defense in depth). Clients never address a host directly.
7. **Swappable model.** The LLM sits behind a provider-agnostic port
   (`IChatModel.Send/Stream`, `IEmbedder`); adapters for Anthropic / OpenAI / Groq /
   OpenAI-compatible (Ollama, vLLM) implement it, and a config-driven `IModelRouter`
   picks provider + model per request. Change the model with config, never code.

## Hosts & data

```
                         Browser (cookie JWT)
                                │  https
                                ▼
                   ┌─────────────────────────────┐
                   │  Korkut.Gateway (YARP)        │  auth · rate-limit · CORS · TLS
                   │  single public entry point    │  routes by path
                   └───────┬──────────────┬────────┘
        /api/* (ops)       │              │   /api/sessions/*, /chat/stream,
                           ▼              ▼   /api/documents/*/draft…
        ┌────────────────────────┐   ┌──────────────────────────────────┐
        │  Korkut.Api (ops)        │   │  Korkut.Agent (AI) — STATELESS    │
        │  stateless · N replicas  │   │  M replicas                       │
        │  finance, matters/cases, │   │  chat/stream, upload, drafting,   │
        │  clients, calendar,evrak,│   │  RAG (ingest→embed→retrieve),     │
        │  settings, reports,      │   │  OCR, PII redaction               │
        │  channels, templates,    │   │  model via IModelRouter →         │
        │  members, admin, auth,   │   │  {Anthropic|OpenAI|Groq|local}    │
        │  onboarding              │   │                                   │
        └───────────┬────────────┘   │  in-process → shared Application   │
              shared │ KorkutDbContext │  services (add/get client, case,  │
                     │ + App services  │  agenda, read persona/style/…)    │
                     ▼  (RLS)          └──────┬──────────────┬────────────┘
        ┌────────────────────────┐            │ shared        │ AgentDbContext
        │  Operational DB (RLS) ───┼───────────┘ operational   ▼
        │  one Postgres instance   │  access        ┌──────────────────┐
        │  (via PgBouncer)         │◄───────────────│  Agent DB + pgvector│
        └────────────────────────┘  same instance  └──────────────────┘
   migrations: ONLY Korkut.Migrator applies BOTH schemas; neither host migrates on start.
```

### Korkut.Agent capabilities

- **Chat / streaming** — the LLM loop; SSE per request; state in the Agent DB.
- **Domain access** — calls the shared Application services in-process (e.g.
  `IClientService.CreateAsync`, `IMatterService.GetAsync`) against the operational DB,
  RLS-scoped to the caller's office. The agent's "tools" are thin wrappers over these.
- **RAG** — on a new/updated document (signalled by the operational **outbox +
  Worker**, or pulled), OCR → chunk → embed → store vectors in the Agent DB (pgvector);
  retrieve top-k at query time and ground the LLM. Embeddings via the provider or a
  dedicated embedding model behind `IEmbedder`.
- **OCR** — `ITextExtractor` (today `PlainTextExtractor`; production adds Tesseract/PDF)
  feeds ingestion and upload-to-chat.
- **PII** — redact PII before any cloud-LLM call and restore it in the reply
  (`IPiiRedactor`); PII audit stored in the Agent DB. Critical for a legal product.

## Gateway (Korkut.Gateway, YARP)

The single public surface. Responsibilities:

- **AuthN** — validate the JWT cookie once (with `KORKUT_SECRET`); reject anonymous on
  protected routes. Services still enforce **authZ** (permissions) + RLS.
- **Routing** — path-based: agent routes (`/api/sessions/**`, `/chat/stream`,
  `/api/documents/*/draft|accept|revert|export`) → `Korkut.Agent`; all other `/api/**`
  → `Korkut.Api`.
- **Cross-cutting** — rate limiting, CORS, TLS termination, request-id/correlation,
  SSE pass-through (buffering off), health aggregation.

## Model providers (swappable)

```
IChatModel   Send(request) / Stream(request)      the port the agent depends on
IEmbedder    Embed(texts)                         RAG embeddings
   ▲
   │ implemented by
IModelRouter ──picks provider+model per request (config: default, per-task, per-office)
   ├─ AnthropicModel      (ANTHROPIC_API_KEY, ANTHROPIC_MODEL)
   ├─ OpenAiModel         (OPENAI_API_KEY, OPENAI_MODEL)
   ├─ GroqModel           (GROQ_API_KEY, GROQ_MODEL)
   └─ OpenAiCompatModel   (Ollama / vLLM / LM Studio — on-prem, no redaction needed)
```

The agent's `IChatService`/`IDraftingService` depend on `IChatModel`, never a concrete
provider. `IModelRouter` selects the provider + model from config (mirrors the retired
Python `router.py` + `model-routing.yaml`), so a model change is configuration only.
Cloud-bound calls go through `IPiiRedactor` first; on-prem endpoints skip redaction.

## Layer & folder shape (shared libraries)

```
Korkut.Domain/Finance/Invoice.cs                        entity (out of Infrastructure)
Korkut.Application/
  Abstractions/Persistence/IInvoiceRepository.cs        port
  Abstractions/Persistence/IUnitOfWork.cs
  Abstractions/Agent/IEmbedder.cs  IPiiRedactor.cs  IVectorStore.cs
  Features/Finance/IFinanceService.cs  FinanceService.cs  Dtos/  Validators/
  Features/Agent/IChatService.cs  IDraftingService.cs  IRagService.cs
Korkut.Infrastructure/
  Persistence/Operational/KorkutDbContext.cs  Configurations/  Repositories/
  Persistence/Agent/AgentDbContext.cs         Configurations/  Repositories/  (pgvector)
  Ai/  Storage/  Security/                     adapters
```

Endpoint flow: `endpoint → IFinanceService → IInvoiceRepository → DbContext` (no
`DbContext` in endpoints).

## Cross-cutting

- **Auth** — both hosts validate the same JWT with `KORKUT_SECRET`; each sets the RLS
  tenant scope per request.
- **Migrations** — `Korkut.Migrator` applies BOTH the operational and agent schemas;
  neither host migrates on start.
- **Scaling** — PgBouncer (transaction pooling) in front of the DB; long-lived SSE
  streams hold connections. SSE is stateless → no sticky sessions; ingress must disable
  response buffering for `text/event-stream`. The LLM provider rate limit is the real
  ceiling (add a per-office concurrency cap).

## Named patterns

Clean/Hexagonal per host · DDD bounded contexts (Operational vs Agent) · **shared
database** for the operational context between two hosts of one modular monolith ·
**database-per-context** for the agent's private store · **Agentic RAG** ingest/retrieve ·
outbox/event-driven document indexing · in-process domain reuse (no anti-corruption layer
needed — same code, same DB).

## Projects

```
Hosts      Korkut.Gateway (YARP)   Korkut.Api   Korkut.Agent   Korkut.Migrator   Korkut.Worker
Libraries  Korkut.Domain   Korkut.Application   Korkut.Infrastructure   Korkut.SharedKernel
```

## Build roadmap (phased PRs; suite stays green)

1. ✅ **Foundation** — this doc + `Korkut.Agent` host skeleton + Dockerfile + smoke
   coverage (PR #148).
2. **Clean foundation** — `Abstractions/Persistence` ports + `IUnitOfWork`; split
   `OnModelCreating` → `Configurations/`.
3. **Finance reference** — the CRUD pattern: Domain models, `FinanceStore` →
   `InvoiceRepository`/`ExpenseRepository` behind ports, `IFinanceService`, thin endpoint.
4. **Model provider abstraction** — `IChatModel` + `IModelRouter` + provider adapters
   (Anthropic first, OpenAI/Groq/OpenAI-compat behind config); `IEmbedder`.
5. **Agent DB + host** — add `AgentDbContext` (+ pgvector); move
   conversations/messages/drafts/versions to it; the agent host serves chat off the Agent
   DB and reaches domain data via the shared operational services; move agent endpoints
   off `Korkut.Api`; split agent tests onto a second `WebApplicationFactory`.
6. **Gateway** — `Korkut.Gateway` (YARP): auth + routing + rate-limit/CORS; make it the
   only exposed host.
7. **RAG + OCR + PII** — ingestion (outbox-driven), `IVectorStore` (pgvector),
   `IEmbedder`, real OCR, `IPiiRedactor`.
8. **Roll out** the repository/service pattern across the remaining CRUD modules.
9. **Deploy** — agent DB + PgBouncer + gateway + `agent` compose services.
