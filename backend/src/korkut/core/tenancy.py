"""Multi-tenant primitives: the RLS tenant GUC, per-request office context, and the exact,
Postgres-proven DDL that enforces row-level isolation.

The DDL here is verified end-to-end by ``tests/test_tenancy_rls.py`` against a real Postgres.
See docs/SAAS_ARCHITECTURE.md §1 and §15 for the design.
"""

from __future__ import annotations

import contextvars
import uuid
from typing import TYPE_CHECKING

from sqlalchemy import ForeignKey, Uuid, event, select
from sqlalchemy.orm import Mapped, mapped_column, with_loader_criteria

if TYPE_CHECKING:
    from sqlalchemy.orm import Session, sessionmaker

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


# ---------------------------------------------------------------------------
# SQLAlchemy integration
# ---------------------------------------------------------------------------


class TenantMixin:
    """Mixin for every tenant-owned model: an ``office_id`` FK (auto-stamped from the request
    context) and a non-enumerable external ``uid`` (UUID) — internal PKs stay ``bigint``, but the
    id exposed in URLs/APIs is the random ``uid``, which kills id-guessing (IDOR). See §15."""

    office_id: Mapped[int] = mapped_column(
        ForeignKey("offices.id", ondelete="CASCADE"),
        index=True,
        default=require_current_office,
    )
    uid: Mapped[uuid.UUID] = mapped_column(Uuid, default=uuid.uuid4, unique=True, index=True)


def install_tenant_guc(session_factory: sessionmaker[Session]) -> None:
    """On Postgres, set the RLS GUC at the start of every transaction from the request context, so
    every query on the session is scoped by RLS. No-op on SQLite (no RLS)."""

    @event.listens_for(session_factory, "after_begin")
    def _after_begin(session, transaction, connection):
        if connection.dialect.name == "postgresql":
            oid = get_current_office()
            if oid is not None:
                connection.exec_driver_sql(set_local_office_sql(oid))


def install_tenant_filter(session_factory: sessionmaker[Session]) -> None:
    """App-layer belt-and-suspenders filter: scope every SELECT / UPDATE / DELETE of a
    ``TenantMixin`` entity to the current office. Covers the SQLite dev/test path (which has no
    RLS) and fails closed (no context ⇒ matches nothing). Bypass for a deliberate cross-tenant
    query via
    ``execution_options(skip_tenant_filter=True)`` — reserved for the Phase 2 cross-tenant
    resolvers (login by Google sub, invite/telegram lookups) that run before an office is known."""

    @event.listens_for(session_factory, "do_orm_execute")
    def _scope(state):
        scoped = state.is_select or state.is_update or state.is_delete
        if scoped and not state.execution_options.get("skip_tenant_filter"):
            # Read the context OUTSIDE the lambda — SQLAlchemy's lambda-SQL system extracts bound
            # values without invoking closures, so a call inside the lambda is rejected. `oid` is a
            # fresh closure value per execution, so the filter stays dynamic (and fail-closed: a
            # None oid yields `office_id IS NULL`, which matches nothing on a NOT NULL column).
            oid = get_current_office()
            state.statement = state.statement.options(
                with_loader_criteria(
                    TenantMixin,
                    lambda cls: cls.office_id == oid,
                    include_aliases=True,
                )
            )


class NotOwned(LookupError):
    """Raised by ``load_owned`` when the object is absent or belongs to another tenant. The API
    layer maps this to 404 (never 403 — we don't confirm existence across tenants)."""


def load_owned[M: TenantMixin](session: Session, model: type[M], uid: uuid.UUID | str) -> M:
    """Fetch a tenant object by its external ``uid`` under the current tenant scope, or raise
    ``NotOwned``. The single funnel for object access — no raw ``session.get(Model, id)`` in
    handlers — so RLS + the app filter guarantee cross-tenant ids resolve to 404."""
    obj = session.scalars(select(model).where(model.uid == uid)).first()
    if obj is None:
        raise NotOwned(f"{model.__name__} {uid} not in current tenant")
    return obj
