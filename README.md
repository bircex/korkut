# Korkut

An assistant that runs a law firm's daily work (court hearing reminders, petition drafts, legal
research) over **Mattermost**, **keeping the data in the office**. The user
talks to a single identity — **Korkut** — while specialized modules run in parallel under the hood.

> ⚠️ This repo is private. Client data (`data/`), secrets (`.env`), and backups **never**
> go into the repo (see `.gitignore`).

## Documents
- **[PLAN.md](PLAN.md)** — roadmap, principles, phases, technology, scope decisions
- **[MIMARI.md](MIMARI.md)** — technical design: process topology, message flow, core,
  data model, Korkut's internal structure, **tools layer (§12)**, approval flow
- **[KAYNAKLAR.md](KAYNAKLAR.md)** — legal data sources and access (case law/legislation)
- **[GUVENLIK.md](GUVENLIK.md)** — single-gate Mattermost, mobile, 2FA, network hardening
- **[KORPUS.md](KORPUS.md)** — research/petition data strategy: RAG (now) → focused index → broad corpus
- **[RAKIPLER.md](RAKIPLER.md)** — competitor products (preliminary review); detailed research pending

## Architecture overview
- **Interface:** self-hosted Mattermost (single gate, mobile, mandatory 2FA); channels + DMs
- **Korkut:** single identity; specialized modules under the hood — routing, court hearing, petition, research (parallel)
- **Model:** cloud = GPT (with redacted text); local model once the hardware arrives
- **Capabilities:** tools under `tools/`, run from the CLI, configured into Korkut
- **Data:** Postgres + `data/dosyalar/`; all on the server

## Status
Design complete (PLAN/MIMARI/KAYNAKLAR/GUVENLIK). Up next: **Phase 0** (Linux setup).
