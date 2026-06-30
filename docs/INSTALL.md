# Installation

Korkut runs in two ways: a **native** install (a Python virtualenv + the built SPA) or **Docker**.
For day-to-day development, see [DEVELOPMENT.md](DEVELOPMENT.md). For production, see
[DEPLOYMENT.md](DEPLOYMENT.md).

## Prerequisites

- **Python 3.12+**
- **Node 20+** (with npm) — to build the web UI
- A model endpoint: a cloud key (`GROQ_API_KEY` or `ANTHROPIC_API_KEY`) **or** a local
  OpenAI-compatible server such as [LM Studio](https://lmstudio.ai) or Ollama.
- *(Docker path only)* Docker with Compose v2.

## Native install (recommended for a single machine)

Run the install script for your OS from the repository root. It is idempotent — safe to re-run.

```bash
# macOS / Linux
./scripts/install.sh

# Windows (PowerShell)
./scripts/install.ps1
```

The script creates `.venv`, installs the backend, builds the frontend SPA into the backend's static
directory, and writes a `.env` with a generated `KORKUT_SECRET` (defaulting to a local SQLite
database under `data/`).

Then:

1. **Add a model.** Put a key in `.env` (`GROQ_API_KEY` or `ANTHROPIC_API_KEY`), or after first login
   point Korkut at a local endpoint from **Settings → Model** (e.g. LM Studio at
   `http://localhost:1234/v1`).
2. **Create the first user** (you'll be prompted for a password):
   ```bash
   cd backend && ../.venv/bin/python -m korkut.api.adduser avukat --name "Av. Name"
   ```
3. **Start the server:**
   ```bash
   cd backend && ../.venv/bin/python -m korkut.api
   ```
   Open <http://localhost:8080>. First login enrols TOTP 2FA — scan the QR and save the recovery
   codes.

## Docker

The all-in-one local trial (web app + scheduled-send worker + integrations poller) builds from
source:

```bash
docker compose -f deploy/docker-compose.yml up -d --build      # http://localhost:8090

# create the first user inside the container
docker compose -f deploy/docker-compose.yml exec korkut \
  python -m korkut.api.adduser avukat --name "Av. Name"
```

The SQLite database lives in `./data` and survives rebuilds. To stop: `docker compose -f
deploy/docker-compose.yml down` (add `-v` to also wipe the database).

For a production stack (published image + Postgres behind a reverse proxy), use
`deploy/docker-compose.prod.yml` — see [DEPLOYMENT.md](DEPLOYMENT.md).

## Manual native install

If you'd rather not use the script:

```bash
python3 -m venv .venv
./.venv/bin/python -m pip install -e "backend[app,dev]"
( cd frontend && npm ci && npm run build )
cp .env.example .env   # then set KORKUT_SECRET and unset/SQLite KORKUT_DB_URL
```

Generate a session secret with:

```bash
python -c "import secrets; print(secrets.token_hex(32))"
```
