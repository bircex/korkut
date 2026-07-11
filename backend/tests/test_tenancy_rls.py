"""End-to-end proof that the RLS primitives in ``core.tenancy`` isolate tenants on real Postgres.

Skipped unless ``KORKUT_TEST_PG_URL`` points at a Postgres superuser DSN (RLS does not exist in the
SQLite dev/test path). CI runs it against a Postgres service. It builds a toy tenant table using the
shipped ``rls_policy_ddl`` / ``set_local_office_sql`` and asserts the five properties the whole
isolation model depends on.
"""

from __future__ import annotations

import os
from urllib.parse import urlsplit, urlunsplit

import pytest

from korkut.core.tenancy import rls_policy_ddl, set_local_office_sql

psycopg = pytest.importorskip("psycopg")

SUPER_URL = os.environ.get("KORKUT_TEST_PG_URL")
pytestmark = pytest.mark.skipif(
    not SUPER_URL, reason="set KORKUT_TEST_PG_URL to a Postgres superuser DSN to run RLS tests"
)

APP_ROLE = "korkut_app_test"
APP_PW = "apppw"


def _app_url(super_url: str) -> str:
    parts = urlsplit(super_url)
    netloc = f"{APP_ROLE}:{APP_PW}@{parts.hostname}"
    if parts.port:
        netloc += f":{parts.port}"
    return urlunsplit((parts.scheme, netloc, parts.path, parts.query, parts.fragment))


def _setup(super_url: str) -> None:
    ddl = rls_policy_ddl("items")
    with psycopg.connect(super_url, autocommit=True) as cn:
        cn.execute("DROP TABLE IF EXISTS items")
        cn.execute("DROP TABLE IF EXISTS offices")
        cn.execute(
            f"DO $$ BEGIN "
            f"IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='{APP_ROLE}') THEN "
            f"EXECUTE 'DROP OWNED BY {APP_ROLE}'; EXECUTE 'DROP ROLE {APP_ROLE}'; END IF; END $$;"
        )
        cn.execute(
            f"CREATE ROLE {APP_ROLE} LOGIN PASSWORD '{APP_PW}' "
            f"NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE"
        )
        cn.execute(
            "CREATE TABLE offices (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, name text)"
        )
        cn.execute(
            "CREATE TABLE items ("
            "id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, "
            "office_id bigint NOT NULL REFERENCES offices(id) ON DELETE CASCADE, "
            "label text NOT NULL)"
        )
        cn.execute(ddl)
        cn.execute(f"GRANT USAGE ON SCHEMA public TO {APP_ROLE}")
        cn.execute(f"GRANT SELECT, INSERT, UPDATE, DELETE ON items TO {APP_ROLE}")
        cn.execute(f"GRANT SELECT ON offices TO {APP_ROLE}")
        cn.execute("INSERT INTO offices (name) VALUES ('A'), ('B')")
        cn.execute("INSERT INTO items (office_id, label) VALUES (1,'a-1'),(1,'a-2'),(2,'b-1')")


@pytest.fixture(scope="module")
def app_conn():
    _setup(SUPER_URL)
    # role attributes: must be non-super and NOBYPASSRLS, else RLS is silently ignored
    with psycopg.connect(SUPER_URL, autocommit=True) as cn:
        assert cn.execute(
            f"SELECT rolsuper, rolbypassrls FROM pg_roles WHERE rolname='{APP_ROLE}'"
        ).fetchone() == (False, False)
    with psycopg.connect(_app_url(SUPER_URL)) as cn:
        yield cn


def _labels(cn, office_id: int) -> list[str]:
    with cn.transaction():
        cn.execute(set_local_office_sql(office_id))
        return sorted(r[0] for r in cn.execute("SELECT label FROM items").fetchall())


def _write_as(cn, office_id: int, sql: str) -> None:
    """A single write scoped to ``office_id``; may raise on the RLS WITH CHECK."""
    with cn.transaction():
        cn.execute(set_local_office_sql(office_id))
        cn.execute(sql)


def test_cross_tenant_read_isolation(app_conn):
    assert _labels(app_conn, 1) == ["a-1", "a-2"]
    assert _labels(app_conn, 2) == ["b-1"]


def test_fail_closed_when_guc_unset(app_conn):
    # a fresh transaction with no SET LOCAL must see zero rows and NOT raise
    with app_conn.transaction():
        assert app_conn.execute("SELECT count(*) FROM items").fetchone()[0] == 0


def test_no_guc_leak_across_transactions(app_conn):
    # after scoping to office 2, the next transaction without a SET must not inherit it
    _labels(app_conn, 2)
    with app_conn.transaction():
        assert app_conn.execute("SELECT count(*) FROM items").fetchone()[0] == 0


def test_with_check_blocks_insert_into_other_office(app_conn):
    with pytest.raises(psycopg.errors.InsufficientPrivilege):
        _write_as(app_conn, 1, "INSERT INTO items (office_id, label) VALUES (2, 'evil')")


def test_same_office_insert_allowed(app_conn):
    _write_as(app_conn, 1, "INSERT INTO items (office_id, label) VALUES (1, 'a-new')")
    assert "a-new" in set(_labels(app_conn, 1))


def test_with_check_blocks_moving_row_to_other_office(app_conn):
    with pytest.raises(psycopg.errors.InsufficientPrivilege):
        _write_as(app_conn, 1, "UPDATE items SET office_id = 2 WHERE label = 'a-1'")
