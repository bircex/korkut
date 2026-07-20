# Python ↔ .NET Differential & Data-Migration Rehearsal (KDM-034)

Proves behaviour + data parity **before** the .NET runtime touches production.

## 1. Shadow / differential compare

Both runtimes must produce *equivalent observable output* for the same request.
Equivalence excludes volatile, non-contractual detail (generated ids, timestamps,
tokens, cookies).

- **Normalize** each response to a canonical form
  (`Korkut.Application.Differential.ResponseNormalizer`, a 1:1 port of the Python
  `tests/compat/normalize.py`). Volatile keys/values are masked to **type tokens**
  (`<uuid>`, `<iso8601>`, `<int>` …) — a shape/type change still diffs, a value
  change does not, and **no raw id/token/PII can leak into an artifact**.
- **Compare** the two normalized records (`DifferentialComparator`). Surviving
  differences are classified:
  - **High** — status / shape (missing-extra field) / type / array-length / value.
  - **Low** — set-like reordering only.

## 2. Mismatch telemetry & rollback criterion

`RolloutPolicy.Summarize` produces `MismatchTelemetry { Total, High, Low, HighPaths }`.

- **Cut over** to .NET (dual-read → traffic switch) **only when `High == 0`** on every
  critical flow (`CanCutOver`).
- **Roll back** immediately if any high-severity mismatch appears in production shadow
  traffic (`MustRollback`). Low-severity mismatches are logged, triaged, and do not
  block.

## 3. Data-migration rehearsal

Run on an **anonymized, production-like snapshot** — never real PII/secrets.

- `DataParity.Fingerprint` computes a per-table **row count + content checksum**, with
  PII columns **pseudonymized** (keyed HMAC) before anything is serialized, so the
  emitted artifact (a hex digest) can never contain a raw value, while identical
  logical data still yields identical checksums.
- The rehearsal (`MigrationRehearsalTests`, real PostgreSQL) asserts:
  1. **Idempotent** — re-running the migrator applies no new migrations and leaves
     every row unchanged (`GetPendingMigrations` empty; fingerprints unchanged).
  2. **Order-independent parity** — the same preserved-id snapshot seeded in a
     different order converges to the same checksum.
  3. **No PII** — seeded PII strings never appear in any fingerprint.

## 4. Required comparison surfaces

Contract suite (normalized response pairs), row/count/checksum (§3), auth/session
(normalized login/2FA/refresh shapes), tenant isolation (per-office row parity, already
guarded by the RLS TenantMatrix), and representative load comparison (repeat the
contract suite over a batch and assert `High == 0`).
