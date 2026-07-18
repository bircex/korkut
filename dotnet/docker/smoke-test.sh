#!/usr/bin/env bash
# KDM-009 — container structure / health / read-only-fs / shutdown smoke test.
#
# Builds the three hardened images and asserts: non-root user, one-shot migrator
# exits cleanly, the API serves health on a READ-ONLY root filesystem, and the API
# shuts down gracefully (exit 0) on SIGTERM. No secrets are baked in; runtime
# config is supplied via the environment.
#
# Usage: docker/smoke-test.sh   (run from the dotnet/ directory)
set -uo pipefail

TAG="${TAG:-kdm009smoke}"
PORT="${PORT:-18080}"
pass=0; fail=0
check() { if [ "$1" = "0" ]; then echo "  ok   $2"; pass=$((pass+1)); else echo "  FAIL $2"; fail=$((fail+1)); fi; }

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
out=$(docker run --rm --read-only --tmpfs /tmp korkut-migrator:$TAG info 2>&1); rc=$?
check $rc "migrator runs one-shot and exits 0 on read-only fs"
echo "$out" | grep -q "least-privileged"; check $? "migrator prints its status"

echo "== api on READ-ONLY filesystem =="
docker run -d --name korkut-api-smoke \
  --read-only --tmpfs /tmp \
  --cap-drop ALL --security-opt no-new-privileges \
  -e ASPNETCORE_URLS="http://+:8080" \
  -p $PORT:8080 korkut-api:$TAG >/dev/null; check $? "api starts read-only, cap-drop ALL"

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

echo "== graceful shutdown on SIGTERM =="
start=$(date +%s)
docker stop -t 15 korkut-api-smoke >/dev/null 2>&1
rc=$(docker inspect korkut-api-smoke --format '{{.State.ExitCode}}' 2>/dev/null)
elapsed=$(( $(date +%s) - start ))
{ [ "$rc" = "0" ] && [ "$elapsed" -lt 15 ]; }; check $? "SIGTERM -> clean exit (code=$rc, ${elapsed}s < grace)"

echo
echo "passed=$pass failed=$fail"
[ "$fail" -eq 0 ]
