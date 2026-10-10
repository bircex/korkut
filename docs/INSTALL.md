# Installation

Korkut runs locally with Docker Compose: one office, on its own machine.

## Prerequisites

- Docker with Compose v2.

## Install

From the repository root:

```bash
docker compose up -d --build
```

Then open <http://localhost:8090>. No `.env` is needed: every setting has a local default, and the
signing and 2FA keys are generated on first start.

To change a setting (the port, a model key, Google sign-in, SMTP), copy `.env.example` to `.env`,
uncomment the line and run `docker compose up -d` again.

## Where your data lives

| Volume | What |
|---|---|
| `pgdata` | The database |
| `objects` | Uploaded documents |
| `secrets` | The generated `KORKUT_SECRET` and `KORKUT_TOTP_KEY` |

All three survive rebuilds and upgrades. Back them up together. `docker compose down` stops
Korkut; `docker compose down -v` also deletes all data.

Korkut listens on `127.0.0.1` only. Set `KORKUT_BIND_ADDRESS=0.0.0.0` to reach it from other
machines on your network.

## Not there yet

Creating the office's first account from the browser, and choosing a model from the app's
settings, are in progress; see [OPEN_SOURCE_PLAN.md](OPEN_SOURCE_PLAN.md).
