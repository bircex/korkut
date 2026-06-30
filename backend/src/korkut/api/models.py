"""Web-app data models — office users, shared chat sessions, 2FA, the matter domain, and audit.

All models share `core.db.Base`, so `core.db.init_db(engine)` creates them in one metadata. The
app is MULTI-TENANT: each `Office` is a tenant. A person self-registers an office (becoming its
`owner`) and adds members; usernames stay globally unique so login is username+password.

The matter domain (`Client`, `Matter`, `Hearing`) is the spine of the lawyer's work — research,
drafts and hearings hang off a matter. It is office-scoped like every other table here and is
exposed to the agent via `korkut.api.matter_tools`.

Chat sessions are shared WITHIN an office: every member sees and can open every session
(Conversation) belonging to their `office_id`, but never another office's. Messages belong to
a Conversation, not to a single user.
"""

from __future__ import annotations

from datetime import date as _date
from datetime import datetime
from datetime import time as _time

from sqlalchemy import JSON, Date, ForeignKey, Index, String, Text, Time, UniqueConstraint, func, text
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
    # Optional link to a matter (dosya): a chat can belong to a case file. Null = unfiled.
    matter_id: Mapped[int | None] = mapped_column(ForeignKey("matters.id"), default=None, index=True)
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


class OfficeChannel(Base):
    """An office's credentials for one outbound channel (email now; Telegram/SMTP/… later).

    Generic by design: `config` is a JSON blob whose keys are channel-specific (e.g. email:
    from_address, from_name, app_password, host, port), so new integrations need no schema change.
    See `korkut.messaging`. Stored on the office's own DB (self-hosted), like `OfficeModel`.
    """

    __tablename__ = "office_channels"
    __table_args__ = (UniqueConstraint("office_id", "kind", name="uq_office_channel"),)
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int] = mapped_column(ForeignKey("offices.id"), index=True)
    kind: Mapped[str] = mapped_column(String(32))  # "email", "telegram", …
    enabled: Mapped[bool] = mapped_column(default=False)
    config: Mapped[dict] = mapped_column(JSON, default=dict)
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())


class OutboundMessage(Base):
    """A message Korkut proposed to send — the channel-agnostic outbox + schedule queue.

    Lifecycle: ``draft`` (proposed, awaiting the lawyer's confirm) → ``queued`` (confirmed,
    waiting for ``send_at``) → ``sent`` | ``failed``; or ``cancelled``. ``send_at`` null/past means
    send on confirm; a future value schedules it (the dispatcher sends due ones). The same row
    serves email, Telegram and reminders — only `kind`/`to_addr` differ.
    """

    __tablename__ = "outbound_messages"
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int] = mapped_column(ForeignKey("offices.id"), index=True)
    user_id: Mapped[int | None] = mapped_column(ForeignKey("users.id"), default=None)  # author
    # Optional link to a matter (dosya): correspondence filed under a case file. Null = unfiled.
    matter_id: Mapped[int | None] = mapped_column(ForeignKey("matters.id"), default=None, index=True)
    kind: Mapped[str] = mapped_column(String(32), default="email")
    to_addr: Mapped[str] = mapped_column(String(320))
    subject: Mapped[str] = mapped_column(String(500), default="")
    body: Mapped[str] = mapped_column(Text, default="")
    send_at: Mapped[datetime | None] = mapped_column(default=None, index=True)
    status: Mapped[str] = mapped_column(String(16), default="draft", index=True)
    error: Mapped[str | None] = mapped_column(Text, default=None)
    attempts: Mapped[int] = mapped_column(default=0)
    created_at: Mapped[datetime] = mapped_column(default=func.now())
    sent_at: Mapped[datetime | None] = mapped_column(default=None)


class IntegrationLink(Base):
    """Binds a messaging-channel sender to a korkut `User`, so two-way integrations know which
    lawyer is talking (`korkut.integrations`).

    A lawyer cannot just message the bot and be trusted — Telegram/WhatsApp/email all hand us only
    an opaque sender id, never korkut credentials. So linking is an explicit proof-of-ownership:
    the lawyer generates a one-time `code` in the web UI (a `pending` row), then sends that code
    FROM the channel; the gateway captures the sender's `external_id` and flips the row to
    `active`. Inbound auth is then a lookup by (channel, external_id) among active links.

    `external_id` is channel-specific: Telegram numeric user id, WhatsApp jid, or the sender's
    e-mail address (lower-cased). One external identity maps to at most one user.
    """

    __tablename__ = "integration_links"
    __table_args__ = (UniqueConstraint("channel", "external_id", name="uq_integration_external"),)
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int] = mapped_column(ForeignKey("offices.id"), index=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    channel: Mapped[str] = mapped_column(String(32))  # "telegram" | "whatsapp" | "email"
    external_id: Mapped[str | None] = mapped_column(String(320), default=None, index=True)
    code: Mapped[str | None] = mapped_column(String(16), default=None)  # one-time, while pending
    status: Mapped[str] = mapped_column(String(16), default="pending")  # pending | active
    created_at: Mapped[datetime] = mapped_column(default=func.now())
    linked_at: Mapped[datetime | None] = mapped_column(default=None)


class IntegrationThread(Base):
    """Maps one channel conversation (a Telegram/WhatsApp chat, or an e-mail correspondent) to a
    korkut `Conversation`, so an incoming chat keeps its history and shows up in the web UI like
    any other session. Keyed by (channel, thread_id) — the channel's own chat identifier.
    """

    __tablename__ = "integration_threads"
    __table_args__ = (UniqueConstraint("channel", "thread_id", name="uq_integration_thread"),)
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int] = mapped_column(ForeignKey("offices.id"), index=True)
    channel: Mapped[str] = mapped_column(String(32))
    thread_id: Mapped[str] = mapped_column(String(190))
    conversation_id: Mapped[int] = mapped_column(ForeignKey("conversations.id"), index=True)
    created_at: Mapped[datetime] = mapped_column(default=func.now())


# ---------------- matter domain (dosya/dava omurgası) ----------------
# The case file is the spine of a lawyer's work: research, drafts, hearings and correspondence
# all hang off a Matter. Office-scoped like every other tenant table. Exposed to the agent via
# korkut.api.matter_tools and to the UI via /api/matters.


class Client(Base):
    """A client (müvekkil) of the office."""

    __tablename__ = "clients"
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int] = mapped_column(ForeignKey("offices.id"), index=True)
    full_name: Mapped[str] = mapped_column(String(200))
    national_id: Mapped[str | None] = mapped_column(String(11), default=None)
    phone: Mapped[str | None] = mapped_column(String(20), default=None)
    email: Mapped[str | None] = mapped_column(String(200), default=None)
    address: Mapped[str | None] = mapped_column(String(500), default=None)
    notes: Mapped[str | None] = mapped_column(Text, default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())


class Matter(Base):
    """A legal matter/file (dava/dosya). The office's work attaches here.

    `client_id` optionally links the müvekkil; `status` is open|closed (free text for now).
    Court/file-number/case-type/counterparty mirror the fields a lawyer tracks per dosya.
    """

    __tablename__ = "matters"
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int] = mapped_column(ForeignKey("offices.id"), index=True)
    created_by: Mapped[int | None] = mapped_column(ForeignKey("users.id"), default=None)
    title: Mapped[str] = mapped_column(String(200))
    client_id: Mapped[int | None] = mapped_column(
        ForeignKey("clients.id"), default=None, index=True
    )
    file_no: Mapped[str | None] = mapped_column(String(50), default=None)  # esas no
    court: Mapped[str | None] = mapped_column(String(200), default=None)  # mahkeme
    case_type: Mapped[str | None] = mapped_column(String(100), default=None)  # dava türü
    counterparty: Mapped[str | None] = mapped_column(String(200), default=None)  # karşı taraf
    status: Mapped[str] = mapped_column(String(30), default="open")
    opened_at: Mapped[_date | None] = mapped_column(Date, default=None)
    notes: Mapped[str | None] = mapped_column(Text, default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())

    client: Mapped[Client | None] = relationship()
    hearings: Mapped[list[Hearing]] = relationship(
        back_populates="matter", cascade="all, delete-orphan", order_by="Hearing.date"
    )
    deadlines: Mapped[list[Deadline]] = relationship(
        back_populates="matter", cascade="all, delete-orphan", order_by="Deadline.due_date"
    )


class Hearing(Base):
    """A hearing (duruşma) on a matter's calendar — the Phase-1 deadline primitive."""

    __tablename__ = "hearings"
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int] = mapped_column(ForeignKey("offices.id"), index=True)
    matter_id: Mapped[int] = mapped_column(ForeignKey("matters.id"), index=True)
    date: Mapped[_date] = mapped_column(Date, index=True)
    time: Mapped[_time | None] = mapped_column(Time, default=None)  # fixed: was Date in old schema
    location: Mapped[str | None] = mapped_column(String(200), default=None)
    type: Mapped[str | None] = mapped_column(String(100), default=None)
    result: Mapped[str | None] = mapped_column(Text, default=None)
    reminder_status: Mapped[str] = mapped_column(String(30), default="pending")
    notes: Mapped[str | None] = mapped_column(Text, default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())

    matter: Mapped[Matter] = relationship(back_populates="hearings")


class Deadline(Base):
    """A procedural deadline (süre) on a matter — e.g. cevap, istinaf, temyiz süresi.

    Distinct from a hearing: a hearing is an event you attend, a deadline is a date by which an
    act must be done. `status` is pending|done. Surfaced alongside hearings in the office agenda.
    """

    __tablename__ = "deadlines"
    id: Mapped[int] = mapped_column(primary_key=True)
    office_id: Mapped[int] = mapped_column(ForeignKey("offices.id"), index=True)
    matter_id: Mapped[int] = mapped_column(ForeignKey("matters.id"), index=True)
    due_date: Mapped[_date] = mapped_column(Date, index=True)
    title: Mapped[str] = mapped_column(String(200))
    type: Mapped[str | None] = mapped_column(String(100), default=None)  # cevap, istinaf, temyiz…
    status: Mapped[str] = mapped_column(String(20), default="pending")  # pending | done
    notes: Mapped[str | None] = mapped_column(Text, default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())

    matter: Mapped[Matter] = relationship(back_populates="deadlines")


class AuthEvent(Base):
    """Append-only audit trail for authentication events (KVKK / incident response)."""

    __tablename__ = "auth_events"
    id: Mapped[int] = mapped_column(primary_key=True)
    username: Mapped[str | None] = mapped_column(String(64), index=True, default=None)
    event: Mapped[str] = mapped_column(String(40))
    ip: Mapped[str | None] = mapped_column(String(64), default=None)
    detail: Mapped[str | None] = mapped_column(String(200), default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now(), index=True)
