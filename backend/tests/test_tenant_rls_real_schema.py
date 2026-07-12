"""Prove the REAL schema (all 24 models) isolates tenants on Postgres with a non-superuser role.

This is the production shape: admin bootstraps the schema + RLS (store.apply_tenant_rls), the app
connects as a non-superuser role, and every query is office-scoped by RLS + the app filter. Skipped
unless KORKUT_TEST_PG_URL points at a Postgres superuser DSN.
"""

from __future__ import annotations

import os
from datetime import datetime
from urllib.parse import urlsplit, urlunsplit

import pytest
from sqlalchemy import create_engine, text
from sqlalchemy.orm import sessionmaker

from korkut.api import models
from korkut.api.store import (
    apply_tenant_rls,
    resolve_office_for_invite,
    resolve_office_for_signin,
    setup_pg_resolvers,
    tenant_table_names,
)
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
    # admin builds the real schema + RLS + the pre-tenant resolver
    db.init_db(admin)
    apply_tenant_rls(admin)
    setup_pg_resolvers(admin)
    with admin.begin() as cn:
        dml = "SELECT, INSERT, UPDATE, DELETE"
        cn.execute(text(f"GRANT USAGE ON SCHEMA public TO {APP_ROLE}"))
        cn.execute(text(f"GRANT {dml} ON ALL TABLES IN SCHEMA public TO {APP_ROLE}"))
        cn.execute(text(f"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {APP_ROLE}"))
        # prod bootstrap grants the resolver to the app role (setup_pg_resolvers granted it to the
        # bootstrapping admin under the single-role dev model)
        cn.execute(
            text(f"GRANT EXECUTE ON FUNCTION resolve_office_for_signin(text, text) TO {APP_ROLE}")
        )
        cn.execute(
            text(f"GRANT EXECUTE ON FUNCTION resolve_office_for_invite(text) TO {APP_ROLE}")
        )
    # seed two offices + a client and a user each (admin bypasses RLS; office_id passed explicitly)
    admin_sf = sessionmaker(admin, expire_on_commit=False)
    with admin_sf() as s:
        oa, ob = models.Office(name="A"), models.Office(name="B")
        s.add_all([oa, ob])
        s.flush()
        # ua is already bound to a Google sub; ub is pre-provisioned by email, not yet bound
        ua = models.User(
            office_id=oa.id, username="a-owner", email="a@x.co", google_sub="sub-a", role="owner"
        )
        ub = models.User(office_id=ob.id, username="b-owner", email="b@x.co", role="owner")
        future = datetime(2099, 1, 1)
        s.add_all(
            [
                models.Client(office_id=oa.id, full_name="Ahmet (A)"),
                models.Client(office_id=ob.id, full_name="Mehmet (B)"),
                ua,
                ub,
                models.OfficeInvite(
                    office_id=oa.id, token_hash="hash-a", role="member", expires_at=future
                ),
            ]
        )
        s.commit()
        ids = (oa.id, ob.id)
        uids = (ua.id, ub.id)

    app = create_engine(_app_url(SUPER))
    sf = sessionmaker(app, expire_on_commit=False)
    install_tenant_guc(sf)
    install_tenant_filter(sf)
    yield sf, ids, uids
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
    sf, (a_id, b_id), _ = app_sf
    assert _client_names(sf, a_id) == ["Ahmet (A)"]
    assert _client_names(sf, b_id) == ["Mehmet (B)"]


def test_real_schema_fail_closed_without_context(app_sf):
    sf, _, _ = app_sf
    tok = set_current_office(None)  # override the autouse conftest context
    try:
        with sf() as s:
            assert s.query(models.Client).all() == []
    finally:
        reset_current_office(tok)


def test_real_schema_with_check_blocks_cross_office_insert(app_sf):
    from sqlalchemy.exc import ProgrammingError

    sf, (a_id, b_id), _ = app_sf
    tok = set_current_office(a_id)
    try:
        with sf() as s:
            s.add(models.Client(office_id=b_id, full_name="evil"))
            with pytest.raises(ProgrammingError):
                s.commit()
    finally:
        reset_current_office(tok)


def test_composite_fk_rejects_cross_office_reference(app_sf):
    """A matter in office A cannot reference office B's client — the composite FK rejects it."""
    from sqlalchemy.exc import IntegrityError

    sf, (a_id, b_id), _ = app_sf
    tok = set_current_office(b_id)
    try:
        with sf() as s:
            b_client_id = s.query(models.Client).one().id
    finally:
        reset_current_office(tok)

    tok = set_current_office(a_id)
    try:
        with sf() as s:
            s.add(models.Matter(office_id=a_id, title="x", client_id=b_client_id))
            with pytest.raises(IntegrityError):
                s.commit()
    finally:
        reset_current_office(tok)


def test_resolver_maps_signin_to_office_without_context(app_sf):
    # the Google callback resolves office from sub/email BEFORE any tenant context exists
    sf, (a_id, b_id), _ = app_sf
    tok = set_current_office(None)
    try:
        with sf() as s:
            # bound account: matched by its Google sub (email irrelevant)
            assert resolve_office_for_signin(s, "sub-a", "who@x.co") == a_id
            # pre-provisioned account: matched by email while google_sub IS NULL
            assert resolve_office_for_signin(s, "unbound-sub", "b@x.co") == b_id
            # nothing matches -> None (fail-closed)
            assert resolve_office_for_signin(s, "nope", "nobody@x.co") is None
    finally:
        reset_current_office(tok)


def test_resolver_is_not_a_cross_tenant_read_hole(app_sf):
    # the SECURITY DEFINER resolver must not let the app role read another office's rows directly
    sf, (a_id, _b_id), _ = app_sf
    tok = set_current_office(a_id)
    try:
        with sf() as s:
            rows = s.query(models.User).all()
            assert [u.username for u in rows] == ["a-owner"]  # never b-owner
    finally:
        reset_current_office(tok)


def test_invite_resolver_maps_token_to_office_without_context(app_sf):
    # invite redemption is pre-tenant too: the token hash resolves to its office, unknown -> None
    sf, (a_id, _b_id), _ = app_sf
    tok = set_current_office(None)
    try:
        with sf() as s:
            assert resolve_office_for_invite(s, "hash-a") == a_id
            assert resolve_office_for_invite(s, "no-such-hash") is None
    finally:
        reset_current_office(tok)
