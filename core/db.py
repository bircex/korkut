"""Application database (Postgres in production; ARCHITECTURE.md §6).

SQLAlchemy 2.0 models for the office's structured data: client, case, hearing, petition.
A separate database from Mattermost's. Engine/session helpers are URL-driven so tests run
against in-memory SQLite while production uses Postgres.

Approval invariant (ARCHITECTURE.md §9): a petition is created as `draft`, becomes
`pending_approval`, and only `approved` after the lawyer confirms — never written as final
without approval.
"""

from __future__ import annotations

from datetime import date as _date
from datetime import datetime

from sqlalchemy import Date, ForeignKey, String, Time, create_engine, func, select
from sqlalchemy.orm import DeclarativeBase, Mapped, Session, mapped_column, relationship

PETITION_DRAFT = "draft"
PETITION_PENDING = "pending_approval"
PETITION_APPROVED = "approved"


class Base(DeclarativeBase):
    pass


class Client(Base):
    __tablename__ = "clients"
    id: Mapped[int] = mapped_column(primary_key=True)
    full_name: Mapped[str] = mapped_column(String(200))
    national_id: Mapped[str | None] = mapped_column(String(11), default=None)
    phone: Mapped[str | None] = mapped_column(String(20), default=None)
    email: Mapped[str | None] = mapped_column(String(200), default=None)
    address: Mapped[str | None] = mapped_column(String(500), default=None)
    notes: Mapped[str | None] = mapped_column(default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())

    cases: Mapped[list["Case"]] = relationship(back_populates="client", cascade="all, delete-orphan")


class Case(Base):
    __tablename__ = "cases"
    id: Mapped[int] = mapped_column(primary_key=True)
    file_no: Mapped[str] = mapped_column(String(50), index=True)
    client_id: Mapped[int] = mapped_column(ForeignKey("clients.id"))
    court: Mapped[str | None] = mapped_column(String(200), default=None)
    case_type: Mapped[str | None] = mapped_column(String(100), default=None)
    counterparty: Mapped[str | None] = mapped_column(String(200), default=None)
    status: Mapped[str] = mapped_column(String(30), default="open")
    opened_at: Mapped[_date | None] = mapped_column(Date, default=None)
    notes: Mapped[str | None] = mapped_column(default=None)

    client: Mapped[Client] = relationship(back_populates="cases")
    hearings: Mapped[list["Hearing"]] = relationship(back_populates="case", cascade="all, delete-orphan")
    petitions: Mapped[list["Petition"]] = relationship(back_populates="case", cascade="all, delete-orphan")


class Hearing(Base):
    __tablename__ = "hearings"
    id: Mapped[int] = mapped_column(primary_key=True)
    case_id: Mapped[int] = mapped_column(ForeignKey("cases.id"))
    date: Mapped[_date] = mapped_column(Date, index=True)
    time: Mapped[_date | None] = mapped_column(Time, default=None)
    location: Mapped[str | None] = mapped_column(String(200), default=None)
    type: Mapped[str | None] = mapped_column(String(100), default=None)
    result: Mapped[str | None] = mapped_column(default=None)
    reminder_status: Mapped[str] = mapped_column(String(30), default="pending")
    notes: Mapped[str | None] = mapped_column(default=None)

    case: Mapped[Case] = relationship(back_populates="hearings")


class Petition(Base):
    __tablename__ = "petitions"
    id: Mapped[int] = mapped_column(primary_key=True)
    case_id: Mapped[int] = mapped_column(ForeignKey("cases.id"))
    type: Mapped[str | None] = mapped_column(String(100), default=None)
    file_path: Mapped[str | None] = mapped_column(String(500), default=None)
    status: Mapped[str] = mapped_column(String(30), default=PETITION_DRAFT)
    created_by: Mapped[str | None] = mapped_column(String(100), default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())

    case: Mapped[Case] = relationship(back_populates="petitions")


def make_engine(url: str = "sqlite+pysqlite:///:memory:"):
    """Create an engine. Production passes a Postgres URL; tests use in-memory SQLite."""
    return create_engine(url, future=True)


def init_db(engine) -> None:
    Base.metadata.create_all(engine)


def upcoming_hearings(session: Session, today: _date, days: int = 7) -> list[Hearing]:
    """Hearings from `today` through `today + days`, ordered by date then time."""
    from datetime import timedelta

    end = today + timedelta(days=days)
    stmt = (
        select(Hearing)
        .where(Hearing.date >= today, Hearing.date <= end)
        .order_by(Hearing.date, Hearing.time)
    )
    return list(session.scalars(stmt))
