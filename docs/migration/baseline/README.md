# Python runtime migration baseline (KDM-002)

This directory freezes the **observable behavior, contracts and performance** of
the current Python Korkut backend, so the .NET port (KDM-005+) can be measured
for parity against a recorded, repeatable source of truth. It is a
**read-only baseline** — KDM-002 changes no runtime behavior.

## Artifacts

| File | What it freezes |
|---|---|
| [`PYTHON_BASELINE.md`](PYTHON_BASELINE.md) | HTTP API surface, auth/session, jobs/workers, DB schema, providers, templates, toolset — each active path classified with an owner + migration decision. |
| [`ENV_CONTRACT.md`](ENV_CONTRACT.md) | Every env var name + semantics + default + read site. **No secret values.** |
| [`DEADCODE_CANDIDATES.md`](DEADCODE_CANDIDATES.md) | Unused-code candidates with usage evidence (grep). Removal is deferred to later KDM issues once parity is proven — **not** removed here. |
| [`PERFORMANCE_BASELINE.md`](PERFORMANCE_BASELINE.md) | Startup, per-endpoint latency, memory snapshot + methodology. |

## Golden smoke suite

The repeatable behavior baseline lives in `backend/tests/baseline/`:

- `golden_smoke.py` — deterministic seed + critical-flow request matrix; records
  HTTP status + normalized response **shape** (not volatile values/secrets).
- `golden_smoke.json` — the committed golden record (15 critical-flow cases).
- `test_golden_smoke.py` — asserts the current runtime still matches the golden,
  plus explicit security invariants (unauth ⇒ 401, cross-office IDOR ⇒ 403/404,
  bad body ⇒ 422, missing ⇒ 404).
- `perf_baseline.py` — performance snapshot tool.

Run it:

```bash
cd backend
../.venv/bin/python -m pytest tests/baseline/ -q          # assert against golden
KORKUT_BASELINE_UPDATE=1 ../.venv/bin/python -m tests.baseline.golden_smoke   # regenerate intentionally
../.venv/bin/python -m tests.baseline.perf_baseline --reps 50                 # perf snapshot
```

The full existing suite (**396 passed, 18 skipped**, `-m 'not live'`) is the
broader regression baseline; the golden smoke is the focused parity contract.

## Key baseline facts (see the detailed docs)

- **Schema is built by `Base.metadata.create_all()`**, not alembic — the
  `alembic/versions` dir has no revisions. There is no migration history to
  preserve; KDM-011 introduces expand-contract EF Core migrations from the
  current schema shape.
- **Tenancy already exists**: most tables use `TenantMixin` (office scoping) and
  the app has app-role/RLS awareness. HTTP-level cross-office IDOR is denied
  today (golden case `idor_cross_office_matter` ⇒ 404).
- **`KORKUT_MODELS_FILE` is not used** by the runtime; models come from
  per-provider `*_MODEL` env vars + hardcoded defaults (see `ENV_CONTRACT.md`).
