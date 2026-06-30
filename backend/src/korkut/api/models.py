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

from sqlalchemy import ForeignKey, Index, String, Text, func, text
from sqlalchemy.orm import Mapped, mapped_column, relationship

from korkut.core.db import Base


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


class OfficeModel(Base):
    """An office's custom LLM, defined by the owner in Özelleştir (one row per office).

    When `enabled`, every chat/draft for the office routes to this model instead of the built-in
    default. `is_local` True means the endpoint runs on-prem (Ollama etc.) → the router skips
    redaction (data never leaves the machine); False means an external endpoint, so cloud-bound
    text is still redacted. Any OpenAI-compatible Chat Completions endpoint works.
    """

    __tablename__ = "office_models"
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int] = mapped_column(ForeignKey("offices.id"), unique=True, index=True)
    enabled: Mapped[bool] = mapped_column(default=False)
    is_local: Mapped[bool] = mapped_column(default=False)
    model: Mapped[str] = mapped_column(String(200), default="")  # e.g. "llama-3.3-70b-versatile"
    base_url: Mapped[str] = mapped_column(String(500), default="")  # OpenAI-compatible /v1 URL
    api_key: Mapped[str] = mapped_column(Text, default="")  # blank for keyless local endpoints
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


class TemplateDefault(Base):
    """The built-in theme (example markdown) for a petition/contract type.

    Synced from the `templates/*/examples/*.md` files on every startup (keyed by `source_hash`
    so unchanged files are skipped). The LLM draws inspiration from this theme when drafting —
    there is no placeholder substitution. An office or lawyer can shadow it with a
    `TemplateOverride`.
    """

    __tablename__ = "template_defaults"
    id: Mapped[int] = mapped_column(primary_key=True)
    type_id: Mapped[str] = mapped_column(String(80), unique=True, index=True)
    kind: Mapped[str] = mapped_column(String(16))  # "dilekce" | "sozlesme"
    content: Mapped[str] = mapped_column(Text, default="")  # theme markdown
    source_hash: Mapped[str] = mapped_column(String(64), default="")  # skip unchanged files
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())


class TemplateOverride(Base):
    """An office- or lawyer-customized theme for one type. Never touched by the file sync.

    Exactly one scope FK is set: `user_id` → personal override, `office_id` (with `user_id` NULL)
    → office-wide override. Resolution order is user > office > default (see `effective_theme`).
    """

    __tablename__ = "template_overrides"
    __table_args__ = (
        # One personal override per (user, type); one office override per (office, type).
        Index(
            "uq_tpl_override_user",
            "user_id",
            "type_id",
            unique=True,
            sqlite_where=text("user_id IS NOT NULL"),
            postgresql_where=text("user_id IS NOT NULL"),
        ),
        Index(
            "uq_tpl_override_office",
            "office_id",
            "type_id",
            unique=True,
            sqlite_where=text("user_id IS NULL"),
            postgresql_where=text("user_id IS NULL"),
        ),
    )
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int | None] = mapped_column(
        ForeignKey("offices.id"), index=True, default=None
    )
    user_id: Mapped[int | None] = mapped_column(ForeignKey("users.id"), index=True, default=None)
    type_id: Mapped[str] = mapped_column(String(80), index=True)
    content: Mapped[str] = mapped_column(Text, default="")
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())


class AuthEvent(Base):
    """Append-only audit trail for authentication events (KVKK / incident response)."""

    __tablename__ = "auth_events"
    id: Mapped[int] = mapped_column(primary_key=True)
    username: Mapped[str | None] = mapped_column(String(64), index=True, default=None)
    event: Mapped[str] = mapped_column(String(40))
    ip: Mapped[str | None] = mapped_column(String(64), default=None)
    detail: Mapped[str | None] = mapped_column(String(200), default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now(), index=True)
