#!/usr/bin/env bash
# One-command load run:
#   1. bring up the load stack (mock LLM + agent pointed at it + real Tesseract OCR),
#   2. generate fixtures, 3. seed N tenants, 4. run k6 on the stack network, 5. save the summary.
#
# Usage:
#   tests/load/run.sh                 # full run (defaults below)
#   tests/load/run.sh --smoke         # tiny run to validate wiring (2 tenants, 5 VUs, 20s)
#   N_TENANTS=10 VUS=200 DURATION=3m tests/load/run.sh
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"                 # korkut-backend
cd "$ROOT"
COMPOSE="docker compose -f docker-compose.yml -f tests/load/docker-compose.load.yml"
# Host entry = the gateway mapped to :8091 by the load overlay (the SPA proxy on :8090 doesn't
# forward every /api path). Exported so seed.sh inherits it.
export HOST_BASE="${HOST_BASE:-http://localhost:8091}"

if [ "${1:-}" = "--smoke" ]; then
  export N_TENANTS="${N_TENANTS:-2}" VUS="${VUS:-5}" DURATION="${DURATION:-20s}" RAMP="${RAMP:-5s}"
fi
export N_TENANTS="${N_TENANTS:-10}" VUS="${VUS:-200}" DURATION="${DURATION:-2m}" RAMP="${RAMP:-30s}"

echo "==> [1/5] Building + starting the load stack (mock-llm, agent→mock, real OCR, api rate-limit lifted)"
$COMPOSE up -d --build mock-llm ocr agent api gateway postgres pgbouncer

echo "==> [2/5] Generating fixtures"
python3 "$HERE/fixtures/make-fixtures.py"

echo "==> Waiting for the API to be ready (login endpoint reachable) ..."
for i in $(seq 1 60); do
  code=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$HOST_BASE/api/auth/login" \
    -H "Content-Type: application/json" -d '{"email":"x@x","password":"x"}' 2>/dev/null || echo 000)
  # 401/400/422 = api up and answering; 000/502/503 = still starting.
  case "$code" in 401|400|422) echo "  api ready (HTTP $code)"; break;; esac
  [ "$i" = "60" ] && { echo "  API did not become ready in time"; exit 1; }
  sleep 1
done

echo "==> [3/5] Seeding $N_TENANTS tenants"
N_TENANTS="$N_TENANTS" bash "$HERE/seed.sh"

mkdir -p "$HERE/results"
TS="$(date +%Y%m%d-%H%M%S)"
SUMMARY="/load/results/summary-${TS}.json"

echo "==> [5/5] Running k6  (VUS=$VUS DURATION=$DURATION RAMP=$RAMP)"
$COMPOSE run --rm \
  -e VUS="$VUS" -e DURATION="$DURATION" -e RAMP="$RAMP" \
  -e W_CHAT="${W_CHAT:-}" -e W_UPLOAD="${W_UPLOAD:-}" -e W_OCR="${W_OCR:-}" \
  -e THINK_MAX="${THINK_MAX:-}" -e DEBUG_ERR="${DEBUG_ERR:-}" \
  k6 run /load/k6/rag-load.js --summary-export="$SUMMARY"

echo
echo "Benchmark summary saved to tests/load/results/summary-${TS}.json"
