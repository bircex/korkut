# Korkut

An assistant that runs a law firm's daily work (court hearing reminders, petition drafts, legal
research) over **Mattermost**, **keeping the data in the office**. The user
talks to a single identity — **Korkut** — while specialized modules run in parallel under the hood.

> ⚠️ This repo is private. Client data (`data/`), secrets (`.env`), and backups **never**
> go into the repo (see `.gitignore`).

## Documents
- **[PLAN.md](PLAN.md)** — roadmap, principles, phases, technology, scope decisions
- **[ARCHITECTURE.md](ARCHITECTURE.md)** — technical design: process topology, message flow, core,
  data model, Korkut's internal structure, **tools layer (§12)**, approval flow
- **[SOURCES.md](SOURCES.md)** — legal data sources and access (case law/legislation)
- **[SECURITY.md](SECURITY.md)** — single-gate Mattermost, mobile, 2FA, network hardening
- **[CORPUS.md](CORPUS.md)** — research/petition data strategy: RAG (now) → focused index → broad corpus
- **[COMPETITORS.md](COMPETITORS.md)** — competitor products (preliminary review); detailed research pending

## Architecture overview
- **Interface:** self-hosted Mattermost (single gate, mobile, mandatory 2FA); channels + DMs
- **Korkut:** single identity; specialized modules under the hood — routing, court hearing, petition, research (parallel)
- **Model:** cloud = GPT (with redacted text); local model once the hardware arrives
- **Capabilities:** tools under `tools/`, run from the CLI, configured into Korkut
- **Data:** Postgres + `data/files/`; all on the server

## Status
Design complete (PLAN/ARCHITECTURE/SOURCES/SECURITY/CORPUS/COMPETITORS).
- **Tools layer (done):** 11 CLI tools under `tools/` (`search_*` + `fetch_*`), all verified
  live against the real public APIs.
- **Core / Phase 1 (done):** `core/redactor.py` (PII detection + tokenization + restore),
  `core/router.py` (specialty→backend + local↔cloud fallback + redaction chokepoint),
  `core/providers/gpt.py` (OpenAI tool-calling loop), `core/db.py` (SQLAlchemy models:
  client/case/hearing/petition). 58 tests passing.

Next: Mattermost bridge (Phase 2) + the Korkut agent loop, then hearing/petition/research modules.

## Quickstart (dev)
```bash
python3 -m venv .venv && .venv/bin/pip install -e ".[dev]"
.venv/bin/pytest                       # offline tests (live tests deselected by default)
.venv/bin/python -m tools.search_yargitay --query "kira tespiti" --limit 5
```
