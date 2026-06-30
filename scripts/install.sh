#!/usr/bin/env bash
#
# Korkut installer for macOS / Linux. Idempotent — safe to re-run.
#
#   ./scripts/install.sh            Native install (Python venv + built SPA)
#   ./scripts/install.sh --docker   Docker install (build + start the compose stack)
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

info() { printf '\033[1;34m==>\033[0m %s\n' "$1"; }
warn() { printf '\033[1;33m! \033[0m %s\n' "$1"; }
die()  { printf '\033[1;31mError:\033[0m %s\n' "$1" >&2; exit 1; }

# Generate a 64-hex session secret without assuming any particular runtime is present.
gen_secret() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -hex 32
  elif command -v python3 >/dev/null 2>&1; then
    python3 -c 'import secrets; print(secrets.token_hex(32))'
  else
    head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n'
  fi
}

# Create .env from the template, fill KORKUT_SECRET, and default the local DB to SQLite.
write_env() {
  if [ -f .env ]; then
    warn ".env already exists — leaving it untouched"
    return
  fi
  info "Creating .env from .env.example"
  cp .env.example .env
  local secret
  secret="$(gen_secret)"
  sed -i.bak "s/^KORKUT_SECRET=.*/KORKUT_SECRET=${secret}/" .env
  sed -i.bak "s|^KORKUT_DB_URL=postgresql|# KORKUT_DB_URL=postgresql|" .env
  rm -f .env.bak
  info "Generated KORKUT_SECRET; using SQLite under data/ for the local database"
}

# ---------------------------------------------------------------------------
# Docker install
# ---------------------------------------------------------------------------
if [ "${1:-}" = "--docker" ]; then
  command -v docker >/dev/null 2>&1 || die "docker not found. Install Docker Desktop / Engine."
  docker compose version >/dev/null 2>&1 || die "Docker Compose v2 required (docker compose ...)."
  info "Docker $(docker --version | awk '{print $3}' | tr -d ,) — OK"
  write_env
  info "Building and starting the stack (this can take a few minutes the first time)"
  docker compose -f deploy/docker-compose.yml up -d --build
  cat <<'NEXT'

Setup complete. Next steps:

  1) Add a model key to .env (GROQ_API_KEY or ANTHROPIC_API_KEY), or point Korkut at a local
     OpenAI-compatible endpoint (e.g. LM Studio) from Settings -> Model in the app.

  2) Create your first user (you will be prompted for a password):
       docker compose -f deploy/docker-compose.yml exec korkut \
         python -m korkut.api.adduser avukat --name "Av. Name"

  3) Open http://localhost:8090

  Logs:  docker compose -f deploy/docker-compose.yml logs -f korkut
  Stop:  docker compose -f deploy/docker-compose.yml down

NEXT
  exit 0
fi

# ---------------------------------------------------------------------------
# Native install
# ---------------------------------------------------------------------------
command -v python3 >/dev/null 2>&1 || die "python3 not found. Install Python 3.12 or newer."
PYV="$(python3 -c 'import sys; print("%d.%d" % sys.version_info[:2])')"
python3 -c 'import sys; raise SystemExit(0 if sys.version_info[:2] >= (3, 12) else 1)' \
  || die "Python 3.12+ required (found $PYV)."
command -v node >/dev/null 2>&1 || die "node not found. Install Node 20 or newer."
NODEV="$(node -p 'process.versions.node.split(".")[0]')"
[ "$NODEV" -ge 20 ] || die "Node 20+ required (found $(node -v))."
command -v npm >/dev/null 2>&1 || die "npm not found. Install Node.js (includes npm)."
info "Python $PYV, Node $(node -v) — OK"

if [ ! -d .venv ]; then
  info "Creating virtual environment (.venv)"
  python3 -m venv .venv
fi
info "Installing the backend (editable, app + dev extras)"
./.venv/bin/python -m pip install --upgrade pip >/dev/null
./.venv/bin/python -m pip install -e "backend[app,dev]"

info "Installing and building the frontend SPA"
( cd frontend && npm ci && npm run build )

write_env

cat <<'NEXT'

Setup complete. Next steps:

  1) Add a model key to .env (GROQ_API_KEY or ANTHROPIC_API_KEY), or point Korkut at a local
     OpenAI-compatible endpoint (e.g. LM Studio) from Settings -> Model in the app.

  2) Create your first user (you will be prompted for a password):
       cd backend && ../.venv/bin/python -m korkut.api.adduser avukat --name "Av. Name"

  3) Start the server:
       cd backend && ../.venv/bin/python -m korkut.api
     Then open http://localhost:8080

NEXT
