# Load baseline (recorded benchmark)

Raw per-run summaries live under `results/` (git-ignored). This file is the human-readable record of
reference runs — use it as the regression baseline.

Environment: single machine (Docker Desktop, ~14 cores), mock LLM (`MOCK_LATENCY_MS=300`), real
Tesseract OCR, `KORKUT_OFFICE_CONCURRENCY=4`, pgbouncer `DEFAULT_POOL_SIZE=50`.

## Run A — balanced mix (reference baseline)

`N_TENANTS=10 VUS=200 DURATION=30s RAMP=15s` · weights chat 77 / upload 20 / OCR 3 · 2026-07-31

| Metric | Value |
|---|---|
| Total iterations | 3621 (**0 interrupted**) |
| Error rate | **0.00%** (op_errors 0/3621, http_req_failed 0/3831) |
| Throughput | ~61 req/s, ~58 iterations/s |
| RAG chat | 2826 ok · **45/s** · p95 **2.54s** · med 2.09s |
| Upload | 688 ok · 11/s · p95 2.54s |
| OCR (real Tesseract) | 107 ok · p95 **341ms** · med 266ms |

Takeaway: the chat/upload pipeline scales to 200 VUs at **0% error**. The p95 ~2.5s is driven by the
per-office concurrency cap (4) — 20 VUs per office, 4 concurrent model calls → queueing (expected,
healthy).

## Run B — OCR stress (bottleneck diagnosis)

`N_TENANTS=10 VUS=200 DURATION=30s` · weights chat 70 / upload 20 / **OCR 10**

| Operation | Result |
|---|---|
| chat | 1383 ok, p95 1.55s, ~0% error |
| upload | 429 ok, ~0% error |
| **OCR** | 10 of 46 ok, the rest **60s timeout** |

Takeaway: real Tesseract is **CPU-bound**; a single OCR container hits **~1400% CPU** (14 cores) under
load and saturates at ~10-20 concurrent requests, timing out beyond that. **Recommendation:** scale
the OCR host **horizontally (replicas) or add a queue** for heavy OCR; otherwise keep `W_OCR` low
(Run A).

## Run C — max throughput (DB-mutation tools, no think-time)

`VUS=400 THINK_MAX=0 LOAD_OFFICE_CONCURRENCY=0 MOCK_LATENCY_MS=0` · chat 90 / upload 10 / OCR 0 ·
each chat = tool loop with a **DB write** tool (`create_client` / `create_matter` → INSERT + RLS) then
the answer. Config knobs: `LOAD_OFFICE_CONCURRENCY` (governor), `THINK_MAX` (client think-time),
`AGENT_URL` + `NO_CONN_REUSE` (spread across scaled agent replicas).

| Metric | Value |
|---|---|
| Throughput | **~300–435 req/s** (run-to-run variance), **0% errors** |
| RAG chat (DB write) | p95 ~2–3s under saturation |

**Where the ceiling is — the important finding:** it is NOT our tenant/connection layer. Under load
Postgres sits at ~1 core (~100%) and pgbouncer ~45% — clear headroom; the phased short-scope turns
never pin the pool. The limiter is the **single-process mock LLM** (Python, one call per turn ×2 =
GIL-bound) plus the per-turn agent cost. Scaling agent replicas (`--scale agent=4`, `AGENT_URL`,
`NO_CONN_REUSE`) did **not** raise throughput, because the bottleneck is the shared mock and per-turn
work, not a single agent.

**Re: the "1000 req/s" target.** Not reached for *full agentic turns* on a single machine — a full
turn is heavy (2 LLM round-trips + a DB-mutation tool + several tenant-scoped transactions), and the
test's own mock LLM is the artificial ceiling. Reaching 1000+ req/s of full turns needs a
faster/replicated mock (or a real LLM fleet) and load-balancing across agent replicas at both the
client and gateway layers (connection pooling pins to one replica) — realistically multiple hosts.
The positive result stands: **the tenant-isolation / connection-pool / concurrency work we optimized
is not the bottleneck**; the database and pooler have ample headroom at the current ceiling.
