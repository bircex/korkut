"""Web-app data models — office users, shared chat sessions, 2FA, the matter domain, and audit.

All models share `core.db.Base`, so `core.db.init_db(engine)` creates them in one metadata. The
app is SINGLE-TENANT: there is one `Office`, a singleton identity row (id, name). The first person
to register (becoming `owner`) names it and adds members; usernames stay globally unique so login
is username+password.

The matter domain (`Client`, `Matter`, `CalendarEvent`) is the spine of the lawyer's work —
research, drafts and calendar events hang off a matter. Exposed to the agent via
`korkut.api.matter_tools`.

Chat sessions are shared: every member sees and can open every session (Conversation). Messages
belong to a Conversation, not to a single user.
"""

from __future__ import annotations

from datetime import date as _date
from datetime import datetime
from decimal import Decimal

from sqlalchemy import (
    JSON,
    Date,
    ForeignKey,
    Index,
    Numeric,
    String,
    Text,
    UniqueConstraint,
    func,
    text,
)
from sqlalchemy.orm import Mapped, mapped_column, relationship

from korkut.core.db import Base
from korkut.core.tenancy import TenantMixin


class Office(Base):
    """The law office — a singleton identity row. Members share every session/document."""

    __tablename__ = "offices"
    id: Mapped[int] = mapped_column(primary_key=True)
    name: Mapped[str] = mapped_column(String(200))
    slug: Mapped[str | None] = mapped_column(String(200), unique=True, index=True, default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())


class StyleProfile(TenantMixin, Base):
    """A drafting-style profile for the office OR a single lawyer.

    The office profile is the row with `user_id IS NULL`; a personal profile sets `user_id`.
    Two layers merge into a lawyer's effective style: deterministic blocks (`letterhead`,
    `signature`) override (lawyer wins over office) and are inserted into the document directly —
    never sent to the cloud, so names/addresses are exact and skip redaction. Generative text
    (`tone`, `boilerplate`) is the office house-style + the lawyer's, both injected into the draft
    prompt so Korkut writes in their voice.
    """

    __tablename__ = "style_profiles"
    __table_args__ = (
        Index(
            "uq_style_office_default", "office_id", unique=True,
            sqlite_where=text("user_id IS NULL"), postgresql_where=text("user_id IS NULL"),
        ),
        Index(
            "uq_style_office_user", "office_id", "user_id", unique=True,
            sqlite_where=text("user_id IS NOT NULL"), postgresql_where=text("user_id IS NOT NULL"),
        ),
    )
    id: Mapped[int] = mapped_column(primary_key=True)
    user_id: Mapped[int | None] = mapped_column(ForeignKey("users.id"), default=None)
    letterhead: Mapped[str] = mapped_column(Text, default="")  # antet (deterministik, üstte)
    signature: Mapped[str] = mapped_column(
        Text, default=""
    )  # imza bloğu (deterministik, dilekçe sonunda)
    tone: Mapped[str] = mapped_column(Text, default="")  # serbest üslup talimatı (üretken)
    boilerplate: Mapped[str] = mapped_column(Text, default="")  # tercih edilen kalıplar (üretken)
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())


class OfficeModel(TenantMixin, Base):
    """The office's custom LLM, defined by the owner in Özelleştir (a singleton row).

    When `enabled`, every chat/draft routes to this model instead of the built-in default.
    `is_local` True means the endpoint runs on-prem (Ollama etc.) → the router skips redaction
    (data never leaves the machine); False means an external endpoint, so cloud-bound text is
    still redacted. Any OpenAI-compatible Chat Completions endpoint works.
    """

    __tablename__ = "office_models"
    id: Mapped[int] = mapped_column(primary_key=True)
    enabled: Mapped[bool] = mapped_column(default=False)
    is_local: Mapped[bool] = mapped_column(default=False)
    model: Mapped[str] = mapped_column(String(200), default="")  # e.g. "llama-3.3-70b-versatile"
    base_url: Mapped[str] = mapped_column(String(500), default="")  # OpenAI-compatible /v1 URL
    api_key: Mapped[str] = mapped_column(Text, default="")  # blank for keyless local endpoints
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())


class Persona(TenantMixin, Base):
    """The office's configurable assistant persona (the Kişilik UI). A singleton row.

    Augments — never replaces — the locked core persona in `brain/persona.py`. `tone` sets the
    assistant's voice, `expertise` names the office's focus areas, and `extra_rules` are house
    rules the owner adds. The core security/citation/approval rules live in code and cannot be
    overridden here. The reply language stays Turkish regardless.
    """

    __tablename__ = "personas"
    id: Mapped[int] = mapped_column(primary_key=True)
    tone: Mapped[str] = mapped_column(Text, default="")  # üslup/ses
    expertise: Mapped[str] = mapped_column(Text, default="")  # uzmanlık alanları
    extra_rules: Mapped[str] = mapped_column(Text, default="")  # ofisin ek kuralları
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())


class MemoryItem(TenantMixin, Base):
    """A durable note the office wants Korkut to keep in mind (the Hafıza UI).

    Office-wide, durable knowledge — house preferences, standing facts, reminders — injected into
    every prompt as context (and masked by the redactor for cloud calls, like any other content).

    `status` gates what reaches the model: a lawyer's manual note is ``active`` immediately, while a
    note the agent proposes via the `remember` tool is ``pending`` until a lawyer approves it in the
    Hafıza tab. Only ``active`` notes are injected — nothing about a client is remembered without a
    lawyer choosing to. Structured facts (TCKN, dates, parties) belong in the records, not here.
    """

    __tablename__ = "memory_items"
    id: Mapped[int] = mapped_column(primary_key=True)
    kind: Mapped[str] = mapped_column(String(16), default="note")  # "note" | "fact" | "preference"
    text: Mapped[str] = mapped_column(Text)
    status: Mapped[str] = mapped_column(String(16), default="active")  # "active" | "pending"
    created_by: Mapped[int | None] = mapped_column(ForeignKey("users.id"), default=None)
    created_by_name: Mapped[str | None] = mapped_column(String(200), default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())


class User(TenantMixin, Base):
    __tablename__ = "users"
    __table_args__ = (UniqueConstraint("office_id", "username", name="uq_user_office_username"),)
    id: Mapped[int] = mapped_column(primary_key=True)
    role: Mapped[str] = mapped_column(String(16), default="member")  # "owner" | "member"
    username: Mapped[str] = mapped_column(String(64), index=True)
    full_name: Mapped[str | None] = mapped_column(String(200), default=None)
    bar_registry_no: Mapped[str | None] = mapped_column(String(32), default=None)  # baro sicil no
    password_hash: Mapped[str] = mapped_column(String(255))
    is_active: Mapped[bool] = mapped_column(default=True)

    # Two-factor (TOTP). Optional per user: the owner toggles `require_2fa`; when on, a user with
    # no confirmed secret must enrol before chatting, otherwise password alone signs them in.
    require_2fa: Mapped[bool] = mapped_column(default=False)
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


class RecoveryCode(TenantMixin, Base):
    __tablename__ = "recovery_codes"
    id: Mapped[int] = mapped_column(primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    code_hash: Mapped[str] = mapped_column(String(255))
    used_at: Mapped[datetime | None] = mapped_column(default=None)

    user: Mapped[User] = relationship(back_populates="recovery_codes")


class Conversation(TenantMixin, Base):
    """A chat session, shared across the whole office (any user can open any session)."""

    __tablename__ = "conversations"
    id: Mapped[int] = mapped_column(primary_key=True)
    # Optional link to a matter (dosya): a chat can belong to a case file. Null = unfiled.
    matter_id: Mapped[int | None] = mapped_column(
        ForeignKey("matters.id"), default=None, index=True
    )
    title: Mapped[str] = mapped_column(String(120), default="Yeni sohbet")
    created_by: Mapped[int | None] = mapped_column(ForeignKey("users.id"), default=None)
    created_by_name: Mapped[str | None] = mapped_column(String(200), default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())

    messages: Mapped[list[Message]] = relationship(
        back_populates="conversation", cascade="all, delete-orphan", order_by="Message.id"
    )


class Message(TenantMixin, Base):
    __tablename__ = "messages"
    id: Mapped[int] = mapped_column(primary_key=True)
    conversation_id: Mapped[int] = mapped_column(ForeignKey("conversations.id"), index=True)
    user_id: Mapped[int | None] = mapped_column(ForeignKey("users.id"), default=None)  # author
    role: Mapped[str] = mapped_column(String(16))  # "user" | "assistant"
    content: Mapped[str] = mapped_column(Text)
    created_at: Mapped[datetime] = mapped_column(default=func.now())

    conversation: Mapped[Conversation] = relationship(back_populates="messages")


class Document(TenantMixin, Base):
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


class DocumentVersion(TenantMixin, Base):
    """An accepted snapshot of a document (history / undo / audit)."""

    __tablename__ = "document_versions"
    id: Mapped[int] = mapped_column(primary_key=True)
    document_id: Mapped[int] = mapped_column(ForeignKey("documents.id"), index=True)
    content_md: Mapped[str] = mapped_column(Text)
    author: Mapped[str] = mapped_column(String(64))  # "korkut" | "<username>"
    note: Mapped[str | None] = mapped_column(String(200), default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())

    document: Mapped[Document] = relationship(back_populates="versions")


class ProposedEdit(TenantMixin, Base):
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


class TemplateOverride(TenantMixin, Base):
    """An office- or lawyer-customized theme for one type. Never touched by the file sync.

    The office override is the row with `user_id IS NULL` for a `type_id`; a personal override
    sets `user_id`. Resolution order is user > office > default (see `effective_theme`).
    """

    __tablename__ = "template_overrides"
    __table_args__ = (
        # One personal override per (user, type).
        Index(
            "uq_tpl_override_user",
            "office_id",
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
    user_id: Mapped[int | None] = mapped_column(ForeignKey("users.id"), index=True, default=None)
    type_id: Mapped[str] = mapped_column(String(80), index=True)
    content: Mapped[str] = mapped_column(Text, default="")
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())


class OfficeChannel(TenantMixin, Base):
    """The office's credentials for one outbound channel (email now; Telegram/SMTP/… later).

    Generic by design: `config` is a JSON blob whose keys are channel-specific (e.g. email:
    from_address, from_name, app_password, host, port), so new integrations need no schema change.
    See `korkut.messaging`. Stored on the office's own DB (self-hosted), like `OfficeModel`.
    """

    __tablename__ = "office_channels"
    __table_args__ = (UniqueConstraint("office_id", "kind", name="uq_office_channel"),)
    id: Mapped[int] = mapped_column(primary_key=True)
    kind: Mapped[str] = mapped_column(String(32))  # "email", "telegram", …
    enabled: Mapped[bool] = mapped_column(default=False)
    config: Mapped[dict] = mapped_column(JSON, default=dict)
    updated_at: Mapped[datetime] = mapped_column(default=func.now(), onupdate=func.now())


class OutboundMessage(TenantMixin, Base):
    """A message Korkut proposed to send — the channel-agnostic outbox + schedule queue.

    Lifecycle: ``draft`` (proposed, awaiting the lawyer's confirm) → ``queued`` (confirmed,
    waiting for ``send_at``) → ``sent`` | ``failed``; or ``cancelled``. ``send_at`` null/past means
    send on confirm; a future value schedules it (the dispatcher sends due ones). The same row
    serves email, Telegram and reminders — only `kind`/`to_addr` differ.
    """

    __tablename__ = "outbound_messages"
    id: Mapped[int] = mapped_column(primary_key=True)
    user_id: Mapped[int | None] = mapped_column(ForeignKey("users.id"), default=None)  # author
    # Optional link to a matter (dosya): correspondence filed under a case file. Null = unfiled.
    matter_id: Mapped[int | None] = mapped_column(
        ForeignKey("matters.id"), default=None, index=True
    )
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


class IntegrationLink(TenantMixin, Base):
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
    __table_args__ = (
        UniqueConstraint("office_id", "channel", "external_id", name="uq_integration_external"),
    )
    id: Mapped[int] = mapped_column(primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"), index=True)
    channel: Mapped[str] = mapped_column(String(32))  # "telegram" | "whatsapp" | "email"
    external_id: Mapped[str | None] = mapped_column(String(320), default=None, index=True)
    code: Mapped[str | None] = mapped_column(String(16), default=None)  # one-time, while pending
    status: Mapped[str] = mapped_column(String(16), default="pending")  # pending | active
    created_at: Mapped[datetime] = mapped_column(default=func.now())
    linked_at: Mapped[datetime | None] = mapped_column(default=None)


class IntegrationThread(TenantMixin, Base):
    """Maps one channel conversation (a Telegram/WhatsApp chat, or an e-mail correspondent) to a
    korkut `Conversation`, so an incoming chat keeps its history and shows up in the web UI like
    any other session. Keyed by (channel, thread_id) — the channel's own chat identifier.
    """

    __tablename__ = "integration_threads"
    __table_args__ = (
        UniqueConstraint("office_id", "channel", "thread_id", name="uq_integration_thread"),
    )
    id: Mapped[int] = mapped_column(primary_key=True)
    channel: Mapped[str] = mapped_column(String(32))
    thread_id: Mapped[str] = mapped_column(String(190))
    conversation_id: Mapped[int] = mapped_column(ForeignKey("conversations.id"), index=True)
    created_at: Mapped[datetime] = mapped_column(default=func.now())


# ---------------- matter domain (dosya/dava omurgası) ----------------
# The case file is the spine of a lawyer's work: research, drafts, hearings and correspondence
# all hang off a Matter. Exposed to the agent via korkut.api.matter_tools and to the UI via
# /api/matters.


class Client(TenantMixin, Base):
    """A client (müvekkil) of the office."""

    __tablename__ = "clients"
    id: Mapped[int] = mapped_column(primary_key=True)
    full_name: Mapped[str] = mapped_column(String(200))
    national_id: Mapped[str | None] = mapped_column(String(11), default=None)
    phone: Mapped[str | None] = mapped_column(String(20), default=None)
    email: Mapped[str | None] = mapped_column(String(200), default=None)
    address: Mapped[str | None] = mapped_column(String(500), default=None)
    date_of_birth: Mapped[_date | None] = mapped_column(Date, default=None)  # doğum tarihi
    notes: Mapped[str | None] = mapped_column(Text, default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())


class Matter(TenantMixin, Base):
    """A legal matter/file (dava/dosya). The office's work attaches here.

    `client_id` optionally links the müvekkil; `status` is open|closed (free text for now).
    Court/file-number/case-type/counterparty mirror the fields a lawyer tracks per dosya.
    """

    __tablename__ = "matters"
    id: Mapped[int] = mapped_column(primary_key=True)
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
    events: Mapped[list[CalendarEvent]] = relationship(
        back_populates="matter", cascade="all, delete-orphan", order_by="CalendarEvent.start_at"
    )


class CalendarEvent(TenantMixin, Base):
    """A single entry on the office calendar (takvim) — the unified event primitive.

    One table for every kind of dated thing a law office tracks, distinguished by `category` and a
    finer `type`:

    * category="hearing"     → duruşma, keşif, bilirkişi, icra, ifade, arabuluculuk (yargısal)
    * category="deadline"    → cevap/istinaf/temyiz/itiraz süreleri (yasal süre)
    * category="appointment" → müvekkil görüşmesi, noter randevusu, iç toplantı
    * category="reminder"    → ödeme/vekalet/sözleşme hatırlatmaları, genel hatırlatma

    `start_at` is a full datetime; `all_day` marks day-scoped items (deadlines, all-day meetings) so
    the time part is ignored. Optional links: `matter_id` (dosya), `client_id` (müvekkil),
    `assignee_id` (sorumlu avukat). `status` is the lifecycle (planned|done|cancelled; deadlines use
    pending|done). `rrule` (RFC-5545) and the `remind_*` fields back later phases (recurrence and
    integration reminders) and stay null until then.
    """

    __tablename__ = "calendar_events"
    id: Mapped[int] = mapped_column(primary_key=True)
    category: Mapped[str] = mapped_column(String(20), default="appointment", index=True)
    type: Mapped[str | None] = mapped_column(String(100), default=None)  # finer subtype
    title: Mapped[str] = mapped_column(String(200))
    start_at: Mapped[datetime] = mapped_column(index=True)
    end_at: Mapped[datetime | None] = mapped_column(default=None)
    all_day: Mapped[bool] = mapped_column(default=False)
    matter_id: Mapped[int | None] = mapped_column(
        ForeignKey("matters.id"), default=None, index=True
    )
    client_id: Mapped[int | None] = mapped_column(
        ForeignKey("clients.id"), default=None, index=True
    )
    assignee_id: Mapped[int | None] = mapped_column(
        ForeignKey("users.id"), default=None, index=True
    )
    location: Mapped[str | None] = mapped_column(String(200), default=None)
    status: Mapped[str] = mapped_column(String(20), default="planned")  # planned|done|cancelled
    result: Mapped[str | None] = mapped_column(Text, default=None)  # duruşma sonucu vb.
    reminder_status: Mapped[str] = mapped_column(String(30), default="pending")
    remind_before_min: Mapped[int | None] = mapped_column(default=None)  # phase: reminders
    remind_channel: Mapped[str | None] = mapped_column(String(32), default=None)  # email|whatsapp…
    rrule: Mapped[str | None] = mapped_column(String(300), default=None)  # phase: recurrence
    notes: Mapped[str | None] = mapped_column(Text, default=None)
    created_by: Mapped[int | None] = mapped_column(ForeignKey("users.id"), default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())

    matter: Mapped[Matter | None] = relationship(back_populates="events")
    client: Mapped[Client | None] = relationship()
    assignee: Mapped[User | None] = relationship(foreign_keys=[assignee_id])


class Invoice(TenantMixin, Base):
    """A client invoice / serbest meslek makbuzu — draft-first, like the rest of Korkut.

    `amount` is the net (KDV hariç) figure and `vat_rate` the KDV percent; the gross is derived.
    `status` tracks the lifecycle draft → issued → paid (or cancelled). Optionally filed under a
    client and/or matter. Real e-SMM/e-fatura submission (GİB) is out of scope for now.
    """

    __tablename__ = "invoices"
    id: Mapped[int] = mapped_column(primary_key=True)
    client_id: Mapped[int | None] = mapped_column(
        ForeignKey("clients.id"), default=None, index=True
    )
    matter_id: Mapped[int | None] = mapped_column(
        ForeignKey("matters.id"), default=None, index=True
    )
    amount: Mapped[Decimal] = mapped_column(Numeric(12, 2), default=0)  # net tutar (KDV hariç)
    vat_rate: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=20)  # KDV %
    description: Mapped[str | None] = mapped_column(String(300), default=None)
    status: Mapped[str] = mapped_column(String(16), default="draft")  # draft|issued|paid|cancelled
    issued_at: Mapped[_date | None] = mapped_column(Date, default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())


class Expense(TenantMixin, Base):
    """A case expense or advance (masraf/avans) — harç, posta, bilirkişi, yol, etc.

    Optionally filed under a matter and/or client, so it can roll up into a client's account.
    """

    __tablename__ = "expenses"
    id: Mapped[int] = mapped_column(primary_key=True)
    matter_id: Mapped[int | None] = mapped_column(
        ForeignKey("matters.id"), default=None, index=True
    )
    client_id: Mapped[int | None] = mapped_column(
        ForeignKey("clients.id"), default=None, index=True
    )
    amount: Mapped[Decimal] = mapped_column(Numeric(12, 2), default=0)
    kind: Mapped[str | None] = mapped_column(String(40), default=None)  # harç, posta, bilirkişi…
    description: Mapped[str | None] = mapped_column(String(300), default=None)
    spent_at: Mapped[_date | None] = mapped_column(Date, default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())


class Task(TenantMixin, Base):
    """A to-do assigned to an office member (görev). Optionally tied to a matter and due-dated.

    `status` is open|done. A due-dated open task can surface in the office agenda alongside
    hearings and deadlines. `assignee` is the member responsible; `created_by` who added it.
    """

    __tablename__ = "tasks"
    id: Mapped[int] = mapped_column(primary_key=True)
    title: Mapped[str] = mapped_column(String(200))
    description: Mapped[str | None] = mapped_column(Text, default=None)
    assignee_id: Mapped[int | None] = mapped_column(
        ForeignKey("users.id"), default=None, index=True
    )
    matter_id: Mapped[int | None] = mapped_column(
        ForeignKey("matters.id"), default=None, index=True
    )
    status: Mapped[str] = mapped_column(String(16), default="open")  # open | done
    due_date: Mapped[_date | None] = mapped_column(Date, default=None, index=True)
    created_by: Mapped[int | None] = mapped_column(ForeignKey("users.id"), default=None)
    created_at: Mapped[datetime] = mapped_column(default=func.now())

    assignee: Mapped[User | None] = relationship(foreign_keys=[assignee_id])


class DocumentRecord(TenantMixin, Base):
    """A document-register (evrak) entry — incoming/outgoing correspondence and internal notes.

    A metadata log of the paper/e-documents on a matter (gelen/giden/iç), not file storage: title,
    date, reference/sayı number, and notes. Optionally tied to a matter. File attachments are a
    later addition.
    """

    __tablename__ = "document_records"
    id: Mapped[int] = mapped_column(primary_key=True)
    matter_id: Mapped[int | None] = mapped_column(
        ForeignKey("matters.id"), default=None, index=True
    )
    # incoming | outgoing | internal
    direction: Mapped[str] = mapped_column(String(16), default="incoming")
    title: Mapped[str] = mapped_column(String(200))
    reference_no: Mapped[str | None] = mapped_column(String(100), default=None)  # sayı/referans
    doc_date: Mapped[_date | None] = mapped_column(Date, default=None)  # evrak tarihi
    notes: Mapped[str | None] = mapped_column(Text, default=None)
    created_by: Mapped[int | None] = mapped_column(ForeignKey("users.id"), default=None)
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
