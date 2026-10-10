# Korkut Documentation

> **Runtime note:** Korkut runs on **.NET 10** at the repository root (`Korkut.slnx`, `src/`), with the React app in `ui/`, installed locally with Docker Compose. Commands below that reference `python -m korkut`, `backend/…` or `deploy/…` are from the retired Python runtime.

The root [README](../README.md) is the project overview; these pages go deeper. Start here.

| Page | What it covers |
|---|---|
| [INSTALL.md](INSTALL.md) | Installing and running Korkut locally with Docker Compose. |
| [DEVELOPMENT.md](DEVELOPMENT.md) | Day-to-day dev loop: run, test, lint, build, CLIs, adding a tool. |
| [ARCHITECTURE.md](ARCHITECTURE.md) | How Korkut works: the single-office model, the matter domain, the agentic core. |
| [SOURCES.md](SOURCES.md) | The Turkish legal data sources behind the research tools, and how they're accessed. |
| [GLOSSARY.md](GLOSSARY.md) | Turkish legal & product terms, explained in English. |

See also:

- [CONTRIBUTING.md](../CONTRIBUTING.md) — how to contribute and the quality gates.
- [SECURITY.md](../SECURITY.md) — reporting vulnerabilities.
- [CHANGELOG.md](../CHANGELOG.md) — notable changes per release.
- `backend/src/korkut/tools/README.md` — the legal-database tool catalog.
