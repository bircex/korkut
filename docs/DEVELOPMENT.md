# Development

This is the day-to-day developer loop. For first-time setup, see [INSTALL.md](INSTALL.md); for the
system design, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Layout

```
backend/                Python package (src-layout) + tests
  src/korkut/
    api/                FastAPI app, auth/2FA, matters, documents, dispatcher, adduser
    core/               agent loop, tool registry, model router, providers, redactor, DB
    brain/              agent entry point (answer) + office persona
    tools/              legal-database search/fetch tools (auto-discovered)
    messaging/          outbound channels (e-mail now)
    integrations/       inbound two-way connectors (Telegram/WhatsApp/e-mail)
    api/static/         the prebuilt SPA (committed; source lives in the korkut-ui repo)
deploy/                 Dockerfile, docker-compose(.prod).yml, cron
config/                 model-routing, redaction rules, tool tiers
templates/              petition/contract examples + intake schemas
scripts/                install + release helpers
```

The React SPA now lives in the separate **korkut-ui** repo and the Astro landing site in
**korkut-website**. This backend only ships the prebuilt SPA under `backend/src/korkut/api/static`.

## Running the dev servers

Two terminals:

```bash
# 1) API server on :8080
cd backend && ../.venv/bin/python -m korkut.api

# 2) Vite dev server on :5173 (proxies /api -> :8080, hot reload) — from the sibling korkut-ui repo
cd ../korkut-ui && npm run dev
```

Rebuild the production SPA from the sibling korkut-ui repo into the backend's static dir (served by
FastAPI at `/`):

```bash
make ui
```

## Quality gates

Run before every commit (CI enforces the same — `.github/workflows/ci.yml`):

```bash
make test     # backend: pytest + ruff check + mypy
make lint     # backend ruff (check + format)
make ui       # rebuild the SPA from ../korkut-ui into the backend static dir
```

Or directly:

```bash
cd backend && ../.venv/bin/ruff check . && ../.venv/bin/ruff format --check . \
  && ../.venv/bin/mypy && ../.venv/bin/python -m pytest
```

Keep the tree **ruff-clean**, **mypy-clean**, and free of dead code. New code needs tests; CI fails
under the coverage floor.

## CLI entry points

```bash
python -m korkut                       # terminal chat (brain only, no UI)
python -m korkut.api                   # FastAPI web server (:8080)
python -m korkut.api.adduser <user>    # create/manage users, reset 2FA, unlock
python -m korkut.api.dispatcher        # flush queued outbound messages (cron)
python -m korkut.integrations.runner   # poll connectors, answer inbound (cron)
python -m korkut.tools.<name> --query "..."   # run a single legal-database tool
```

## Adding a legal-database tool

Tools live under `backend/src/korkut/tools/<name>/` and are auto-discovered by the registry
(`core/tool_registry.py`). A tool is a self-contained folder:

```
tools/<name>/
  tool.yaml     # name, description, parameters, requires_key
  client.py     # run_tool(**params) -> JSON-serializable dict
  __main__.py   # CLI entry (reuses tools/_base/cli)
  README.md     # what it does, parameters, example
```

Keep the offered tool surface tight: prefer one consolidated tool with a `source` enum over many
near-identical ones (see `tools/search_decisions/`). A composite tool can hide the ones it replaces
via `supersedes` in its `tool.yaml`. See [ARCHITECTURE.md](ARCHITECTURE.md) and
`backend/src/korkut/tools/README.md`.

## Tests

`pytest` runs offline by default (network/live tests are deselected). The web tests reload the store
and app against a temp SQLite file and patch the brain. See `backend/tests/` for the patterns
(`test_matters.py`, `test_matter_tools.py`, `test_web_auth.py`).
