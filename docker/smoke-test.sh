#!/usr/bin/env bash
# KDM-009 — container structure / health / read-only-fs / shutdown smoke test.
#
# Builds the three hardened images and asserts: non-root user, one-shot migrator
# exits cleanly, the API serves health on a READ-ONLY root filesystem, and the API
# shuts down gracefully (exit 0) on SIGTERM. No secrets are baked in; runtime
# config is supplied via the environment.
#
# Usage: docker/smoke-test.sh   (run from the repository root)
set -uo pipefail

TAG="${TAG:-kdm009smoke}"
PORT="${PORT:-18080}"
pass=0; fail=0
check() { if [ "$1" = "0" ]; then echo "  ok   $2"; pass=$((pass+1)); else echo "  FAIL $2"; fail=$((fail+1)); fi; }

# Every docker run here is expected to finish on its own. When one does not — the migrator used to
# hang forever on arm64 instead of exiting — an unbounded run turns a failing assertion into a job
# that never ends. Bound them so a hang is reported as a failure like any other.
run_bounded() { # run_bounded <seconds> <docker run args...>
  local limit="$1"; shift
  docker run "$@" &
  local pid=$!
  local waited=0
  while kill -0 "$pid" 2>/dev/null && [ "$waited" -lt "$limit" ]; do
    sleep 1; waited=$((waited+1))
  done
  if kill -0 "$pid" 2>/dev/null; then
    kill -9 "$pid" 2>/dev/null
    wait "$pid" 2>/dev/null
    return 124
  fi
  wait "$pid"
}

cleanup() { docker rm -f korkut-api-smoke >/dev/null 2>&1 || true; }
trap cleanup EXIT

echo "== build =="
docker build -q -f docker/Dockerfile.api      -t korkut-api:$TAG .      >/dev/null; check $? "api image builds"
docker build -q -f docker/Dockerfile.worker   -t korkut-worker:$TAG .   >/dev/null; check $? "worker image builds"
docker build -q -f docker/Dockerfile.migrator -t korkut-migrator:$TAG . >/dev/null; check $? "migrator image builds"

echo "== hardening =="
for img in korkut-api korkut-worker korkut-migrator; do
  u=$(docker image inspect $img:$TAG --format '{{.Config.User}}')
  [ "$u" = "app" ]; check $? "$img runs as non-root user ('$u')"
done

echo "== migrator one-shot =="
# The migrator applies schema only under the elevated migrator role; without a DB URL
# it must FAIL FAST (never migrate blindly). This DB-less smoke asserts that safe
# one-shot behaviour on a read-only fs; real migrate/rollback is covered by KDM-035.
out=$(run_bounded 30 --rm --read-only --tmpfs /tmp korkut-migrator:$TAG info 2>&1); rc=$?
{ [ "$rc" -ne 0 ] && [ "$rc" -ne 124 ]; }; check $? "migrator fails fast (exit $rc) with no KORKUT_ADMIN_DB_URL"
echo "$out" | grep -q "KORKUT_ADMIN_DB_URL is required"; check $? "migrator prints the required-var message"

echo "== api on READ-ONLY filesystem =="
# Runtime secrets are supplied via the environment (never baked into the image).
API_SECRET="$(openssl rand -hex 32)"       # >= 32 bytes for HS256 access tokens
API_TOTP_KEY="$(openssl rand -base64 32)"  # base64 of 32 bytes for TOTP envelope encryption
docker run -d --name korkut-api-smoke \
  --read-only --tmpfs /tmp \
  --cap-drop ALL --security-opt no-new-privileges \
  -e ASPNETCORE_URLS="http://+:8080" \
  -e KORKUT_SECRET="$API_SECRET" \
  -e KORKUT_TOTP_KEY="$API_TOTP_KEY" \
  -e KORKUT_DB_URL="Host=127.0.0.1;Port=5432;Database=korkut;Username=korkut_app;Password=none" \
  -p $PORT:8080 korkut-api:$TAG >/dev/null; check $? "api starts read-only, cap-drop ALL"
# The app fail-fasts without KORKUT_DB_URL (it is a required least-privileged app-role
# connection). Health/config never open the connection (it is lazy), so a dummy value is
# enough to boot and prove liveness/readiness/config on the read-only rootfs.

# Wait for readiness.
ready=1
for _ in $(seq 1 30); do
  code=$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:$PORT/health/live" 2>/dev/null || true)
  [ "$code" = "200" ] && { ready=0; break; }
  sleep 1
done
check $ready "GET /health/live -> 200 (read-only rootfs)"
code=$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:$PORT/health/ready" 2>/dev/null)
[ "$code" = "200" ]; check $? "GET /health/ready -> 200"
body=$(curl -s "http://localhost:$PORT/api/v1/config" 2>/dev/null)
echo "$body" | grep -q "demo_enabled"; check $? "GET /api/v1/config serves snake_case contract"
type=$(curl -s -o /dev/null -w '%{content_type}' "http://localhost:$PORT/sohbet/1" 2>/dev/null)
echo "$type" | grep -q "text/html"; check $? "a client-side route serves the app shell"

echo "== graceful shutdown on SIGTERM =="
start=$(date +%s)
docker stop -t 15 korkut-api-smoke >/dev/null 2>&1
rc=$(docker inspect korkut-api-smoke --format '{{.State.ExitCode}}' 2>/dev/null)
elapsed=$(( $(date +%s) - start ))
{ [ "$rc" = "0" ] && [ "$elapsed" -lt 15 ]; }; check $? "SIGTERM -> clean exit (code=$rc, ${elapsed}s < grace)"

echo
echo "passed=$pass failed=$fail"
[ "$fail" -eq 0 ]
