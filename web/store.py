"""Database wiring for the web app.

URL-driven so the trial can run on a single SQLite file (default) while production points
KORKUT_DB_URL at the office Postgres. Importing `web.models` here guarantees the user/message
tables are registered on `Base.metadata` before `init_db` runs.
"""

from __future__ import annotations

import os
from pathlib import Path

from sqlalchemy import inspect, text
from sqlalchemy.orm import Session, sessionmaker

from core import db
from web import models  # noqa: F401  (registers the models on Base.metadata)

_DEFAULT_DIR = Path(__file__).resolve().parent.parent / "data"
_DEFAULT_URL = f"sqlite+pysqlite:///{_DEFAULT_DIR / 'korkut.db'}"

_engine = None
_Session: sessionmaker[Session] | None = None


def _url() -> str:
    return os.getenv("KORKUT_DB_URL", _DEFAULT_URL)


def engine():
    global _engine, _Session
    if _engine is None:
        url = _url()
        if url.startswith("sqlite") and "memory" not in url:
            _DEFAULT_DIR.mkdir(parents=True, exist_ok=True)
        _engine = db.make_engine(url)
        _migrate_legacy(_engine)   # pre-init: drop obsolete chat tables so init_db rebuilds them
        db.init_db(_engine)        # create any missing tables (offices, documents, …)
        _migrate_tenant(_engine)   # post-init: add multi-tenant columns to existing tables
        _Session = sessionmaker(_engine, expire_on_commit=False)
    return _engine


def _migrate_legacy(engine) -> None:
    """Old per-user `messages` schema (no `conversation_id`) → drop chat tables; init_db rebuilds.
    Chat history is disposable; accounts/2FA/recovery/audit are preserved.
    """
    insp = inspect(engine)
    tables = set(insp.get_table_names())
    if "messages" not in tables:
        return
    cols = {c["name"] for c in insp.get_columns("messages")}
    if "conversation_id" not in cols:
        with engine.begin() as conn:
            conn.execute(text("DROP TABLE messages"))
            if "conversations" in tables:
                conn.execute(text("DROP TABLE conversations"))


def _migrate_tenant(engine) -> None:
    """Multi-tenant upgrade: add `office_id`/`role` to existing rows WITHOUT data loss.

    A pre-tenant DB has a `users` table with no `office_id`. We create a default office, add the
    columns (SQLite ADD COLUMN), assign every existing user/conversation to that office, and make
    the earliest user its owner. Idempotent (skips once `office_id` exists).
    """
    insp = inspect(engine)
    if "users" not in insp.get_table_names():
        return  # fresh DB — init_db already created the correct schema
    if "office_id" in {c["name"] for c in insp.get_columns("users")}:
        return  # already migrated
    with engine.begin() as conn:
        oid = conn.execute(text("SELECT id FROM offices ORDER BY id LIMIT 1")).scalar()
        if oid is None:
            conn.execute(text("INSERT INTO offices (name, created_at) VALUES ('Cankar Hukuk', CURRENT_TIMESTAMP)"))
            oid = conn.execute(text("SELECT id FROM offices ORDER BY id LIMIT 1")).scalar()
        conn.execute(text("ALTER TABLE users ADD COLUMN office_id INTEGER"))
        conn.execute(text("ALTER TABLE users ADD COLUMN role VARCHAR(16) DEFAULT 'member'"))
        conn.execute(text(f"UPDATE users SET office_id = {oid}"))
        conn.execute(text("UPDATE users SET role = 'owner' WHERE id = (SELECT id FROM users ORDER BY id LIMIT 1)"))
        if "conversations" in insp.get_table_names() and \
                "office_id" not in {c["name"] for c in insp.get_columns("conversations")}:
            conn.execute(text("ALTER TABLE conversations ADD COLUMN office_id INTEGER"))
            conn.execute(text(f"UPDATE conversations SET office_id = {oid}"))


def session() -> Session:
    engine()
    assert _Session is not None
    return _Session()
