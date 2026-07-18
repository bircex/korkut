# Dead-code / non-use candidates (KDM-002)

Candidates with **usage evidence**. Per the migration rules, KDM-002 **does not
change behavior or remove anything** — removal is deferred to the KDM issue that
proves parity for the replacing .NET path (and, for Python removal, to the
cutover gates KDM-034+). This list is the audit input for those later removals.

Verification commands were run 2026-07-18 from `korkut-backend/`.

## 1. Alembic migration tree — **high confidence unused at runtime**

- **Evidence**: `backend/alembic/versions/` has **no `.py` sources**, only 3
  orphaned `.pyc` (`e92d85015d0b_baseline_schema`,
  `b8945f4948e1_add_users_require_2fa`, `b7e2c4a19f30_unify_calendar_events`).
  No `alembic.ini` (`find backend -name alembic.ini` → 0). Alembic is not invoked
  by any entrypoint, `Makefile`, `deploy/`, `scripts/`, `.github/`, or compose
  service. Runtime schema is `create_all` (`core/db.py:23`, `store.py:63`).
- **Decision**: treat as non-authoritative now. Remove the orphaned `alembic/`
  tree only once `KDM-011` establishes EF Core migrations as the schema source of
  truth (so the removal has a proven replacement).

## 2. `office_channels` table + `OfficeChannel` model — orphaned

- **Evidence**: the table appears in the dead baseline migration, but **no
  `OfficeChannel` class exists in `models.py`** (`grep -c OfficeChannel
  models.py` → 0). Only docstring references remain (`messaging/channel.py:5,34`,
  `messaging/gmail.py:5`).
- **Decision**: do not port the `office_channels` table. Tied to the absent
  integrations subsystem (#4).

## 3. `users.require_2fa` column — dead

- **Evidence**: added by migration `b8945f4948e1`, but **no reader** anywhere and
  **not in the ORM** (`grep -c require_2fa models.py` → 0).
- **Decision**: do not carry the column forward. Office TOTP is designed fresh in
  `KDM-017`; this stale column is not its basis.

## 4. Telegram/WhatsApp integrations subsystem — absent from checkout

- **Evidence**: `korkut.integrations.runner` and a Node `sidecar` are referenced
  in `deploy/docker-compose.yml`, `.prod.yml`, and `deploy/cron/korkut.crontab`,
  but the package **does not exist in this source tree** (`find backend/src
  -iname '*integ*'` → 0). Remaining Telegram/WhatsApp/`gateway` mentions are
  comments only. `OutboundMessage.kind` / `CalendarEvent.remind_channel`
  anticipate them but **only `email` is wired**.
- **Decision**: **out of migration scope** until the source is located. Flagged
  to the migration owner — either it lives in another repo (then it is a separate
  migration) or it is genuinely gone (then the deploy/cron references are stale
  and should be cleaned when the deploy config is migrated). Do **not** invent it.

## Explicitly NOT dead (checked, keep in scope)

- `api/adduser.py` — live CLI provisioning, referenced by
  `scripts/install.sh/.ps1` (`python -m korkut.api.adduser`). **Port.**
- `api/coverage.py` — used via lazy import (`app.py:1143`). **Port.**
- `finance_store`, `reports_store`, `matter_tools`, `office_tools` — all
  referenced by live endpoints. **Port.**

## Note on scope discipline

None of the above is removed in KDM-002. Each removal must be paired with a
proven replacement (.NET parity) and, where it removes a Python path, must wait
for the differential/cutover gates (KDM-034+) per the program rules.
