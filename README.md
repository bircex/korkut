<p align="center">
  <img src="assets/korkut-logo.svg" alt="Korkut" width="120" height="120" />
</p>

<h1 align="center">Korkut</h1>

<p align="center">
  <a href="https://github.com/recepkizilarslan/korkut-backend/actions/workflows/dotnet-migration-ci.yml"><img src="https://github.com/recepkizilarslan/korkut-backend/actions/workflows/dotnet-migration-ci.yml/badge.svg" alt="CI" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License: MIT" /></a>
  <img src="https://img.shields.io/badge/.NET-10-blueviolet.svg" alt=".NET 10" />
  <img src="https://img.shields.io/badge/version-CalVer-blue.svg" alt="CalVer" />
</p>

Korkut is a **self-hosted, single-tenant AI assistant for a Turkish law office**. It manages case
files, does live legal research, drafts petitions, and handles correspondence — through a chat web
app that **keeps client data on the office's own machine**. Korkut answers under that office's
identity.

## Features

- **Case files (matters)** — clients, hearings, and procedural deadlines, with a unified, proactive
  **agenda** of what's coming up.
- **Legal research** — live search and fetch over Turkish public legal databases (Yargıtay,
  Danıştay, UYAP Emsal, mevzuat, KİK, Rekabet, Sayıştay, GİB, Uyuşmazlık, KVKK, BDDK, insurance
  arbitration).
- **Drafting** — petition/contract drafting in a Canvas editor with DOCX/UDF export.
- **Correspondence** — propose-and-confirm e-mail; two-way Telegram/WhatsApp/e-mail integrations.
- **Local-first** — point Korkut at a local OpenAI-compatible model (e.g. LM Studio,
  Ollama) and nothing leaves the machine. If you choose a cloud model, outbound text is
  passed through **best-effort** redaction first — a mitigation, **not a guarantee**
  (see [DISCLAIMER.md](DISCLAIMER.md)).
- **Self-hosted, Google sign-in** — Google OIDC only, no passwords; each install serves one office. Overall
  security depends on how *you* deploy it (TLS, backups, updates); provided as-is, with
  no warranty.

## Quick start

The runtime is a **.NET 10** solution at the repository root (`Korkut.slnx`, `src/`,
`tests/`; the Python runtime was retired after the .NET migration).

```bash
make build     # dotnet build Korkut.slnx
make test      # full test suite (needs Docker for Postgres/MinIO integration tests)
make run       # start the API on :8080  (dotnet run --project src/Korkut.Api)
make migrate   # apply EF Core migrations via the migrator
```

Full steps in **[docs/INSTALL.md](docs/INSTALL.md)**.

## Layout

Everything lives in this repository: the .NET hosts and modules under `src/`, and the React
app under `ui/`, which is built into the gateway image and served from the same origin as
the API.

## Documentation

Start with the **[documentation index](docs/README.md)**:

| Page | What it covers |
|---|---|
| [INSTALL](docs/INSTALL.md) | Native (macOS / Linux / Windows) and Docker installation |
| [DEVELOPMENT](docs/DEVELOPMENT.md) | Dev loop: run, test, lint, build, CLIs, adding a tool |
| [ARCHITECTURE](docs/ARCHITECTURE.md) | The single-office model, the matter domain, and the agentic core |
| [DEPLOYMENT](docs/DEPLOYMENT.md) | Production: Docker + Postgres behind a reverse proxy |
| [GLOSSARY](docs/GLOSSARY.md) | Turkish legal & product terms in English |

## Contributing

Contributions are welcome — see **[CONTRIBUTING.md](CONTRIBUTING.md)** and our
**[Code of Conduct](CODE_OF_CONDUCT.md)**. Report security issues privately via
**[SECURITY.md](SECURITY.md)**.

## Legal & disclaimer

Korkut is **free, open-source software that you self-host** — not a service, a SaaS
platform, or legal advice. It is provided **"AS IS"**, without warranty of any kind
(see [LICENSE](LICENSE)). Full text: **[DISCLAIMER.md](DISCLAIMER.md)**.

- **Not legal advice.** Korkut does not provide legal counsel or representation. Every
  output — research, summaries, petition/contract drafts — is a **draft for a qualified
  lawyer to review**. AI output can be wrong, incomplete, or fabricated (e.g. invented
  case/article citations); **verifying it is the user's responsibility.**
- **Your data stays on your machine.** By default all data (chats, documents, accounts)
  lives only on **your own server**. The project ships **no telemetry or analytics** —
  the developers receive nothing and cannot access your deployment.
- **You are the data controller.** Whoever installs and operates Korkut is the "data
  controller" (veri sorumlusu) under Türkiye's KVKK (and GDPR where applicable) for the
  personal data they process, and bears all related obligations (registration, notice,
  consent, security, retention). The developers are not a controller or processor of
  your data.
- **Local by default; cloud models are optional and your responsibility.** Point Korkut
  at a **local** OpenAI-compatible model and nothing leaves the machine. If *you* choose
  to connect it to an **external/cloud** model (e.g. OpenAI, Google, any third-party
  API), your inputs are sent to that provider — possibly **abroad**. Meeting cross-border
  transfer requirements (KVKK Art. 9 / GDPR) and assessing that provider's data-retention
  and training policies is **solely your responsibility.**
- **Optional integrations.** Enabling WhatsApp, Telegram, or e-mail integrations sends the
  relevant data to those third parties, under their own terms. Lawfulness and any required
  notice/consent are **your responsibility.**
- **Limitation of liability.** To the maximum extent permitted by law, the developers are
  not liable for any direct or indirect damages arising from use of the software. This
  does not apply to gross negligence or willful misconduct.

> This is a general notice, **not legal advice.** Before using Korkut with real client
> data, have a data-protection/IT lawyer review your deployment.

## License

[MIT](LICENSE) © Recep Kızılarslan
