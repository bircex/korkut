"""Web-app data models — office users, shared chat sessions, 2FA, and an auth audit trail.

These share `core.db.Base`, so `core.db.init_db(engine)` creates them alongside the
case/hearing/petition tables. The app is MULTI-TENANT: each `Office` is a tenant. A person
self-registers an office (becoming its `owner`) and adds members; usernames stay globally
unique so login is username+password.

Chat sessions are shared WITHIN an office: every member sees and can open every session
(Conversation) belonging to their `office_id`, but never another office's. Messages belong to
a Conversation, not to a single user.
"""

from __future__ import annotations

from datetime import datetime

from sqlalchemy import ForeignKey, String, Text, func
from sqlalchemy.orm import Mapped, mapped_column, relationship

from core.db import Base


class Office(Base):
    """A law office — the tenant. Members share every session/document under their office."""

    __tablename__ = "offices"
    id: Mapped[int] = mapped_column(primary_key=True)
    name: Mapped[str] = mapped_column(String(200))
    created_at: Mapped[datetime] = mapped_column(default=func.now())


class StyleProfile(Base):
    """A drafting-style profile for an office OR a single lawyer (exactly one of the FKs is set).

    Two layers merge into a lawyer's effective style: deterministic blocks (`letterhead`,
    `signature`) override (lawyer wins over office) and are inserted into the document directly —
    never sent to the cloud, so names/addresses are exact and skip redaction. Generative text
    (`tone`, `boilerplate`) is the office house-style + the lawyer's, both injected into the draft
    prompt so Korkut writes in their voice.
    """

    __tablename__ = "style_profiles"
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int | None] = mapped_column(
        ForeignKey("offices.id"), unique=True, default=None
    )
    user_id: Mapped[int | None] = mapped_column(ForeignKey("users.id"), unique=True, default=None)
    letterhead: Mapped[str] = mapped_column(Text, default="")  # antet (deterministik, üstte)
    signature: Mapped[str] = mapped_column(
        Text, default=""
    )  # imza bloğu (deterministik, dilekçe sonunda)
    tone: Mapped[str] = mapped_column(Text, default="")  # serbest üslup talimatı (üretken)
    boilerplate: Mapped[str] = mapped_column(Text, default="")  # tercih edilen kalıplar (üretken)
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())


class User(Base):
    __tablename__ = "users"
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int] = mapped_column(ForeignKey("offices.id"), index=True)
    role: Mapped[str] = mapped_column(String(16), default="member")  # "owner" | "member"
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

    recovery_codes: Mapped[list[RecoveryCode]] = relationship(
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


class Conversation(Base):
    """A chat session, shared across the whole office (any user can open any session)."""

    __tablename__ = "conversations"
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int] = mapped_column(ForeignKey("offices.id"), index=True)
    title: Mapped[str] = mapped_column(String(120), default="Yeni sohbet")
    created_by: Mapped[int | None] = mapped_column(ForeignKey("users.id"), default=None)
    created_by_name: Mapped[str | None] = mapped_column(String(200), default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())

    messages: Mapped[list[Message]] = relationship(
        back_populates="conversation", cascade="all, delete-orphan", order_by="Message.id"
    )


class Message(Base):
    __tablename__ = "messages"
    id: Mapped[int] = mapped_column(primary_key=True)
    conversation_id: Mapped[int] = mapped_column(ForeignKey("conversations.id"), index=True)
    user_id: Mapped[int | None] = mapped_column(ForeignKey("users.id"), default=None)  # author
    role: Mapped[str] = mapped_column(String(16))  # "user" | "assistant"
    content: Mapped[str] = mapped_column(Text)
    created_at: Mapped[datetime] = mapped_column(default=func.now())

    conversation: Mapped[Conversation] = relationship(back_populates="messages")


class Document(Base):
    """A legal document (petition draft) attached to a session — the Canvas content.

    One document per session in v1. `content_md` is the current accepted markdown (source of
    truth); WYSIWYG editing and UDF/DOCX export derive from it.
    """

    __tablename__ = "documents"
    id: Mapped[int] = mapped_column(primary_key=True)
    conversation_id: Mapped[int] = mapped_column(
        ForeignKey("conversations.id"), unique=True, index=True
    )
    title: Mapped[str] = mapped_column(String(200), default="Belge")
    content_md: Mapped[str] = mapped_column(Text, default="")
    status: Mapped[str] = mapped_column(String(30), default="draft")
    created_at: Mapped[datetime] = mapped_column(default=func.now())
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())

    versions: Mapped[list[DocumentVersion]] = relationship(
        back_populates="document", cascade="all, delete-orphan", order_by="DocumentVersion.id"
    )


class DocumentVersion(Base):
    """An accepted snapshot of a document (history / undo / audit)."""

    __tablename__ = "document_versions"
    id: Mapped[int] = mapped_column(primary_key=True)
    document_id: Mapped[int] = mapped_column(ForeignKey("documents.id"), index=True)
    content_md: Mapped[str] = mapped_column(Text)
    author: Mapped[str] = mapped_column(String(64))  # "korkut" | "<username>"
    note: Mapped[str | None] = mapped_column(String(200), default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())

    document: Mapped[Document] = relationship(back_populates="versions")


class ProposedEdit(Base):
    """Korkut's proposed new content, pending the lawyer's accept/revert (one pending at a time)."""

    __tablename__ = "proposed_edits"
    id: Mapped[int] = mapped_column(primary_key=True)
    document_id: Mapped[int] = mapped_column(ForeignKey("documents.id"), index=True)
    content_md: Mapped[str] = mapped_column(Text)
    summary: Mapped[str | None] = mapped_column(String(300), default=None)
    status: Mapped[str] = mapped_column(String(20), default="pending")  # pending|accepted|reverted
    created_at: Mapped[datetime] = mapped_column(default=func.now())


class AuthEvent(Base):
    """Append-only audit trail for authentication events (KVKK / incident response)."""

    __tablename__ = "auth_events"
    id: Mapped[int] = mapped_column(primary_key=True)
    username: Mapped[str | None] = mapped_column(String(64), index=True, default=None)
    event: Mapped[str] = mapped_column(String(40))
    ip: Mapped[str | None] = mapped_column(String(64), default=None)
    detail: Mapped[str | None] = mapped_column(String(200), default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now(), index=True)
