"""KDM-002 — performance baseline snapshot for the Python runtime.

Measures, with the deterministic golden-smoke seed and in-process TestClient:

* **startup**: wall time to import + reload the app module and seed the DB;
* **latency**: median / p95 wall time per endpoint over N repetitions;
* **memory**: peak Python heap during the run (``tracemalloc``) and process RSS.

Numbers are machine-dependent and are recorded as an *indicative* baseline with
their methodology, not asserted in CI. Re-run on the target host to compare the
.NET port against a like-for-like environment:

    ../.venv/bin/python -m tests.baseline.perf_baseline --reps 50

Writes a JSON snapshot next to this file when ``--out`` is given.
"""

from __future__ import annotations

import argparse
import json
import statistics
import time
import tracemalloc
from pathlib import Path
from unittest.mock import patch

from tests.baseline import golden_smoke as gs

# Read-only endpoints exercised for latency (method, path-builder from ctx).
LATENCY_ENDPOINTS = [
    ("public_config", lambda c: ("GET", "/api/config", None)),
    ("me", lambda c: ("GET", "/api/me", c["a"]["cookie"])),
    ("matters_list", lambda c: ("GET", "/api/matters", c["a"]["cookie"])),
    ("matter_get", lambda c: ("GET", f"/api/matters/{c['a']['matter_id']}", c["a"]["cookie"])),
    ("sessions", lambda c: ("GET", "/api/sessions", c["a"]["cookie"])),
    ("clients", lambda c: ("GET", "/api/clients", c["a"]["cookie"])),
]


def _percentile(values: list[float], pct: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    k = max(0, min(len(ordered) - 1, round((pct / 100) * (len(ordered) - 1))))
    return ordered[k]


def _rss_mb() -> float | None:
    try:
        import resource

        rss = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
        # macOS reports bytes; Linux reports kilobytes.
        return rss / (1024 * 1024) if rss > 10_000_000 else rss / 1024
    except Exception:
        return None


def run(reps: int, tmp_db: Path) -> dict:
    tracemalloc.start()

    t0 = time.perf_counter()
    app_mod, ctx = gs.build_app(tmp_db)
    startup_ms = (time.perf_counter() - t0) * 1000

    latency: dict[str, dict] = {}
    with patch("korkut.brain.agent.answer", return_value="x"):
        for name, builder in LATENCY_ENDPOINTS:
            method, path, cookie = builder(ctx)
            client = gs._client(app_mod, cookie)
            # Warm-up (JIT/first-hit caches) excluded from the sample.
            getattr(client, method.lower())(path)
            samples = []
            for _ in range(reps):
                t = time.perf_counter()
                getattr(client, method.lower())(path)
                samples.append((time.perf_counter() - t) * 1000)
            latency[name] = {
                "reps": reps,
                "median_ms": round(statistics.median(samples), 3),
                "p95_ms": round(_percentile(samples, 95), 3),
                "min_ms": round(min(samples), 3),
            }

    _current, peak = tracemalloc.get_traced_memory()
    tracemalloc.stop()

    return {
        "startup_ms": round(startup_ms, 2),
        "endpoints": latency,
        "python_heap_peak_mb": round(peak / (1024 * 1024), 2),
        "process_rss_mb": _rss_mb(),
        "notes": (
            "In-process FastAPI TestClient over SQLite; no network/LLM. "
            "Indicative only — re-run on the target host for .NET parity comparison."
        ),
    }


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--reps", type=int, default=50)
    ap.add_argument("--out", type=str, default="")
    args = ap.parse_args()

    import tempfile

    with tempfile.TemporaryDirectory() as d:
        snapshot = run(args.reps, Path(d) / "perf.db")

    text = json.dumps(snapshot, indent=2, ensure_ascii=False)
    print(text)
    if args.out:
        Path(args.out).write_text(text + "\n")


if __name__ == "__main__":
    main()
