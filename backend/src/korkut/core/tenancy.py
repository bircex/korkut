"""Multi-tenant primitives: the RLS tenant GUC, per-request office context, and the exact,
Postgres-proven DDL that enforces row-level isolation.

The DDL here is verified end-to-end by ``tests/test_tenancy_rls.py`` against a real Postgres.
See docs/SAAS_ARCHITECTURE.md §1 and §15 for the design.
"""

from __future__ import annotations

import contextvars

# Postgres session variable that RLS policies read. Set per transaction via ``SET LOCAL`` so it is
# transaction-scoped and cannot leak across transactions on a pooled connection.
TENANT_GUC = "app.current_office"

# Per-request tenant, set by ASGI middleware from the JWT's signed office_id claim. A ContextVar so
# concurrent requests / agent runs never share tenant state.
_current_office: contextvars.ContextVar[int | None] = contextvars.ContextVar(
    "current_office", default=None
)


def set_current_office(office_id: int | None) -> contextvars.Token[int | None]:
    """Set the current tenant; returns a token for ``reset_current_office``."""
    return _current_office.set(office_id)


def reset_current_office(token: contextvars.Token[int | None]) -> None:
    _current_office.reset(token)


def get_current_office() -> int | None:
    return _current_office.get()


def require_current_office() -> int:
    """Return the current tenant or raise — callers that touch tenant data must have one set."""
    oid = _current_office.get()
    if oid is None:
        raise RuntimeError("no tenant context set (require_current_office)")
    return oid


def rls_policy_ddl(table: str, office_col: str = "office_id") -> str:
    """SQL that puts fail-closed row-level security on a tenant table.

    ``NULLIF(current_setting(..., true), '')`` is required, not just the two-arg form: a custom GUC
    reverts to the empty string ``''`` (not NULL) after a ``SET LOCAL`` transaction ends, and
    ``''::bigint`` *raises* rather than yielding NULL. ``NULLIF(..., '')`` collapses both the unset
    (NULL) and reverted ('') cases to NULL, so a missing tenant context matches **zero rows — fail
    closed — and never errors**. ``FORCE`` keeps RLS on even for the table owner; the app
    connects as a separate non-owner ``NOBYPASSRLS`` role so the policy always applies.
    """
    pred = f"{office_col} = NULLIF(current_setting('{TENANT_GUC}', true), '')::bigint"
    return (
        f"ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;\n"
        f"ALTER TABLE {table} FORCE ROW LEVEL SECURITY;\n"
        f"CREATE POLICY {table}_tenant ON {table}\n"
        f"    USING ({pred})\n"
        f"    WITH CHECK ({pred});"
    )


def set_local_office_sql(office_id: int) -> str:
    """``SET LOCAL`` statement to scope a transaction to one office.

    Must run inside an open transaction. ``office_id`` is cast to ``int`` (SET does not take bind
    params), which also makes it injection-safe.
    """
    return f"SET LOCAL {TENANT_GUC} = '{int(office_id)}'"
