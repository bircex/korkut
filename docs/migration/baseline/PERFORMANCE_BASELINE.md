# Python runtime performance baseline (KDM-002)

Indicative performance snapshot of the current Python API, captured with the
deterministic golden-smoke seed and the in-process FastAPI TestClient (no
network, no LLM). Numbers are **machine-dependent** — they exist to give the
.NET port a like-for-like comparison point, not an absolute SLA. Re-run on the
**same host** for both runtimes before drawing conclusions.

## How to reproduce

```bash
cd backend
../.venv/bin/python -m tests.baseline.perf_baseline --reps 50
# optionally: ... --out ../docs/migration/baseline/perf_snapshot.json
```

Methodology: `tests/baseline/perf_baseline.py`
- **startup** = wall time to import + reload the app module and seed 2 offices /
  3 matters into a throwaway SQLite DB.
- **latency** = per-endpoint wall time, one warm-up call excluded, then `--reps`
  samples; median / p95 / min reported in milliseconds.
- **memory** = `tracemalloc` peak Python heap + process max RSS.

## Snapshot (reference capture 2026-07-18)

Host: developer macOS (darwin), Python 3.14, SQLite, in-process TestClient.

| Metric | Value |
|---|---|
| App startup (import+reload+seed) | ~0.79 s |
| Python heap peak | ~38 MB |
| Process max RSS | ~138 MB |

| Endpoint | median (ms) | p95 (ms) | min (ms) |
|---|---:|---:|---:|
| `GET /api/config` (public) | 1.37 | 1.82 | 1.27 |
| `GET /api/me` | 2.36 | 2.52 | 2.29 |
| `GET /api/matters` | 2.98 | 4.06 | 2.83 |
| `GET /api/matters/{id}` | 4.05 | 4.24 | 3.97 |
| `GET /api/sessions` | 2.67 | 2.76 | 2.60 |
| `GET /api/clients` | 2.31 | 2.35 | 2.26 |

## Notes & caveats

- SQLite in-process latency is a **floor**; production runs PostgreSQL over a
  network, so absolute numbers will be higher. The value here is the *relative*
  shape (which endpoints are heavier) and the memory footprint order of
  magnitude.
- Throughput was not load-tested here (no concurrency harness in-process); the
  latency figures are single-request. A concurrency/throughput comparison is
  better run against the containerized stack during KDM-034/KDM-036.
- `.env`/secrets are never loaded by this harness; a fixed test secret is used.
