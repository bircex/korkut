"""Prove the REAL schema (all 24 models) isolates tenants on Postgres with a non-superuser role.

This is the production shape: admin bootstraps the schema + RLS (store.apply_tenant_rls), the app
connects as a non-superuser role, and every query is office-scoped by RLS + the app filter. Skipped
unless KORKUT_TEST_PG_URL points at a Postgres superuser DSN.
"""

from __future__ import annotations

import os
from urllib.parse import urlsplit, urlunsplit

import pytest
from sqlalchemy import create_engine, text
from sqlalchemy.orm import sessionmaker

from korkut.api import models
from korkut.api.store import apply_tenant_rls, tenant_table_names
from korkut.core import db
from korkut.core.tenancy import (
    install_tenant_filter,
    install_tenant_guc,
    reset_current_office,
    set_current_office,
)

pytest.importorskip("psycopg")

SUPER = os.environ.get("KORKUT_TEST_PG_URL")
pytestmark = pytest.mark.skipif(
    not SUPER, reason="set KORKUT_TEST_PG_URL for the real-schema RLS test"
)

APP_ROLE = "korkut_real_test"
APP_PW = "apppw"


def _sa(url: str) -> str:
    p = urlsplit(url)
    scheme = "postgresql+psycopg" if p.scheme == "postgresql" else p.scheme
    return urlunsplit((scheme, p.netloc, p.path, p.query, p.fragment))


def _app_url(url: str) -> str:
    p = urlsplit(url)
    scheme = "postgresql+psycopg" if p.scheme == "postgresql" else p.scheme
    netloc = f"{APP_ROLE}:{APP_PW}@{p.hostname}" + (f":{p.port}" if p.port else "")
    return urlunsplit((scheme, netloc, p.path, p.query, p.fragment))


@pytest.fixture
def app_sf():
    admin = create_engine(_sa(SUPER))
    # clean slate
    with admin.begin() as cn:
        cn.execute(text("DROP SCHEMA public CASCADE"))
        cn.execute(text("CREATE SCHEMA public"))
        cn.execute(
            text(
                f"DO $$ BEGIN IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='{APP_ROLE}') THEN "
                f"EXECUTE 'DROP OWNED BY {APP_ROLE}'; EXECUTE 'DROP ROLE {APP_ROLE}'; "
                "END IF; END $$;"
            )
        )
        cn.execute(
            text(f"CREATE ROLE {APP_ROLE} LOGIN PASSWORD '{APP_PW}' NOSUPERUSER NOBYPASSRLS")
        )
    # admin builds the real schema + RLS
    db.init_db(admin)
    apply_tenant_rls(admin)
    with admin.begin() as cn:
        dml = "SELECT, INSERT, UPDATE, DELETE"
        cn.execute(text(f"GRANT USAGE ON SCHEMA public TO {APP_ROLE}"))
        cn.execute(text(f"GRANT {dml} ON ALL TABLES IN SCHEMA public TO {APP_ROLE}"))
        cn.execute(text(f"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {APP_ROLE}"))
    # seed two offices + a client each (admin bypasses RLS; office_id passed explicitly)
    admin_sf = sessionmaker(admin, expire_on_commit=False)
    with admin_sf() as s:
        oa, ob = models.Office(name="A"), models.Office(name="B")
        s.add_all([oa, ob])
        s.flush()
        s.add_all(
            [
                models.Client(office_id=oa.id, full_name="Ahmet (A)"),
                models.Client(office_id=ob.id, full_name="Mehmet (B)"),
            ]
        )
        s.commit()
        ids = (oa.id, ob.id)

    app = create_engine(_app_url(SUPER))
    sf = sessionmaker(app, expire_on_commit=False)
    install_tenant_guc(sf)
    install_tenant_filter(sf)
    yield sf, ids
    app.dispose()
    admin.dispose()


def _client_names(sf, office_id: int) -> list[str]:
    tok = set_current_office(office_id)
    try:
        with sf() as s:
            return sorted(c.full_name for c in s.query(models.Client).all())
    finally:
        reset_current_office(tok)


def test_tenant_table_names_cover_the_domain():
    names = tenant_table_names()
    assert {"users", "clients", "matters", "conversations", "invoices"} <= set(names)
    assert "offices" not in names  # the tenant root is not itself tenant-scoped
    assert "template_defaults" not in names  # shared reference table


def test_real_schema_isolates_clients(app_sf):
    sf, (a_id, b_id) = app_sf
    assert _client_names(sf, a_id) == ["Ahmet (A)"]
    assert _client_names(sf, b_id) == ["Mehmet (B)"]


def test_real_schema_fail_closed_without_context(app_sf):
    sf, _ = app_sf
    tok = set_current_office(None)  # override the autouse conftest context
    try:
        with sf() as s:
            assert s.query(models.Client).all() == []
    finally:
        reset_current_office(tok)


def test_real_schema_with_check_blocks_cross_office_insert(app_sf):
    from sqlalchemy.exc import ProgrammingError

    sf, (a_id, b_id) = app_sf
    tok = set_current_office(a_id)
    try:
        with sf() as s:
            s.add(models.Client(office_id=b_id, full_name="evil"))
            with pytest.raises(ProgrammingError):
                s.commit()
    finally:
        reset_current_office(tok)
