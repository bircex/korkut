"""Alembic runtime environment for Korkut.

The URL comes from KORKUT_DB_URL (falling back to the default SQLite file under the project
root) so migrations always hit the same database the app uses. `target_metadata` is the shared
declarative `Base`, with `korkut.api.models` imported so every table is registered before
autogenerate diffs the schema. SQLite needs batch mode to ALTER columns, so it is enabled there.
"""

from __future__ import annotations

import os

from sqlalchemy import engine_from_config, pool

from alembic import context
from korkut.api import models  # noqa: F401  (registers all ORM tables on Base.metadata)
from korkut.core.db import Base
from korkut.paths import DATA_DIR

config = context.config

target_metadata = Base.metadata


def _db_url() -> str:
    # Prefer a URL the programmatic caller injected on the config (run_migrations sets this), so
    # the CLI env var and the app's engine can never disagree; fall back to the env / default.
    from_cfg = config.get_main_option("sqlalchemy.url")
    if from_cfg:
        return from_cfg
    return os.getenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{DATA_DIR / 'korkut.db'}")


def run_migrations_offline() -> None:
    url = _db_url()
    context.configure(
        url=url,
        target_metadata=target_metadata,
        literal_binds=True,
        dialect_opts={"paramstyle": "named"},
        render_as_batch=url.startswith("sqlite"),
        compare_type=True,
    )
    with context.begin_transaction():
        context.run_migrations()


def run_migrations_online() -> None:
    section = config.get_section(config.config_ini_section, {})
    section["sqlalchemy.url"] = _db_url()
    connectable = engine_from_config(section, prefix="sqlalchemy.", poolclass=pool.NullPool)
    with connectable.connect() as connection:
        context.configure(
            connection=connection,
            target_metadata=target_metadata,
            render_as_batch=connection.dialect.name == "sqlite",
            compare_type=True,
        )
        with context.begin_transaction():
            context.run_migrations()


if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()
