#!/usr/bin/env bash
# Provision N load-test tenants (office + owner) and enable them (the "admin approval" the product
# does via a platform admin). Writes tests/load/tenants.json for the k6 script. Idempotent: an
# already-registered tenant is simply re-enabled. Run from anywhere; paths are resolved here.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"                 # korkut-backend
N="${N_TENANTS:-10}"
HOST_BASE="${HOST_BASE:-http://localhost:8090}"   # host-side entry (ui → gateway)
PASSWORD="${LOAD_PASSWORD:-LoadTest12345!}"
OUT="$HERE/tenants.json"

echo "Seeding $N tenants via $HOST_BASE ..."

emails=()
for i in $(seq 1 "$N"); do
  email="load${i}@korkut.local"
  office="Load Bürosu ${i}"
  code=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$HOST_BASE/api/auth/register" \
    -H "Content-Type: application/json" \
    -d "{\"full_name\":\"Load User ${i}\",\"email\":\"${email}\",\"password\":\"${PASSWORD}\",\"office_name\":\"${office}\"}")
  # 200 = created (pending); 409/422 = already exists → still enable it below.
  echo "  [$i] register ${email} -> HTTP ${code}"
  emails+=("$email")
done

echo "Enabling offices (admin approval) ..."
docker compose -f "$ROOT/docker-compose.yml" exec -T postgres \
  psql -U postgres -d korkut -c \
  "update korkut.offices set disabled=false where id in (select office_id from korkut.users where email like 'load%@korkut.local');" >/dev/null

# Emit tenants.json for k6.
{
  echo "["
  for idx in "${!emails[@]}"; do
    sep=","; [ "$idx" -eq $(( ${#emails[@]} - 1 )) ] && sep=""
    printf '  {"email":"%s","password":"%s"}%s\n' "${emails[$idx]}" "$PASSWORD" "$sep"
  done
  echo "]"
} > "$OUT"

echo "Wrote $OUT (${#emails[@]} tenants)."
