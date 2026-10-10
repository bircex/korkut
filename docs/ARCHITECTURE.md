# Architecture

> **Runtime note:** Korkut runs on **.NET 10** at the repository root (`Korkut.slnx`, `src/`), with the React app in `ui/`, installed locally with Docker Compose. Commands below that reference `python -m korkut`, `backend/…` or `deploy/…` are from the retired Python runtime.

Korkut is a self-hosted, single-tenant AI assistant for a Turkish law office. It is a single
FastAPI web app that serves a React SPA, runs an agent over live legal-database tools, and keeps
each office's data on the office's own machine.

This document describes the system as it is built today.

## Big picture

```
Browser (React SPA)
      │  /api/*
      ▼
FastAPI app (korkut.api.app)
      ├─ auth (Google OIDC sign-in; session is a signed JWT)
      ├─ matters / clients / hearings / deadlines  (the case-file domain)
      ├─ chat  ──►  brain.agent.answer(...)
      │                 ├─ persona (per office)
      │                 ├─ tools (registry + per-request handler tools)
      │                 └─ router ─► provider (cloud or local), redaction for cloud
      ├─ documents (petition drafting Canvas) + DOCX/UDF export
      └─ outbound messaging (e-mail) + inbound integrations (Telegram/WhatsApp/e-mail)
      ▼
SQLite (trial) or Postgres (production)  +  config/ + templates/
```

## One office per install

Korkut is single-tenant: each self-hosted install serves exactly one law office. The office is
provisioned from the admin side (`python -m korkut.api.adduser <email>`) and its first user becomes
the `owner`; the owner then adds members by pre-provisioning their e-mail or issuing a one-time invite
(`POST /api/invites`; usernames are unique). Sign-in is Google OIDC only (`GET /api/auth/google/start`
→ `/api/auth/google/callback`), and a person's row binds to their Google account on first sign-in.
Domain rows carry no `office_id`: everything belongs to the one
office, so the data-access layer (`api/matters_store.py`, `api/store.py`) is office-implicit. `Office`
remains as a singleton identity row (its name drives the persona) and `User.role` is `owner`/`member`.

## The matter-centric domain

A lawyer's work hangs off a **matter** (case file / dosya). The core entities (`api/models.py`):

- **Client** — a müvekkil.
- **Matter** — a case file: title, optional client, court, file number, case type, counterparty,
  status, notes.
- **Hearing** — a duruşma on a matter's calendar (date/time/location/type/result).
- **Deadline** — a procedural süre (cevap/istinaf/temyiz): a date by which an act must be done.

Chats (`Conversation.matter_id`) and correspondence (`OutboundMessage.matter_id`) can be filed under
a matter, and a matter surfaces its own drafts (documents in its filed conversations). The
**agenda** is a unified, date-sorted view of upcoming hearings + pending deadlines, surfaced both in
the UI and to the agent. Data access lives in one place — `api/matters_store.py` — shared by the
REST endpoints and the agent tools, so scoping and shaping never diverge.

## The agentic core

Korkut is **one capable agent**, not a multi-agent system. Intent is the model's decision; tools map
to the domain's natural actions. The pieces (`backend/src/korkut/`):

- **Tool registry** (`core/tool_registry.py`) — discovers tools under `tools/*/tool.yaml`, builds the
  model's function-calling schema, gates tools whose API key is absent, and runs a tool by name. A
  composite tool can hide the ones it replaces via `supersedes`.
- **Agent loop** (`core/agent_loop.py`) — provider-agnostic: given the normalized conversation +
  tools, ask the model for one step (final text or tool calls), execute the calls, append results,
  repeat until a final answer or `max_iters`. A tool name in the `handlers` map runs in-process; any
  other name runs via the registry.
- **Router** (`core/router.py`) — maps a specialty to a backend (cloud/local) per
  `config/model-routing.yaml`. For cloud-bound calls it **redacts PII first and restores it in the
  reply** (`core/redactor.py`); for a local (on-prem) endpoint it skips redaction because the text
  never leaves the machine.
- **Providers** (`core/providers/`) — `ClaudeProvider`, `GptProvider`, `GroqProvider`, and
  `OpenAICompatProvider` (any OpenAI-compatible endpoint: Ollama, LM Studio, vLLM). All implement one
  `step(messages, tools)` protocol.
- **Persona** (`brain/persona.py`) and the entry point `brain/agent.py::answer(...)` wire the persona
  + tools + router into a single call.

### Tools

Two kinds:

1. **Stateless legal-database tools** under `tools/*/` — CLI-runnable, auto-discovered, hand-written
   `tool.yaml` schemas. The search/fetch surface is **consolidated**: a single
   `search_decisions(source=...)` and `fetch_document(source=...)` route to per-source backends
   (Yargıtay, Danıştay, UYAP Emsal, mevzuat, KİK, Rekabet, Sayıştay, GİB, Uyuşmazlık, KVKK, BDDK,
   insurance arbitration; see [SOURCES.md](SOURCES.md) for the full list and how they're accessed).
   This keeps the per-request token footprint small — important for weak local models.
2. **Per-request handler tools** built in the chat endpoint (`api/matter_tools.py`,
   `_CANVAS_TOOL`, `_SEND_EMAIL_TOOL`). These close over the authenticated user, enforce `office_id`,
   and act on the office's own data (matters, hearings, deadlines, drafting Canvas signal, e-mail
   drafts). Internal writes are direct; outward-facing actions (sending e-mail, finalizing a draft)
   stay behind explicit human approval.

### Design principles

- **Model-driven intent, not keyword gates.** Whether to draft a document, open a matter, or just
  answer is the model's call via the tool contracts — no regex pre-classification.
- **Context discipline.** Tool schemas are sent on every model step, so the offered surface is kept
  tight (consolidated search) and the persona lean.
- **Human-in-the-loop for irreversible/outward actions.** E-mail is drafted and queued, never sent
  without the lawyer's confirmation.

## Drafting & documents

Petition drafting happens in a Canvas editor (`Document`/`DocumentVersion`/`ProposedEdit`). The
office's templates and intake schemas live in `templates/`; theme resolution is lawyer → office →
built-in default (`store.effective_theme`). Drafts export to DOCX/UDF.

## Messaging & integrations

- **Outbound** (`messaging/`) — channel-agnostic sending (e-mail now), with a propose → confirm →
  queue → send lifecycle; scheduled sends are flushed by `api.dispatcher`.
- **Inbound** (`integrations/`) — two-way connectors (Telegram/WhatsApp/e-mail). A member links their
  account with a one-time code; the runner polls and answers through the same brain. WhatsApp uses a
  Node/Baileys sidecar.

## Data & configuration

- **Database** — SQLAlchemy models on one `Base`; SQLite for the trial, Postgres in production.
  Schema is created with `create_all`; lightweight in-code migrations add columns / drop dormant
  tables (`api/store.py`).
- **Paths** (`paths.py`) — `config/`, `templates/`, and `data/` are resolved relative to the repo
  root (`KORKUT_ROOT` or a walk-up), so edits survive an editable install.
- **Secrets** — `.env` (gitignored); `KORKUT_SECRET` signs session cookies. See
  [`.env.example`](../.env.example) for the full environment.
