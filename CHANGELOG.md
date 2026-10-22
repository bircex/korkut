# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Calendar Versioning](https://calver.org/) (`YYYY.MM.PATCH`: the month's first release is
`YYYY.MM.0`, each further one adds to PATCH). Add your change under **Unreleased** in the same pull
request; the Release workflow (`.github/workflows/release.yml`, see
[docs/RELEASING.md](docs/RELEASING.md)) turns it into a dated release.

## [Unreleased]

### Added
- **Self-hosted, single office**: one install per office on its own machine, started with
  `./install.sh` (Windows: `.\install.ps1`). The installer creates the `korkut-admin` account and its
  first password; members join by invitation with a password of their own, with optional two-step
  verification.
- **Choose your AI model** in Ayarlar → Yapay zeka: a local OpenAI-compatible server (LM Studio,
  Ollama) so no text leaves the machine, or a cloud provider. Every call goes through the bundled
  model gateway (llmr), which keeps provider keys sealed and shows usage.
- **Matter-based permissions**: 29 permissions grouped into ready roles (owner, lawyer, intern,
  secretary, accounting, reader) and custom roles; every matter has a team, and members see only
  their team's matters and what hangs off them. Client contact and identity details and finance
  have permissions of their own. Matter scope is also enforced in Postgres (row-level security).
- **Chats live in matters**: a matter's conversations sit in its folder and are shared by its team;
  in a matter chat the assistant works only on that matter. Personal chats stay with their owner.
- **Matter page tabs**: Özet, Sohbetler, Belgeler, Ajanda, Notlar, Müvekkil, Ekip, Mali.
- **Telegram**: an office bot for private chats (`/dosya`, `/dosyalar`, `/kisisel`), groups bound to
  a matter (`/baglan`) that answer only while everyone in the group may see it, and reminders.
- **Access log** (Ayarlar → Erişim kayıtları): client detail and finance views, role and team
  changes and refused requests.
- 27 legal calculators, usable from chat or as forms; decision search (Karar Arama) and saved
  decisions (Kararlarım); OCR for scanned uploads; reminders by Telegram and e-mail.
- **Releases**: CalVer releases with published multi-arch images (`ghcr.io/bircex/korkut*`) and a
  downloadable install bundle; the website moved to GitHub Pages with per-feature documentation.

### Changed
- The cloud SaaS (multiple offices, Google sign-in, sign-up and approval) is gone; Korkut is open
  source software you run yourself.
- Timestamps sent with an offset (e.g. `+03:00`) are stored as the same instant instead of failing.

### Removed
- Google sign-in and the multi-office admin console.

## [2026.7.0] - 2026-07-02

### Added
- **Matter-centric domain**: case files (matters), clients, hearings, and procedural deadlines —
  with office-scoped agent tools, REST endpoints, and a "Dosyalar" UI.
- **Unified agenda**: hearings + pending deadlines in one calendar view, surfaced proactively.
- **`prepare_hearing`** tool: assembles a hearing-prep pack (matter + agenda + linked documents).
- **Document ↔ matter linking** and **correspondence ↔ matter linking**.
- Open-source project setup: MIT license, contribution/security/conduct docs, issue & PR
  templates, install scripts, release pipeline, and security scanning.

### Changed
- **Model-driven intent**: removed the keyword-based intake gate; the model now decides when to
  draft via the tool contract (fixes topical questions being treated as petition requests).
- **Consolidated legal-search tools** from 18 near-identical tools to 3 source-routed tools,
  cutting the per-request token footprint.

[Unreleased]: https://github.com/bircex/korkut/commits/main
