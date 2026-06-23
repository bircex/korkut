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
        _migrate(_engine)
        db.init_db(_engine)
        _Session = sessionmaker(_engine, expire_on_commit=False)
    return _engine


def _migrate(engine) -> None:
    """Bring an existing DB up to the current schema.

    The chat tables moved from a flat per-user `messages` table to shared `conversations` +
    session-scoped `messages`. Chat history is disposable trial data, so if we detect the old
    `messages` schema (no `conversation_id`), we drop the chat tables and let `init_db` recreate
    them. User accounts, 2FA, recovery codes and the audit log are preserved.
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


def session() -> Session:
    engine()
    assert _Session is not None
    return _Session()
