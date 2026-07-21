# Korkut .NET Backend — Target Architecture

Status: **agreed** (updated 2026-07-21). A modular monolith of shared libraries with
**three stateless hosts** — the operational API, the AI **agent**, and the **knowledge**
(RAG) service — plus a heavy, on-demand **OCR** service, all behind a single YARP gateway.
Refactored to Clean Architecture (ports & adapters). The Main API Clean Architecture
refactor + parity is **DONE**; the remaining work is the AI plane. Supersedes the
flat-`*Store` layout; the older [ARCHITECTURE.md](ARCHITECTURE.md) describes the retired
Python app.

## Principles

1. **Clean Architecture (ports & adapters).** The Application layer owns *interfaces* —
   a repository per aggregate (`IInvoiceRepository`), a service per use-case
   (`IFinanceService`), plus the infra ports (`IChatModel`, `IEmbedder`, `IObjectStore`,
   `ISecretEncryptor`, `ITextExtractor`, …). Infrastructure implements them. Dependencies
   point inward: `Domain ← Application ← Infrastructure ← Hosts`. Enforced by
   `Korkut.Architecture.Tests`. **Done for every operational module.**
2. **Feature-first organization.** Group by feature (Finance, Calendar, Agent, Knowledge…)
   inside each layer, not one flat `Persistence/` / `Endpoints/` folder.
3. **Hosts split along real seams, not for fashion.** Each host is isolated for a concrete
   reason: **load** (Agent carries the 5k-concurrent chat path), **change frequency**
   (Knowledge/RAG will keep evolving — hybrid search, re-ranking, new embedders — without
   redeploying Agent), and **usage frequency + weight** (OCR is rare, heavy, native-dep —
   invoked on demand, scaled small). All hosts reference the SAME shared libraries, so a
   split duplicates no domain logic and can be deferred/merged cheaply.
4. **Three databases, one cluster.** See *Hosts & data*. Each service owns its store;
   `Korkut.Pii` and domain logic are shared as libraries, not over the wire.
5. **Stateless everywhere.** No in-process session state; SSE is per-request; auth is a
   self-contained HS256 JWT. Every host scales by adding replicas behind PgBouncer.
6. **Single gateway.** A YARP reverse proxy (`Korkut.Gateway`) is the ONLY public entry
   point: JWT validation, rate-limiting / CORS / TLS termination, **trace-context
   propagation**, and path routing. Downstream services still enforce permissions + RLS
   (defense in depth). Clients never address a host directly.
7. **Swappable model.** The LLM sits behind provider-agnostic ports (`IChatModel.Send/
   Stream`, `IEmbedder`); adapters for Anthropic / OpenAI / Groq / OpenAI-compatible
   (Ollama, vLLM) implement them, and a config-driven `IModelRouter` picks provider +
   model per request. Change the model with config, never code.
8. **Observable by construction.** Because the system is distributed, **OpenTelemetry
   tracing is mandatory, not optional** — you cannot debug a cross-host request without
   it. Every host is instrumented for traces + metrics + structured logs from day one, and
   every LLM/embedding call is a first-class span. See *Observability*.
9. **Everything that changes model behavior is versioned and stamped into telemetry.**
   Prompt version, model id, and embedding/index version travel with each LLM/RAG call so a
   quality/cost/latency shift can always be traced to what changed. See *Versioning &
   governance*.

## Hosts & data

```
                          Browser (cookie JWT)
                                 │  https
                                 ▼
                   ┌──────────────────────────────┐
                   │  Korkut.Gateway (YARP)        │  auth · rate-limit · CORS · TLS
                   │  single public entry point    │  path routing · W3C trace propagation
                   └───┬───────────────┬───────────┘
       /api/* (ops)    │               │  /api/sessions/*, /chat/stream,
                       ▼               ▼  /api/documents/*/draft|accept|export…
       ┌───────────────────┐   ┌───────────────────────────────┐
       │  Korkut.Api       │   │  Korkut.Agent — STATELESS      │
       │  operational CRUD │   │  chat/stream · drafting · tools│
       │  N replicas       │   │  M replicas (5k concurrent)    │
       │  finance, matters,│   │  model via IModelRouter →      │
       │  clients, calendar│   │  {Anthropic|OpenAI|Groq|local} │
       │  evrak, settings, │   │  PII redaction (Korkut.Pii)    │
       │  reports, channels│   └──┬──────────────┬───────────┬──┘
       │  templates,members│ shared│ in-proc App  │ HTTP      │ in-proc App
       │  admin,auth,onbrd │ (RLS) │ (ops data)   │ /search   │ (ops data)
       └─────────┬─────────┘      │              │ /ingest   │
        shared   │ KorkutDbContext│              ▼           │
        App svc  │ + App services │   ┌─────────────────────────────┐
                 ▼  (in-proc,RLS) ▼   │  Korkut.Knowledge           │
       ┌───────────────────────────┐ │  ingest · chunk · embed ·   │
       │  Operational DB (Postgres │ │  index · search (retrieval) │
       │  + RLS, via PgBouncer)    │ │  owns pgvector · IEmbedder   │
       └───────────────────────────┘ └───┬──────────────────┬──────┘
       ┌───────────────────────────┐     │ on-demand OCR    │ owns
       │  Agent DB (Postgres)      │     ▼ (scanned only)   ▼
       │  conversations, messages, │ ┌─────────────┐  ┌──────────────────────┐
       │  drafts+versions, pii     │ │ Korkut.Ocr  │  │  Knowledge DB        │
       │  audit, llm_calls (cost)  │ │ heavy·rare· │  │  (Postgres+pgvector) │
       └───────────────────────────┘ │ native·~0   │  │  chunks, embeddings, │
         ▲ Agent only                │ scaled      │  │  embedding_version   │
                                     └─────────────┘  └──────────────────────┘

  Korkut.Pii = shared LIBRARY (used in-process by Agent at chat-time and Knowledge at
  index-time), never a network hop.
  One Postgres cluster: separate database/schema per service; pgvector enabled, used only
  by Knowledge. Split a DB to its own instance later with a connection-string change.
  Migrations: ONLY Korkut.Migrator applies ALL schemas; no host migrates on start.
```

### Korkut.Agent — the interactive plane (latency-sensitive, 5k concurrent)

- **Chat / streaming** — the LLM loop; SSE per request; conversational state in the Agent
  DB (plain Postgres, no pgvector).
- **Domain access** — calls the shared Application services in-process (e.g.
  `IClientService.CreateAsync`, `IMatterService.GetAsync`) against the operational DB,
  RLS-scoped to the caller's office. The agent's "tools" are thin wrappers over these — no
  HTTP hop, invariants + RLS live in one place.
- **Retrieval** — at query time, calls **Korkut.Knowledge** `/search` (HTTP) for grounded
  context, then prompts the LLM. It does NOT own the vector store; retrieval logic can
  evolve in Knowledge without redeploying Agent. The internal hop (~ms) is negligible
  against LLM latency (seconds).
- **Drafting** — produces document edits behind `IChatModel`; drafts + versions in the
  Agent DB, staged as pending edits for human accept/revert.
- **PII** — redact PII before any cloud-LLM call and restore it in the reply, via the
  `Korkut.Pii` library (in-process); PII audit in the Agent DB. Critical for a legal
  product. On-prem model endpoints skip redaction.

### Korkut.Knowledge — the RAG plane (owns the vector store, evolves independently)

- **Ingestion** — on a new/updated document (signalled by the operational **outbox +
  Worker**, or pulled): extract embedded text directly when present; **only fall back to
  Korkut.Ocr for scanned images / text-less PDFs**; PII-mask (Korkut.Pii); chunk; embed
  (`IEmbedder`); write vectors to the Knowledge DB (pgvector) tagged with an
  `embedding_version`.
- **Search / retrieval** — `/search` endpoint: embed the query, top-k similarity (and,
  over time, hybrid keyword + re-ranking), return grounded chunks. This is the surface
  Agent calls. Owning both index and search here keeps RAG iteration off the Agent's
  release cycle.
- Owns the **Knowledge DB** (Postgres + pgvector) and `IEmbedder`.

### Korkut.Ocr — heavy, rare, on-demand

- One job: turn a scanned image / text-less PDF into text (Tesseract/PDF native deps).
- Called **only** by Knowledge ingestion, and **only** when a document actually lacks
  embedded text — most documents skip it entirely.
- Isolated because it is rare + heavy + native: fat image and memory stay off the
  always-on hosts; scaled small (near-zero), with one warm replica to avoid cold-start
  latency.

## Gateway (Korkut.Gateway, YARP)

The single public surface. Responsibilities:

- **AuthN** — validate the JWT cookie once (`KORKUT_SECRET`); reject anonymous on protected
  routes. Services still enforce **authZ** (permissions) + RLS.
- **Routing** — path-based: agent routes (`/api/sessions/**`, `/chat/stream`,
  `/api/documents/*/draft|accept|revert|export`) → `Korkut.Agent`; all other `/api/**` →
  `Korkut.Api`. Knowledge and Ocr are **internal** (not publicly routed) — reached only by
  Agent/Knowledge in-cluster.
- **Cross-cutting** — rate limiting, CORS, TLS termination, **W3C `traceparent`
  propagation** (so a browser request is one trace across every host), request-id, SSE
  pass-through (buffering off), health aggregation.

## Model providers (swappable)

```
IChatModel   Send(request) / Stream(request)      the port Agent depends on
IEmbedder    Embed(texts)                          the port Knowledge depends on
   ▲
   │ implemented by
IModelRouter ──picks provider+model per request (config: default, per-task, per-office)
   ├─ AnthropicModel      (ANTHROPIC_API_KEY, ANTHROPIC_MODEL)
   ├─ OpenAiModel         (OPENAI_API_KEY, OPENAI_MODEL)
   ├─ GroqModel           (GROQ_API_KEY, GROQ_MODEL)
   └─ OpenAiCompatModel   (Ollama / vLLM / LM Studio — on-prem, no redaction needed)
```

`IModelRouter` selects provider + model from **config-as-code** (mirrors the retired Python
`router.py` + `model-routing.yaml`), so a model change is configuration only. Model ids are
**pinned exactly** (never a floating alias) and logged per call. Cloud-bound calls go
through `Korkut.Pii` first; on-prem endpoints skip redaction.

## Observability

Distributed topology makes this **load-bearing, not a nice-to-have**: without a single
trace spanning gateway → agent → knowledge → ocr, cross-host failures are undebuggable.

- **Instrumentation: OpenTelemetry** (traces + metrics + logs), exported over **OTLP**.
  Vendor-neutral by design — the storage backend is swappable by config, never code.
- **Tracing** — W3C `traceparent` propagates from the gateway through every host and every
  in-process Application call; one browser request = one trace.
- **Metrics** — RED (Rate / Errors / Duration) per endpoint; plus AI metrics (tokens,
  cost, queue depth, provider throttles).
- **LLM/embedding spans** — every model call is a span carrying: `provider`, `model_id`,
  `prompt_tokens`, `completion_tokens`, `cost`, `latency`, `office_id`, `feature`
  (chat/drafting/embed), `retry_count`, `cache_hit`, `pii_redacted`, plus the version
  stamps below. Mirrored into an **`llm_calls` table** (Agent DB) for cost aggregation,
  per-office billing, and eval.
- **Backend (self-hosted)** — target the **Grafana OSS stack (Loki / Tempo / Prometheus /
  Grafana, "LGTM")** as Docker-compose services alongside the app; **Seq** is an acceptable
  lighter-weight start for a small team. Because instrumentation is OpenTelemetry, moving
  between them is a collector-config change.
- **Health / readiness** per host, aggregated at the gateway.
- **PII-safe logs (legal product).** Prompt/response capture MUST be masked (via
  `Korkut.Pii`) or access-controlled; observability must never become a data-leak path.

## Versioning & governance

The unifying rule of principle 9: **anything that changes model behavior is versioned and
stamped onto the LLM/RAG span**, so quality/cost/latency regressions are always
attributable.

- **Prompt versioning** — base prompts live in git (reviewed, versioned by commit);
  per-office overrides live in the DB (the existing persona/style layers). Every call is
  stamped with a **composed-prompt fingerprint** (template id + version hash) so a prompt
  change can be A/B'd and rolled back, and a regression traced to it.
- **Model versioning** — exact model ids pinned in config-as-code and logged per call; a
  model swap is a tracked "version event" you correlate cost/quality against.
- **Embedding / index versioning** — every chunk carries an `embedding_version`. Changing
  the embedder or chunking strategy produces an incompatible vector space, so you
  **re-index under a new version and cut over** — never mix embedding spaces (silent
  retrieval corruption otherwise). Essential given RAG will keep changing.
- **Cost / token governance** — per-office **token budget + concurrency cap** enforced in
  Agent; **retry/backoff + circuit-breaker** around provider rate limits (the real
  ceiling). Budgets/limits are derived from the `llm_calls` telemetry above.
- Already handled elsewhere: **draft versioning** (DraftDocument + versions) and the
  **API contract** (OpenAPI snapshot test, `KORKUT_OPENAPI_UPDATE=1` to regenerate).

## Layer & folder shape (shared libraries)

```
Korkut.Domain/Finance/Invoice.cs                        domain model (records)
Korkut.Application/
  Abstractions/Persistence/IInvoiceRepository.cs        port
  Abstractions/Ai/IChatModel.cs  IEmbedder.cs  IModelRouter.cs
  Abstractions/Knowledge/IVectorStore.cs  ISearchService.cs
  Features/Finance/IFinanceService.cs  FinanceService.cs
  Features/Agent/IChatService.cs  IDraftingService.cs
  Features/Knowledge/IIngestionService.cs  IRetrievalService.cs
Korkut.Infrastructure/
  Persistence/Operational/KorkutDbContext.cs   Configurations/  Repositories/
  Persistence/Agent/AgentDbContext.cs          Configurations/  Repositories/
  Persistence/Knowledge/KnowledgeDbContext.cs  Configurations/  Repositories/ (pgvector)
  Ai/  Storage/  Security/  Telemetry/          adapters
Korkut.Pii/                                     shared PII redaction library
```

Endpoint flow: `endpoint → IXxxService → IXxxRepository → DbContext` (no `DbContext` in
endpoints). **This shape is already in place for the whole operational surface.**

## Cross-cutting

- **Auth** — every host validates the same JWT with `KORKUT_SECRET`; each sets the RLS
  tenant scope per request.
- **Migrations** — `Korkut.Migrator` applies the operational, agent, and knowledge schemas;
  no host migrates on start.
- **Scaling** — PgBouncer (transaction pooling) in front of the cluster; long-lived SSE
  streams hold connections. SSE is stateless → no sticky sessions; ingress must disable
  response buffering for `text/event-stream`. The LLM provider rate limit is the real
  ceiling (per-office concurrency cap + budget).

## Named patterns

Clean/Hexagonal per host · DDD bounded contexts (Operational vs Agent vs Knowledge) ·
**shared database** for the operational context (in-process reuse, no anti-corruption
layer — same code, same DB) · **database-per-service** for Agent and Knowledge ·
**RAG as an owned service** (index + retrieval evolve independently) · **conditional heavy
worker** (OCR on-demand) · outbox/event-driven document indexing · OpenTelemetry-first
observability · behavior-version stamping (prompt/model/index) into telemetry.

## Projects

```
Hosts      Korkut.Gateway (YARP)   Korkut.Api   Korkut.Agent   Korkut.Knowledge
           Korkut.Ocr   Korkut.Migrator   Korkut.Worker
Libraries  Korkut.Domain   Korkut.Application   Korkut.Infrastructure
           Korkut.SharedKernel   Korkut.Pii
```

## Build roadmap (phased PRs; suite stays green)

1. ✅ **Foundation** — this doc + `Korkut.Agent` host skeleton + Dockerfile + smoke
   coverage (PR #148).
2. ✅ **Clean foundation + operational refactor** — `Abstractions/Persistence` ports;
   `OnModelCreating` → `Configurations/`; every operational module moved to Domain models
   + repository ports + use-case services; parity gaps closed (PRs #151-163).
3. **Model provider abstraction** — `IChatModel` + `IModelRouter` + provider adapters
   (Anthropic first; OpenAI/Groq/OpenAI-compat behind config); `IEmbedder`.
4. **Agent DB + host** — add `AgentDbContext` (plain Postgres); move conversations /
   messages / drafts / versions to it; serve chat off it; reach domain data via the shared
   operational services; split agent tests onto a second `WebApplicationFactory`.
5. **Knowledge service + DB** — `Korkut.Knowledge` host + `KnowledgeDbContext` (pgvector);
   `IVectorStore`, `IEmbedder`, ingestion (outbox-driven) + `/search`; Agent calls it for
   retrieval; `embedding_version` on every chunk.
6. **OCR service** — `Korkut.Ocr` (Tesseract/PDF), called by Knowledge ingestion only for
   text-less documents; scaled small with one warm replica.
7. **PII library** — `Korkut.Pii` redaction, used in-process by Agent (chat-time) and
   Knowledge (index-time); PII audit + PII-safe logging.
8. **Gateway** — `Korkut.Gateway` (YARP): auth + routing + rate-limit/CORS + trace
   propagation; the only exposed host.
9. **Observability** — OpenTelemetry across all hosts (traces/metrics/logs, OTLP), LLM
   spans + `llm_calls` table, self-hosted backend (LGTM/Seq), health aggregation.
10. **Versioning & governance** — prompt/model/index version stamping into telemetry;
    per-office token budget + concurrency cap; retry/backoff/circuit-breaker.
11. **Deploy** — three service DBs + PgBouncer + gateway + observability stack as compose
    services; agent/knowledge/ocr images.
