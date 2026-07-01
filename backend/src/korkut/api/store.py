"""Database wiring for the web app.

URL-driven so the trial can run on a single SQLite file (default) while production points
KORKUT_DB_URL at the office Postgres. Importing `web.models` here guarantees the user/message
tables are registered on `Base.metadata` before `init_db` runs.
"""

from __future__ import annotations

import hashlib
import os
from datetime import UTC, datetime, timedelta
from urllib.parse import urlsplit, urlunsplit

from sqlalchemy import select
from sqlalchemy.orm import Session, sessionmaker

from korkut.api import models  # noqa: F401  (registers the models on Base.metadata)
from korkut.api.models import (
    CalendarEvent,
    MemoryItem,
    Office,
    OfficeChannel,
    OfficeModel,
    OutboundMessage,
    Persona,
    TemplateDefault,
    TemplateOverride,
)
from korkut.brain.persona import PersonaCfg
from korkut.core import db
from korkut.core.router import ModelCfg
from korkut.messaging import OutboundDraft, SendResult, build_channel
from korkut.paths import DATA_DIR, TEMPLATES_DIR

_DEFAULT_URL = f"sqlite+pysqlite:///{DATA_DIR / 'korkut.db'}"

_engine = None
_Session: sessionmaker[Session] | None = None


def _url() -> str:
    return os.getenv("KORKUT_DB_URL", _DEFAULT_URL)


def engine():
    global _engine, _Session
    if _engine is None:
        url = _url()
        if url.startswith("sqlite") and "memory" not in url:
            DATA_DIR.mkdir(parents=True, exist_ok=True)
        _engine = db.make_engine(url)
        if "memory" in url:
            # In-memory test databases are ephemeral: build the schema straight from the models.
            db.init_db(_engine)
        else:
            # Real (file SQLite / Postgres) databases are migrated so schema changes never drift.
            from korkut.core.migrate import run_migrations

            run_migrations(_engine)
        _Session = sessionmaker(_engine, expire_on_commit=False)
        sync_template_defaults(
            _engine
        )  # refresh built-in themes from files (never touches overrides)
    return _engine


# Folder (under templates/) → catalog `kind`. Example files are named "<type_id>.md".
_THEME_DIRS = {"petition": "dilekce", "contract": "sozlesme"}


def get_office(s: Session) -> Office | None:
    """The single office identity row (singleton), or None before bootstrap."""
    return s.scalar(select(Office))


def effective_theme(s: Session, user_id: int | None, type_id: str) -> str | None:
    """Resolve a type's theme: lawyer override > office override > built-in default.

    Returns the theme markdown the LLM should draw inspiration from, or None if the type has no
    default and no override. `user_id` None skips the personal layer (e.g. system-initiated drafts).
    The office override is the row with `user_id IS NULL` for the type.
    """
    if user_id is not None:
        personal = s.scalar(
            select(TemplateOverride).where(
                TemplateOverride.user_id == user_id, TemplateOverride.type_id == type_id
            )
        )
        if personal is not None:
            return personal.content
    office = s.scalar(
        select(TemplateOverride).where(
            TemplateOverride.user_id.is_(None),
            TemplateOverride.type_id == type_id,
        )
    )
    if office is not None:
        return office.content
    default = s.scalar(select(TemplateDefault).where(TemplateDefault.type_id == type_id))
    return default.content if default is not None else None


def get_persona(s: Session) -> PersonaCfg:
    """The office's Kişilik tuning as a `PersonaCfg` (empty defaults before it is set)."""
    row = s.scalar(select(Persona))
    if row is None:
        return PersonaCfg()
    return PersonaCfg(tone=row.tone, expertise=row.expertise, extra_rules=row.extra_rules)


def collect_memory(s: Session) -> str:
    """The office's APPROVED memory notes as a prompt block (bulleted), or '' if there are none.

    Only ``active`` notes are injected; agent-proposed (``pending``) notes wait for a lawyer's
    approval in the Hafıza tab and never reach the model until then.
    """
    rows = s.scalars(
        select(MemoryItem).where(MemoryItem.status == "active").order_by(MemoryItem.created_at)
    ).all()
    lines = [f"- {r.text.strip()}" for r in rows if r.text.strip()]
    return "\n".join(lines)


def _docker_host_rewrite(base_url: str) -> str:
    """Inside a container, a user-entered `localhost` points at the container itself, not the
    host machine. Rewrite loopback hosts to `host.docker.internal` so a local LM server running
    on the host (e.g. LM Studio) stays reachable. No-op when not containerized or the host isn't
    loopback — so the UI can just say `http://localhost:1234/v1` and it works everywhere.
    """
    if not os.path.exists("/.dockerenv"):
        return base_url
    try:
        parts = urlsplit(base_url)
    except ValueError:
        return base_url
    if parts.hostname not in ("localhost", "127.0.0.1", "::1", "0.0.0.0"):
        return base_url
    userinfo = ""
    if parts.username:
        userinfo = parts.username + (f":{parts.password}" if parts.password else "") + "@"
    netloc = f"{userinfo}host.docker.internal" + (f":{parts.port}" if parts.port else "")
    return urlunsplit((parts.scheme, netloc, parts.path, parts.query, parts.fragment))


def office_model_cfg(s: Session) -> ModelCfg | None:
    """The office's custom model as a router `ModelCfg`, or None if unset/disabled (→ default)."""
    row = s.scalar(select(OfficeModel))
    if row is None or not row.enabled or not row.model or not row.base_url:
        return None
    return ModelCfg(
        model=row.model,
        base_url=_docker_host_rewrite(row.base_url),
        api_key=row.api_key or None,
        local=row.is_local,
    )


def office_channel_config(s: Session, kind: str) -> dict | None:
    """The office's config dict for an enabled outbound channel (`kind`), or None if unset/off."""
    row = s.scalar(select(OfficeChannel).where(OfficeChannel.kind == kind))
    if row is None or not row.enabled:
        return None
    return dict(row.config or {})


def _utcnow() -> datetime:
    return datetime.now(UTC).replace(tzinfo=None)


def send_outbound_message(s: Session, msg: OutboundMessage) -> SendResult:
    """Send one message now via its office's channel config; updates `msg` status in place.

    Shared by the confirm endpoint (immediate sends) and the dispatcher (scheduled sends).
    """
    cfg = office_channel_config(s, msg.kind)
    msg.attempts += 1
    if cfg is None:
        msg.status = "failed"
        msg.error = "Kanal yapılandırılmamış veya kapalı"
        return SendResult(ok=False, detail=msg.error)
    result = build_channel(msg.kind, cfg).send(
        OutboundDraft(to_addr=msg.to_addr, subject=msg.subject, body=msg.body)
    )
    if result.ok:
        msg.status = "sent"
        msg.sent_at = _utcnow()
        msg.error = None
    else:
        msg.status = "failed"
        msg.error = result.detail
    return result


def dispatch_due(limit: int = 100) -> int:
    """Send every queued message whose `send_at` has arrived. Returns how many were processed.

    Idempotent and safe to run on a schedule (cron): only `queued` rows with a due `send_at` are
    touched, and each becomes `sent`/`failed` so it is never picked up twice.
    """
    processed = 0
    with session() as s:
        due = s.scalars(
            select(OutboundMessage)
            .where(
                OutboundMessage.status == "queued",
                OutboundMessage.send_at.is_not(None),
                OutboundMessage.send_at <= _utcnow(),
            )
            .order_by(OutboundMessage.send_at)
            .limit(limit)
        ).all()
        for msg in due:
            send_outbound_message(s, msg)
            processed += 1
        s.commit()
    return processed


def _reminder_body(ev: CalendarEvent) -> str:
    """A short Turkish reminder e-mail body for a calendar event."""
    when = ev.start_at.strftime("%d.%m.%Y %H:%M") if ev.start_at else ""
    lines = [f"Yaklaşan etkinlik: {ev.title}", f"Zaman: {when}"]
    if ev.location:
        lines.append(f"Yer: {ev.location}")
    if ev.notes:
        lines.append("")
        lines.append(ev.notes)
    return "\n".join(lines)


def dispatch_due_reminders(limit: int = 100) -> int:
    """Send e-mail reminders for calendar events whose reminder moment has arrived.

    A non-recurring event with `remind_before_min` set and `reminder_status == "pending"` is
    reminded once, `remind_before_min` minutes before it starts, via the office's e-mail channel
    (to the office's own configured address). Each event then flips to sent/failed/skipped so it is
    never reminded twice. Events are stored in local time, so the due check uses the local clock.
    Recurring-event reminders are a later refinement (occurrences need per-instance state).
    """
    processed = 0
    now = datetime.now()
    with session() as s:
        cfg = office_channel_config(s, "email")
        due = s.scalars(
            select(CalendarEvent)
            .where(
                CalendarEvent.reminder_status == "pending",
                CalendarEvent.remind_before_min.is_not(None),
                CalendarEvent.rrule.is_(None),
                CalendarEvent.start_at.is_not(None),
                CalendarEvent.status != "cancelled",
            )
            .order_by(CalendarEvent.start_at)
            .limit(limit)
        ).all()
        for ev in due:
            remind_at = ev.start_at - timedelta(minutes=ev.remind_before_min or 0)
            if remind_at > now:
                continue  # reminder moment not reached yet — re-checked next run
            if ev.start_at < now:
                ev.reminder_status = "skipped"  # event already started; a reminder is pointless
                processed += 1
                continue
            to_addr = (cfg or {}).get("from_address") or ""
            if cfg is None or not to_addr:
                ev.reminder_status = "skipped"  # no configured e-mail channel to send through
                processed += 1
                continue
            result = build_channel("email", cfg).send(
                OutboundDraft(
                    to_addr=to_addr, subject=f"Hatırlatma: {ev.title}", body=_reminder_body(ev)
                )
            )
            ev.reminder_status = "sent" if result.ok else "failed"
            processed += 1
        s.commit()
    return processed


def sync_template_defaults(engine) -> None:
    """Upsert the built-in themes (templates/*/examples/*.md) into `template_defaults`.

    Idempotent and cheap on reboots: a file whose content hash is unchanged is skipped.
    `template_overrides` (office/lawyer customizations) are never read or modified here.
    """
    with Session(engine) as s:
        existing = {d.type_id: d for d in s.scalars(select(TemplateDefault)).all()}
        for folder, kind in _THEME_DIRS.items():
            ex_dir = TEMPLATES_DIR / folder / "examples"
            if not ex_dir.is_dir():
                continue
            for md in sorted(ex_dir.glob("*.md")):
                content = md.read_text(encoding="utf-8")
                digest = hashlib.sha256(content.encode("utf-8")).hexdigest()
                row = existing.get(md.stem)
                if row is None:
                    s.add(
                        TemplateDefault(
                            type_id=md.stem, kind=kind, content=content, source_hash=digest
                        )
                    )
                elif row.source_hash != digest or row.kind != kind:
                    row.content, row.source_hash, row.kind = content, digest, kind
        s.commit()


def session() -> Session:
    engine()
    assert _Session is not None
    return _Session()
