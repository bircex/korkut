#!/usr/bin/env bash
# Install or update Korkut on this machine: creates the secrets the stack needs in .env (once),
# then builds and starts it with Docker Compose. Safe to run again: a value already in .env is
# never changed, because the model gateway's key seals every provider key stored in it.
set -euo pipefail
cd "$(dirname "$0")"

if ! docker compose version >/dev/null 2>&1; then
  echo "install: Docker with Compose v2 is required (https://docs.docker.com/get-docker/)." >&2
  exit 1
fi

random_base64() { head -c "$1" /dev/urandom | base64 | tr -d '\n'; }
random_hex() { head -c "$1" /dev/urandom | od -An -tx1 | tr -d ' \n'; }

touch .env
has() { grep -Eq "^$1=.+" .env; }
add() {
  [ -s .env ] && [ "$(tail -c1 .env)" != "" ] && echo >> .env
  echo "$1=$2" >> .env
  echo "install: generated $1"
}

# The model gateway (llmr) seals the provider keys you enter in Korkut's settings with this key.
# Keep a copy of .env: without this value those keys cannot be read back.
has LLMR_MASTER_KEY || add LLMR_MASTER_KEY "$(random_base64 32)"
# Korkut presents this token to the gateway; nothing else on the machine can call it.
has LLMR_TOKEN || add LLMR_TOKEN "$(random_hex 32)"
chmod 600 .env

docker compose up -d --build

port="$(grep -E '^KORKUT_HTTP_PORT=' .env | cut -d= -f2 || true)"
echo
echo "Korkut is starting at http://localhost:${port:-8090}"
echo "The first visit asks for your office's name and creates the first account."
echo "Keep a copy of .env somewhere safe: it holds the key to the AI provider keys you enter."
