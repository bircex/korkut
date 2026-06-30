<p align="center">
  <img src="assets/korkut-logo.svg" alt="Korkut" width="120" height="120" />
</p>

<h1 align="center">Korkut</h1>

<p align="center">
  <a href="https://github.com/recepkizilarslan/korkut/actions/workflows/ci.yml"><img src="https://github.com/recepkizilarslan/korkut/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License: MIT" /></a>
  <img src="https://img.shields.io/badge/python-3.12%2B-blue.svg" alt="Python 3.12+" />
  <img src="https://img.shields.io/badge/version-CalVer-blue.svg" alt="CalVer" />
</p>

Korkut is a **self-hosted, multi-tenant AI assistant for Turkish law offices**. It manages case
files, does live legal research, drafts petitions, and handles correspondence — through a chat web
app that **keeps client data on the office's own machine**. Each office self-registers and Korkut
answers under that office's identity.

## Features

- **Case files (matters)** — clients, hearings, and procedural deadlines, with a unified, proactive
  **agenda** of what's coming up.
- **Legal research** — live search and fetch over Turkish public legal databases (Yargıtay,
  Danıştay, UYAP Emsal, mevzuat, KİK, Rekabet, Sayıştay, GİB, Uyuşmazlık, KVKK, BDDK, insurance
  arbitration).
- **Drafting** — petition/contract drafting in a Canvas editor with DOCX/UDF export.
- **Correspondence** — propose-and-confirm e-mail; two-way Telegram/WhatsApp/e-mail integrations.
- **Privacy by design** — cloud-bound text is redacted first; point Korkut at a local
  OpenAI-compatible model (e.g. LM Studio) and nothing leaves the machine.
- **Secure & multi-tenant** — mandatory TOTP 2FA, strict per-office isolation.

## Quick start

```bash
# macOS / Linux
./scripts/install.sh

# Windows (PowerShell)
./scripts/install.ps1

# …or Docker (no local Python/Node needed)
./scripts/install.sh --docker          # http://localhost:8090
```

Then create a user and start the server — full steps in **[docs/INSTALL.md](docs/INSTALL.md)**.

## Documentation

Start with the **[documentation index](docs/README.md)**:

| Page | What it covers |
|---|---|
| [INSTALL](docs/INSTALL.md) | Native (macOS / Linux / Windows) and Docker installation |
| [DEVELOPMENT](docs/DEVELOPMENT.md) | Dev loop: run, test, lint, build, CLIs, adding a tool |
| [ARCHITECTURE](docs/ARCHITECTURE.md) | Multi-tenancy, the matter domain, and the agentic core |
| [DEPLOYMENT](docs/DEPLOYMENT.md) | Production: Docker + Postgres behind a reverse proxy |
| [GLOSSARY](docs/GLOSSARY.md) | Turkish legal & product terms in English |

## Contributing

Contributions are welcome — see **[CONTRIBUTING.md](CONTRIBUTING.md)** and our
**[Code of Conduct](CODE_OF_CONDUCT.md)**. Report security issues privately via
**[SECURITY.md](SECURITY.md)**.

## License

[MIT](LICENSE) © Recep Kızılarslan
