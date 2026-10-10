# Installation

Korkut is installed and run locally with Docker Compose: one office, on its own machine.

## Prerequisites

- Docker with Compose v2.

## Install

From the repository root:

```bash
cp .env.example .env
docker compose up -d --build
```

Then open <http://localhost:8090> (set `KORKUT_HTTP_PORT` in `.env` to use another port).

Uploaded documents live in the `objects` volume and the database in `pgdata`; both survive
rebuilds. `docker compose down` stops Korkut; `docker compose down -v` also deletes all data.

The required settings in `.env` and the first-run setup are being simplified so that
`docker compose up -d` works with no configuration; see [OPEN_SOURCE_PLAN.md](OPEN_SOURCE_PLAN.md).
