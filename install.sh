#!/usr/bin/env bash
# Install or update Korkut on this machine: creates the secrets the stack needs in .env (once),
# then builds and starts it with Docker Compose. Safe to run again: a value already in .env is
# never changed, because the model gateway's key seals every provider key stored in it.
#
#   ./install.sh                         install, or update to this checkout
#   ./install.sh --reset-admin-password  give korkut-admin a new password (if it was lost)
set -euo pipefail
cd "$(dirname "$0")"

reset_admin=false
case "${1:-}" in
  "") ;;
  --reset-admin-password) reset_admin=true ;;
  *) echo "usage: ./install.sh [--reset-admin-password]" >&2; exit 2 ;;
esac

if ! docker compose version >/dev/null 2>&1; then
  echo "install: Docker with Compose v2 is required (https://docs.docker.com/get-docker/)." >&2
  exit 1
fi

random_base64() { head -c "$1" /dev/urandom | base64 | tr -d '\n'; }
random_hex() { head -c "$1" /dev/urandom | od -An -tx1 | tr -d ' \n'; }
# Letters and digits only, so it can be read out and typed.
random_password() { head -c 96 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | cut -c1-20; }

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
# The installation admin (user name korkut-admin) is created with this password on first start.
# Change it in the app afterwards (Ayarlar → Hesap); this value is then only the first one.
new_admin_password=false
if ! has KORKUT_ADMIN_PASSWORD; then
  add KORKUT_ADMIN_PASSWORD "$(random_password)"
  new_admin_password=true
elif $reset_admin; then
  password="$(random_password)"
  grep -v '^KORKUT_ADMIN_PASSWORD=' .env > .env.tmp && mv .env.tmp .env
  add KORKUT_ADMIN_PASSWORD "$password"
  new_admin_password=true
fi
chmod 600 .env

if $reset_admin; then
  # One start with the reset flag sets the admin's password back to the one in .env.
  KORKUT_ADMIN_PASSWORD_RESET=1 docker compose up -d --build
else
  docker compose up -d --build
fi

port="$(grep -E '^KORKUT_HTTP_PORT=' .env | cut -d= -f2 || true)"
echo
echo "Korkut is starting at http://localhost:${port:-8090}"
if $new_admin_password; then
  echo "Sign in with:  korkut-admin / $(grep -E '^KORKUT_ADMIN_PASSWORD=' .env | cut -d= -f2-)"
  echo "Change this password after signing in (Ayarlar → Hesap), then invite your colleagues."
else
  echo "Sign in as korkut-admin. Lost the password? ./install.sh --reset-admin-password"
fi
echo "Keep a copy of .env somewhere safe: it holds the key to the AI provider keys you enter."
