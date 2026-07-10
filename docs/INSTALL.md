# Installation

Korkut runs in two ways: a **native** install (a Python virtualenv + the built SPA) or **Docker**.
For day-to-day development, see [DEVELOPMENT.md](DEVELOPMENT.md). For production, see
[DEPLOYMENT.md](DEPLOYMENT.md).

## Prerequisites

- **Python 3.12+**
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

The script creates `.venv`, installs the backend, and writes a `.env` with a generated
`KORKUT_SECRET` (defaulting to a local SQLite database under `data/`). The web UI is a prebuilt SPA
committed under `backend/src/korkut/api/static` (its source lives in the separate **korkut-ui**
repo), so no Node build is needed here.

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

No local Python or Node needed. The install script has a Docker mode that builds and starts the
all-in-one stack (web app + scheduled-send worker + integrations poller):

```bash
# macOS / Linux
./scripts/install.sh --docker         # http://localhost:8090
# Windows (PowerShell)
./scripts/install.ps1 -Docker
```

It writes `.env` (with a generated `KORKUT_SECRET`) and runs `docker compose up -d --build`. Then
create the first user inside the container:

```bash
docker compose -f deploy/docker-compose.yml exec korkut \
  python -m korkut.api.adduser avukat --name "Av. Name"
```

Equivalently, without the script:

```bash
docker compose -f deploy/docker-compose.yml up -d --build      # http://localhost:8090
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
cp .env.example .env   # then set KORKUT_SECRET and unset/SQLite KORKUT_DB_URL
```

The web UI ships prebuilt under `backend/src/korkut/api/static`. To rebuild it from source, clone the
**korkut-ui** repo as a sibling of this repo and run `make ui`.

Generate a session secret with:

```bash
python -c "import secrets; print(secrets.token_hex(32))"
```
