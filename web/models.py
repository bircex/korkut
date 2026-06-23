"""Web-app data models — office users, their chat history, 2FA, and an auth audit trail.

These share `core.db.Base`, so `core.db.init_db(engine)` creates them alongside the
case/hearing/petition tables. There is deliberately NO self-service signup: accounts are
created only by an administrator via `python -m web.adduser` (closed enrolment).
"""

from __future__ import annotations

from datetime import datetime

from sqlalchemy import ForeignKey, String, Text, func
from sqlalchemy.orm import Mapped, mapped_column, relationship

from core.db import Base


class User(Base):
    __tablename__ = "users"
    id: Mapped[int] = mapped_column(primary_key=True)
    username: Mapped[str] = mapped_column(String(64), unique=True, index=True)
    full_name: Mapped[str | None] = mapped_column(String(200), default=None)
    password_hash: Mapped[str] = mapped_column(String(255))
    is_active: Mapped[bool] = mapped_column(default=True)

    # Two-factor (TOTP). Mandatory: a user with no confirmed secret must enrol before chatting.
    totp_secret: Mapped[str | None] = mapped_column(String(64), default=None)
    totp_confirmed_at: Mapped[datetime | None] = mapped_column(default=None)
    totp_last_counter: Mapped[int] = mapped_column(default=0)  # anti-replay

    # Server-side session revocation: bumping this invalidates every outstanding cookie.
    token_epoch: Mapped[int] = mapped_column(default=1)

    # Brute-force throttling (persisted so a restart can't reset a lockout).
    failed_attempts: Mapped[int] = mapped_column(default=0)
    locked_until: Mapped[datetime | None] = mapped_column(default=None)

    created_at: Mapped[datetime] = mapped_column(default=func.now())

    messages: Mapped[list["Message"]] = relationship(
        back_populates="user", cascade="all, delete-orphan"
    )
    recovery_codes: Mapped[list["RecoveryCode"]] = relationship(
        back_populates="user", cascade="all, delete-orphan"
    )

    @property
    def has_2fa(self) -> bool:
        return self.totp_secret is not None and self.totp_confirmed_at is not None


class RecoveryCode(Base):
    __tablename__ = "recovery_codes"
    id: Mapped[int] = mapped_column(primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    code_hash: Mapped[str] = mapped_column(String(255))
    used_at: Mapped[datetime | None] = mapped_column(default=None)

    user: Mapped[User] = relationship(back_populates="recovery_codes")


class Message(Base):
    __tablename__ = "messages"
    id: Mapped[int] = mapped_column(primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    role: Mapped[str] = mapped_column(String(16))  # "user" | "assistant"
    content: Mapped[str] = mapped_column(Text)
    created_at: Mapped[datetime] = mapped_column(default=func.now())

    user: Mapped[User] = relationship(back_populates="messages")


class AuthEvent(Base):
    """Append-only audit trail for authentication events (KVKK / incident response)."""

    __tablename__ = "auth_events"
    id: Mapped[int] = mapped_column(primary_key=True)
    username: Mapped[str | None] = mapped_column(String(64), index=True, default=None)
    event: Mapped[str] = mapped_column(String(40))
    ip: Mapped[str | None] = mapped_column(String(64), default=None)
    detail: Mapped[str | None] = mapped_column(String(200), default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now(), index=True)
