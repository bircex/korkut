"""Run Alembic migrations at startup so the DB schema always matches the models.

`create_all` only creates missing tables — it never adds or drops columns on existing ones, so
every model change used to drift the persisted SQLite/Postgres schema and crash the next query.
Alembic owns the schema instead: `run_migrations` brings any real database up to `head`.

Baseline handling: a database that predates Alembic has no `alembic_version` table. If it already
holds app tables, it is stamped at the base revision (assumed to match that first schema) and then
upgraded, so later migrations apply on top without trying to recreate existing tables. A fresh
(empty) database is built from scratch by the upgrade itself.
"""

from __future__ import annotations

from pathlib import Path

from alembic.config import Config
from alembic.script import ScriptDirectory
from sqlalchemy import inspect

from alembic import command

# backend/src/korkut/core/migrate.py → parents[3] == backend/
_BACKEND_DIR = Path(__file__).resolve().parents[3]


def _config(url: str) -> Config:
    cfg = Config(str(_BACKEND_DIR / "alembic.ini"))
    cfg.set_main_option("script_location", str(_BACKEND_DIR / "alembic"))
    cfg.set_main_option("sqlalchemy.url", url)
    return cfg


def run_migrations(engine) -> None:
    """Bring `engine`'s database up to the latest revision (idempotent)."""
    cfg = _config(str(engine.url))
    script = ScriptDirectory.from_config(cfg)
    base = script.get_base()  # the down_revision=None migration, or None if there are none yet
    tables = set(inspect(engine).get_table_names())
    # Existing pre-Alembic database: adopt the base revision so its DDL is not re-run.
    if base and "alembic_version" not in tables and "users" in tables:
        command.stamp(cfg, base)
    command.upgrade(cfg, "head")
