"""Admin-side DB bootstrap for the multi-tenant Postgres deployment (idempotent).

Run once with a PRIVILEGED connection before the app starts (see deploy/docker-compose.saas.yml).
It builds the schema, fail-closed RLS, and the pre-tenant SECURITY DEFINER resolvers, then creates a
NON-SUPERUSER app role and grants it exactly what the runtime needs. The app then connects as that
role, so Postgres RLS is actually enforced — a superuser would bypass FORCE RLS. See
docs/SAAS_ARCHITECTURE.md §15.

Env:
  KORKUT_ADMIN_DB_URL     privileged DSN (CREATE ROLE / policies / functions)
  KORKUT_APP_DB_ROLE      app role name (default 'korkut_app')
  KORKUT_APP_DB_PASSWORD  app role password
"""

from __future__ import annotations

import os
import sys

from sqlalchemy import create_engine, text

from korkut.api import store
from korkut.core import db
from korkut.core.env import load_dotenv

_IDENT_OK = frozenset("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_")


def _ident(name: str) -> str:
    """Guard a role name used in DDL (it can't be a bound parameter). Env-supplied, so trusted."""
    if not name or any(c not in _IDENT_OK for c in name):
        raise SystemExit(f"invalid role name: {name!r}")
    return name


def main() -> int:
    load_dotenv()
    admin_url = os.environ.get("KORKUT_ADMIN_DB_URL")
    pw = os.environ.get("KORKUT_APP_DB_PASSWORD")
    if not admin_url or not pw:
        print("KORKUT_ADMIN_DB_URL and KORKUT_APP_DB_PASSWORD are required", file=sys.stderr)
        return 2
    role = _ident(os.getenv("KORKUT_APP_DB_ROLE", "korkut_app"))
    pw_lit = "'" + pw.replace("'", "''") + "'"  # DDL literal; role passwords can't be bound params

    eng = create_engine(admin_url, future=True)
    if eng.dialect.name != "postgresql":
        print("bootstrap is Postgres-only; the app builds SQLite itself", file=sys.stderr)
        return 2

    # 1. schema + fail-closed RLS + pre-tenant resolvers, all owned by the admin
    db.init_db(eng)
    store.apply_tenant_rls(eng)
    store.setup_pg_resolvers(eng)

    # 2. the non-superuser app role + least-privilege grants (RLS applies to it because it is
    #    NOSUPERUSER/NOBYPASSRLS; it can only reach other offices' rows through the resolvers)
    with eng.begin() as cn:
        cn.execute(
            text(
                f"DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='{role}') "
                f"THEN CREATE ROLE {role} LOGIN NOSUPERUSER NOBYPASSRLS; END IF; END $$;"
            )
        )
        cn.execute(text(f"ALTER ROLE {role} WITH LOGIN PASSWORD {pw_lit}"))
        cn.execute(text(f"GRANT USAGE ON SCHEMA public TO {role}"))
        cn.execute(
            text(f"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {role}")
        )
        cn.execute(text(f"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {role}"))
        cn.execute(
            text(f"GRANT EXECUTE ON FUNCTION resolve_office_for_signin(text, text) TO {role}")
        )
        cn.execute(text(f"GRANT EXECUTE ON FUNCTION resolve_office_for_invite(text) TO {role}"))

    # 3. seed the shared reference themes (idempotent; not tenant-scoped)
    store.sync_template_defaults(eng)
    eng.dispose()
    print(f"bootstrap ok: schema + RLS + resolvers ready; app role {role!r} granted")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
