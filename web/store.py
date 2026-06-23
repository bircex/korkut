"""Database wiring for the web app.

URL-driven so the trial can run on a single SQLite file (default) while production points
KORKUT_DB_URL at the office Postgres. Importing `web.models` here guarantees the user/message
tables are registered on `Base.metadata` before `init_db` runs.
"""

from __future__ import annotations

import os
from pathlib import Path

from sqlalchemy.orm import Session, sessionmaker

from core import db
from web import models  # noqa: F401  (registers User/Message on Base.metadata)

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
        db.init_db(_engine)
        _Session = sessionmaker(_engine, expire_on_commit=False)
    return _engine


def session() -> Session:
    engine()
    assert _Session is not None
    return _Session()
