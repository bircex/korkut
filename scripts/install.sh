#!/usr/bin/env bash
#
# Korkut native install for macOS / Linux. Idempotent — safe to re-run.
# Creates a Python venv + installs the backend, builds the frontend SPA, and writes .env.
#
# Usage:  ./scripts/install.sh
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

info() { printf '\033[1;34m==>\033[0m %s\n' "$1"; }
warn() { printf '\033[1;33m! \033[0m %s\n' "$1"; }
die()  { printf '\033[1;31mError:\033[0m %s\n' "$1" >&2; exit 1; }

# --- preflight ---
command -v python3 >/dev/null 2>&1 || die "python3 not found. Install Python 3.12 or newer."
PYV="$(python3 -c 'import sys; print("%d.%d" % sys.version_info[:2])')"
python3 -c 'import sys; raise SystemExit(0 if sys.version_info[:2] >= (3, 12) else 1)' \
  || die "Python 3.12+ required (found $PYV)."
command -v node >/dev/null 2>&1 || die "node not found. Install Node 20 or newer."
NODEV="$(node -p 'process.versions.node.split(".")[0]')"
[ "$NODEV" -ge 20 ] || die "Node 20+ required (found $(node -v))."
command -v npm >/dev/null 2>&1 || die "npm not found. Install Node.js (includes npm)."
info "Python $PYV, Node $(node -v) — OK"

# --- backend venv ---
if [ ! -d .venv ]; then
  info "Creating virtual environment (.venv)"
  python3 -m venv .venv
fi
info "Installing the backend (editable, app + dev extras)"
./.venv/bin/python -m pip install --upgrade pip >/dev/null
./.venv/bin/python -m pip install -e "backend[app,dev]"

# --- frontend build ---
info "Installing and building the frontend SPA"
( cd frontend && npm ci && npm run build )

# --- environment file ---
if [ ! -f .env ]; then
  info "Creating .env from .env.example"
  cp .env.example .env
  SECRET="$(./.venv/bin/python -c 'import secrets; print(secrets.token_hex(32))')"
  # Fill the session secret; default a native install to local SQLite (comment the Postgres URL).
  sed -i.bak "s/^KORKUT_SECRET=.*/KORKUT_SECRET=${SECRET}/" .env
  sed -i.bak "s|^KORKUT_DB_URL=postgresql|# KORKUT_DB_URL=postgresql|" .env
  rm -f .env.bak
  info "Generated KORKUT_SECRET; using SQLite under data/ for the local database"
else
  warn ".env already exists — leaving it untouched"
fi

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
