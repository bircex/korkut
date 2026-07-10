# Contributing to Korkut

Thanks for your interest in improving Korkut. This guide covers how to set up the project, the
quality gates your change must pass, and the conventions we follow.

By participating you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

## Getting set up

The fastest path is the install script for your OS — see **[docs/INSTALL.md](docs/INSTALL.md)**:

```bash
# macOS / Linux
./scripts/install.sh
# Windows (PowerShell)
./scripts/install.ps1
```

For the manual setup and the day-to-day development loop (dev server, tests, building the SPA,
running a single tool from the CLI), see **[docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)**.

## Quality gates

Every change must keep the full suite green. CI (`.github/workflows/ci.yml`) runs these on every
push and pull request; run them locally first:

**Backend** (from `backend/`):

```bash
../.venv/bin/ruff check .
../.venv/bin/ruff format --check .
../.venv/bin/mypy
../.venv/bin/python -m pytest
```

**Frontend** — the React SPA now lives in the separate **korkut-ui** repo. Make UI changes there;
this repo only ships the prebuilt SPA under `backend/src/korkut/api/static`. To refresh it from a
sibling `korkut-ui` checkout, run `make ui`.

Or use the shortcuts: `make test`, `make lint`.

- New code needs tests. The backend keeps a coverage floor (CI fails under it).
- Keep the codebase **ruff-clean** and **mypy-clean** — no new lint or type errors, no dead code.

## Pull request flow

1. Fork and branch from `main` (e.g. `feature/agenda-export`, `fix/hearing-timezone`).
2. Make focused commits; keep unrelated changes out of the PR.
3. Ensure all quality gates pass locally.
4. Open a PR using the template; describe **what** changed and **how you tested it**.

### Commit messages

- **English**, imperative mood, concise subject (≤ ~72 chars), with a body explaining the *why*.
- Co-authored work includes a trailer, e.g.:

  ```
  Add CSV export to the office agenda

  Lets owners pull upcoming hearings/deadlines for offline review.

  Co-Authored-By: Name <email>
  ```

## Adding a legal-database tool

Korkut's tools live under `backend/src/korkut/tools/<name>/` and are auto-discovered. A tool is a
self-contained folder:

```
tools/<name>/
  tool.yaml     # name, description, parameters, requires_key
  client.py     # run_tool(**params) -> JSON-serializable dict
  __main__.py   # CLI entry (reuses tools/_base/cli)
  README.md     # what it does, parameters, example
```

The registry (`core/tool_registry.py`) builds the model's function-calling schema from `tool.yaml`
and runs the tool by name. Keep the offered tool surface tight — prefer one consolidated tool with a
`source` enum over many near-identical ones (see `tools/search_decisions/`). For the full pattern,
read **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** and `backend/src/korkut/tools/README.md`.

## Reporting bugs and requesting features

Use the GitHub issue templates. For **security vulnerabilities**, do **not** open a public issue —
follow **[SECURITY.md](SECURITY.md)**.
