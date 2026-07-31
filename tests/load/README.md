# Korkut load / benchmark

Stresses the multi-tenant concurrency infrastructure (connection pool, per-office concurrency,
tenant isolation, the tool loop, document/OCR heavy work) with **k6** and records a benchmark
summary.

Scenario: **N tenants** (default 10); each VU owns one tenant, logs in, opens a session, then runs a
weighted mix of heavy operations:

| Operation | Weight | What it does |
|---|---|---|
| RAG chat | 77% | Full **tool loop** through the mock LLM (tool call + real DB-backed tool + answer) |
| Document upload | 20% | `/api/sessions/{id}/upload` → text-extraction pipeline |
| OCR | 3% | Real **Tesseract** OCR over a scanned image, straight to the OCR host |

## Why a mock LLM?

200 chats × up to 8 iterations = tens of thousands of model calls. Hitting real DeepSeek would (a)
rate-limit (429), (b) cost real money, (c) make the benchmark measure DeepSeek. Instead
[`mock-llm/`](mock-llm/app.py) is an instant, tool-call-emitting OpenAI-compatible stub; the agent's
`DEEPSEEK_BASE_URL` points at it, so the run measures **our** system. `MOCK_LATENCY_MS` (default
300ms) simulates model think-time so concurrency actually overlaps. The mock also stands in for the
Knowledge `/search` endpoint (`KORKUT_KNOWLEDGE_URL`) to return fast RAG grounding instead of
retrying against a knowledge host that can't embed in this stack.

## Real OCR

The OCR host is normally a scaffold (`DisabledOcrService` → 503). In the load stack,
`KORKUT_OCR_ENGINE=tesseract` activates
[`TesseractOcrService`](../../src/modules/Korkut.Documents/Services/TesseractOcrService.cs); the image
ships `tesseract` + `tesseract-ocr-tur/eng` + `poppler-utils` (see
[`docker/Dockerfile.ocr`](../../docker/Dockerfile.ocr)). k6 runs on the stack network, so it hits the
internal `ocr:8080/extract` directly (passing the access token as `Authorization: Bearer`).

## Running

The root stack must be up first (`docker compose up -d`). Then:

```bash
# Small run that validates the wiring (2 tenants, 5 VUs, 20s)
tests/load/run.sh --smoke

# Full run (default 10 tenants, 200 VUs, 2m)
tests/load/run.sh

# Parameterized
N_TENANTS=10 VUS=200 DURATION=3m RAMP=30s tests/load/run.sh
```

`run.sh` in order: brings up the load stack (mock-llm + agent→mock + real OCR) → generates fixtures
→ seeds tenants → runs k6 on the network → writes the summary to
`tests/load/results/summary-<ts>.json`.

## What it measures

- Per-operation latency (`rag_chat_ms`, `upload_ms`, `ocr_ms` — p50/p90/p95/p99).
- Throughput (`rag_chat_ok`, `upload_ok`, `ocr_ok` counters), error rate (`op_errors`, `http_req_failed`).
- Thresholds (see `k6/rag-load.js` `options.thresholds`) — a breach makes k6 exit non-zero (CI gate).

Because the Grafana LGTM stack is present, k6 can also stream to Prometheus remote-write
(`K6_PROMETHEUS_RW_SERVER_URL` + `-o experimental-prometheus-rw`); the JSON summary is enough for now.

## Baseline results

Recorded reference runs live in [`BASELINE.md`](BASELINE.md) (human-readable) and
`results/baseline-balanced.json` (raw k6 summary). Compare against them to catch regressions.

## First-run finding

At 10 tenants × 200 VUs, chat and upload scale at ~0% error (p95 ~2.5s). Real Tesseract OCR is
**CPU-bound**: a single OCR container hits **~1400% CPU** under load and saturates at ~10-20
concurrent requests, timing out beyond that. Heavy OCR therefore needs the OCR host **scaled
horizontally (replicas) or a queue** — or keep `W_OCR` low (the default).

## Teardown / back to the normal stack

To drop the load overlay and restore the normal stack (agent back on DeepSeek, OCR back to the scaffold):

```bash
docker compose up -d agent ocr        # without the overlay → normal env
# stop the mock:
docker compose -f docker-compose.yml -f tests/load/docker-compose.load.yml stop mock-llm
```

## Deliberate choices / limits

- **k6, not k8s**: k8s is orchestration; k6 is the load-testing tool. Runs via Docker.
- **Per-office concurrency**: `KORKUT_OFFICE_CONCURRENCY` (default 4) caps concurrent model calls per
  tenant. At 10 tenants × 20 VUs, chats queue at the governor — realistic; for higher raw throughput
  raise the tenant count or the cap.
- **Horizontal scale**: on a single replica the concurrency cap is per-replica. Across replicas it
  needs a distributed cap (deferred).
- **fixtures/**, **tenants.json**, and per-run **results/** are generated, not committed (see
  `.gitignore`); the committed `results/baseline-*.json` is the reference.
