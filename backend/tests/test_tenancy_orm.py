"""Prove the SQLAlchemy tenancy integration in ``core.tenancy`` on a toy ``TenantMixin`` model.

Two paths:
- **SQLite** (always): the app-layer ``install_tenant_filter`` scopes ORM selects by office and
  fails closed with no context — the belt-and-suspenders for dev/test where RLS doesn't exist.
- **Postgres** (when ``KORKUT_TEST_PG_URL`` is set): the real thing — ``install_tenant_guc`` +
  ``rls_policy_ddl`` isolate at the DB, ``WITH CHECK`` blocks writing another office, and
  ``office_id`` auto-stamps from the request context.
"""

from __future__ import annotations

import contextlib
import os
from urllib.parse import urlsplit, urlunsplit

import pytest
from sqlalchemy import create_engine, text
from sqlalchemy.exc import ProgrammingError
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column, sessionmaker
from sqlalchemy.pool import StaticPool

from korkut.core.tenancy import (
    NotOwned,
    TenantMixin,
    install_tenant_filter,
    install_tenant_guc,
    load_owned,
    reset_current_office,
    rls_policy_ddl,
    set_current_office,
)

psycopg = pytest.importorskip("psycopg")

APP_ROLE = "korkut_orm_test"
APP_PW = "apppw"


class Base(DeclarativeBase):
    pass


class Office(Base):
    __tablename__ = "offices"
    id: Mapped[int] = mapped_column(primary_key=True)
    name: Mapped[str]


class Widget(TenantMixin, Base):
    __tablename__ = "widgets"
    id: Mapped[int] = mapped_column(primary_key=True)
    label: Mapped[str]


@contextlib.contextmanager
def office(session_factory, oid):
    """A fresh session bound to office ``oid`` (context set before the txn begins)."""
    tok = set_current_office(oid)
    try:
        with session_factory() as s:
            yield s
    finally:
        reset_current_office(tok)


def _labels(session_factory, oid) -> list[str]:
    with office(session_factory, oid) as s:
        return sorted(w.label for w in s.query(Widget).all())


# ---------------------------------------------------------------------------
# SQLite — app-layer filter path (no RLS)
# ---------------------------------------------------------------------------


@pytest.fixture
def sqlite_sf():
    engine = create_engine(
        "sqlite://", connect_args={"check_same_thread": False}, poolclass=StaticPool
    )
    Base.metadata.create_all(engine)
    sf = sessionmaker(engine)
    install_tenant_guc(sf)  # no-op on sqlite, installed for parity
    install_tenant_filter(sf)
    # seed two offices via a context-free session (bypass the filter for setup)
    with sf() as s:
        s.add_all([Office(id=1, name="A"), Office(id=2, name="B")])
        s.commit()
    with office(sf, 1) as s:
        s.add_all([Widget(label="a1"), Widget(label="a2")])
        s.commit()
    with office(sf, 2) as s:
        s.add_all([Widget(label="b1")])
        s.commit()
    return sf


def test_sqlite_filter_isolates_reads(sqlite_sf):
    assert _labels(sqlite_sf, 1) == ["a1", "a2"]
    assert _labels(sqlite_sf, 2) == ["b1"]


def test_sqlite_autostamp_office_id(sqlite_sf):
    with office(sqlite_sf, 1) as s:
        w = Widget(label="a3")
        s.add(w)
        s.commit()
        assert w.office_id == 1  # stamped from context, not passed
    assert "a3" in _labels(sqlite_sf, 1)
    assert "a3" not in _labels(sqlite_sf, 2)


def test_sqlite_fail_closed_without_context(sqlite_sf):
    tok = set_current_office(None)
    try:
        with sqlite_sf() as s:
            assert s.query(Widget).all() == []
    finally:
        reset_current_office(tok)


def test_sqlite_load_owned_cross_tenant_is_notowned(sqlite_sf):
    with office(sqlite_sf, 1) as s:
        a_uid = s.query(Widget).filter_by(label="a1").one().uid
    with office(sqlite_sf, 1) as s:
        assert load_owned(s, Widget, a_uid).label == "a1"
    with office(sqlite_sf, 2) as s, pytest.raises(NotOwned):
        load_owned(s, Widget, a_uid)


# ---------------------------------------------------------------------------
# Postgres — real RLS path
# ---------------------------------------------------------------------------

SUPER_URL = os.environ.get("KORKUT_TEST_PG_URL")
pg = pytest.mark.skipif(not SUPER_URL, reason="set KORKUT_TEST_PG_URL for the Postgres RLS path")


def _sa_scheme(scheme: str) -> str:
    # SQLAlchemy defaults `postgresql://` to psycopg2; force the installed psycopg (v3) driver.
    return "postgresql+psycopg" if scheme == "postgresql" else scheme


def _sa_url(super_url: str) -> str:
    p = urlsplit(super_url)
    return urlunsplit((_sa_scheme(p.scheme), p.netloc, p.path, p.query, p.fragment))


def _app_url(super_url: str) -> str:
    p = urlsplit(super_url)
    netloc = f"{APP_ROLE}:{APP_PW}@{p.hostname}" + (f":{p.port}" if p.port else "")
    return urlunsplit((_sa_scheme(p.scheme), netloc, p.path, p.query, p.fragment))


@pytest.fixture
def pg_sf():
    super_engine = create_engine(_sa_url(SUPER_URL))
    with super_engine.begin() as cn:
        cn.execute(text("DROP TABLE IF EXISTS widgets CASCADE"))
        cn.execute(text("DROP TABLE IF EXISTS offices CASCADE"))
        cn.execute(
            text(
                f"DO $$ BEGIN IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='{APP_ROLE}') THEN "
                f"EXECUTE 'DROP OWNED BY {APP_ROLE}'; EXECUTE 'DROP ROLE {APP_ROLE}'; "
                f"END IF; END $$;"
            )
        )
        cn.execute(
            text(
                f"CREATE ROLE {APP_ROLE} LOGIN PASSWORD '{APP_PW}' "
                f"NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE"
            )
        )
    Base.metadata.create_all(super_engine)
    with super_engine.begin() as cn:
        for stmt in rls_policy_ddl("widgets").split(";\n"):
            if stmt.strip():
                cn.execute(text(stmt))
        cn.execute(text(f"GRANT USAGE ON SCHEMA public TO {APP_ROLE}"))
        cn.execute(text(f"GRANT SELECT, INSERT, UPDATE, DELETE ON widgets TO {APP_ROLE}"))
        cn.execute(text(f"GRANT SELECT ON offices TO {APP_ROLE}"))
        cn.execute(text(f"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {APP_ROLE}"))
    # seed as superuser (bypasses RLS); office_id passed explicitly
    su_sf = sessionmaker(super_engine)
    with su_sf() as s:
        s.add_all([Office(id=1, name="A"), Office(id=2, name="B")])
        s.add_all(
            [
                Widget(label="a1", office_id=1),
                Widget(label="a2", office_id=1),
                Widget(label="b1", office_id=2),
            ]
        )
        s.commit()

    app_engine = create_engine(_app_url(SUPER_URL))
    sf = sessionmaker(app_engine)
    install_tenant_guc(sf)
    install_tenant_filter(sf)
    yield sf
    app_engine.dispose()
    super_engine.dispose()


@pg
def test_pg_rls_isolates_reads(pg_sf):
    assert _labels(pg_sf, 1) == ["a1", "a2"]
    assert _labels(pg_sf, 2) == ["b1"]


@pg
def test_pg_autostamp_and_isolation(pg_sf):
    with office(pg_sf, 1) as s:
        w = Widget(label="a3")
        s.add(w)
        s.commit()
        assert w.office_id == 1
    assert "a3" in _labels(pg_sf, 1)
    assert "a3" not in _labels(pg_sf, 2)


@pg
def test_pg_with_check_blocks_other_office_insert(pg_sf):
    with office(pg_sf, 1) as s:
        s.add(Widget(label="evil", office_id=2))
        with pytest.raises(ProgrammingError) as ei:
            s.commit()
    assert isinstance(ei.value.orig, psycopg.errors.InsufficientPrivilege)


@pg
def test_pg_load_owned_cross_tenant_is_notowned(pg_sf):
    with office(pg_sf, 1) as s:
        a_uid = s.query(Widget).filter_by(label="a1").one().uid
    with office(pg_sf, 2) as s, pytest.raises(NotOwned):
        load_owned(s, Widget, a_uid)
