# Compatibility & differential harness (KDM-003)

Machine-checks Python↔.NET parity. Runs live against the Python runtime today;
the cross-runtime comparison activates when the .NET port exists (KDM-034).

## Layout

| File | Role |
|---|---|
| `contracts/openapi.json` | Versioned OpenAPI snapshot (drift guard). |
| `openapi_snapshot.py` | Extract + normalize the live OpenAPI; (re)write the snapshot. |
| `fixtures.json` | Deterministic, PII/secret-free request fixtures. |
| `adapters.py` | `PythonAdapter` (in-process, live) + `DotNetAdapter` (HTTP, KDM-034). |
| `normalize.py` | Canonicalize a result: mask volatile ids/dates/tokens to type tokens. |
| `differential.py` | Run fixtures through an adapter → normalized results. |
| `expected_python.json` | Recorded Python normalized output (parity reference). |
| `legacy_diffs.json` / `legacy_diffs.py` | Allowlist of known diffs (owner + expiry). |
| `test_compat.py` | The gate: OpenAPI drift, Python parity, allowlist, cross-runtime. |

## Run

```bash
cd backend
../.venv/bin/python -m pytest tests/compat/ -q          # the full gate

# Intentional, reviewed contract updates only:
KORKUT_CONTRACT_UPDATE=1 ../.venv/bin/python -m tests.compat.openapi_snapshot
KORKUT_CONTRACT_UPDATE=1 ../.venv/bin/python -m tests.compat.differential

# Cross-runtime (once the .NET port is up — KDM-034):
KORKUT_DOTNET_BASE_URL=http://localhost:5080 ../.venv/bin/python -m pytest tests/compat/ -q
```

## Adding a fixture

Add an entry to `fixtures.json` (no PII/secrets — enforced by
`test_fixtures_are_secret_and_pii_free`), regenerate `expected_python.json` with
the update flag, and review the diff. See `docs/migration/compat/CONTRACTS.md`
for the semantics each fixture pins.

## Known-diff allowlist

When a real, justified Python↔.NET difference is found during KDM-034, add an
entry to `legacy_diffs.json` with `fixture_id`, `owner`, `reason`, `expires`
(YYYY-MM-DD) and `tracking_issue`. Expired entries **fail** the test, forcing
re-review. The list is empty until the .NET runtime exists.
