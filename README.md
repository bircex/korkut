# Korkut

Korkut is an AI assistant for a Turkish law office — legal research (live case law +
legislation), petition & contract drafting, and document workflow — delivered through a
**self-hosted chat web app** that **keeps client data in the office**. It is multi-tenant:
each office self-registers and Korkut answers under that office's own identity.

> ⚠️ Private repo. Client data (`data/`), secrets (`.env`), and backups **never** go into
> the repo (see `.gitignore`).

## Repository layout

```
backend/            Python package + tests
  pyproject.toml
  src/korkut/
    core/           model router, providers, redactor, DB, tool registry
    tools/          live legal-database search & fetch tools (21)
    brain/          agent entry point (answer) + per-office persona
    api/            FastAPI app (auth, chat, documents) + built SPA (static/)
  tests/
frontend/           React + Vite single-page chat UI
deploy/             Dockerfile, docker-compose.yml, cron
docs/               design docs (architecture, security, sources, …)
config/             model-routing, redaction-rules, tools tiers
templates/          golden petition/contract examples + intake schemas
```

## Quickstart (dev)

Backend:
```bash
python3 -m venv .venv
.venv/bin/pip install -e "backend/[app,dev]"
cd backend && ../.venv/bin/pytest          # offline tests (live tests deselected by default)
../.venv/bin/python -m korkut.api          # serve the app on :8080 (needs ANTHROPIC_API_KEY)
```

Frontend:
```bash
cd frontend && npm install
npm run dev        # Vite dev server on :5173, proxies /api → :8080
npm run build      # build the SPA into backend/src/korkut/api/static/
```

Run a single tool from the CLI:
```bash
.venv/bin/python -m korkut.tools.search_yargitay --query "kira tespiti" --limit 5
```

## Quality gates

- **Backend:** `ruff check . && ruff format --check . && mypy && pytest --cov` (157 tests, ~85% coverage)
- **Frontend:** `npm run lint && npm run build`
- CI runs all of the above on every push/PR (`.github/workflows/ci.yml`); Dependabot keeps
  pip/npm/actions current.

## How it works

- **Identity:** one assistant, **Korkut**, addressed per office — the office name comes from the
  authenticated user's tenant and drives the system persona (`brain/persona.py`).
- **Models:** cloud = Claude (GPT supported); PII is redacted before any cloud call and restored
  in the reply (`core/redactor.py`, `core/router.py`).
- **Tools:** 21 CLI-runnable search/fetch tools over live Turkish legal databases (Yargıtay,
  Danıştay, UYAP Emsal, Anayasa, KİK, Rekabet, Sayıştay, GİB, mevzuat, Uyuşmazlık, KVKK, BDDK,
  Sigorta Tahkim). See `backend/src/korkut/tools/README.md`.
- **Data:** Postgres in production (SQLite for the trial); everything stays on the office server.

## Docs

- **[docs/PLAN.md](docs/PLAN.md)** — roadmap, principles, scope
- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** — process topology, message flow, data model, tools layer, approval flow
- **[docs/SOURCES.md](docs/SOURCES.md)** — legal data sources and access
- **[docs/SECURITY.md](docs/SECURITY.md)** — gateway, 2FA, network hardening
- **[docs/CORPUS.md](docs/CORPUS.md)** — research/petition data strategy
- **[docs/DOCUMENT_EDITOR.md](docs/DOCUMENT_EDITOR.md)** — petition-drafting Canvas editor
- **[docs/COMPETITORS.md](docs/COMPETITORS.md)** — competitor review
- **[docs/LOCAL_TRIAL.md](docs/LOCAL_TRIAL.md)** — running the office trial locally
