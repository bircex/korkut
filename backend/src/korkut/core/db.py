"""Application database engine + the shared declarative Base.

All ORM models live in `korkut.api.models` and share this `Base`, so `init_db(engine)` creates
them together in one metadata. Engine/session helpers are URL-driven so tests run against
in-memory SQLite while production uses Postgres (ARCHITECTURE.md §6).
"""

from __future__ import annotations

from sqlalchemy import create_engine
from sqlalchemy.orm import DeclarativeBase


class Base(DeclarativeBase):
    pass


def make_engine(url: str = "sqlite+pysqlite:///:memory:"):
    """Create an engine. Production passes a Postgres URL; tests use in-memory SQLite."""
    return create_engine(url, future=True)


def init_db(engine) -> None:
    Base.metadata.create_all(engine)
