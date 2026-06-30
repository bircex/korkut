"""Database wiring for the web app.

URL-driven so the trial can run on a single SQLite file (default) while production points
KORKUT_DB_URL at the office Postgres. Importing `web.models` here guarantees the user/message
tables are registered on `Base.metadata` before `init_db` runs.
"""

from __future__ import annotations

import hashlib
import os

from sqlalchemy import inspect, select, text
from sqlalchemy.orm import Session, sessionmaker

from korkut.api import models  # noqa: F401  (registers the models on Base.metadata)
from korkut.api.models import OfficeChannel, OfficeModel, TemplateDefault, TemplateOverride
from korkut.core import db
from korkut.core.router import ModelCfg
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
        _migrate_legacy(_engine)  # pre-init: drop obsolete chat tables so init_db rebuilds them
        db.init_db(_engine)  # create any missing tables (offices, documents, …)
        _migrate_tenant(_engine)  # post-init: add multi-tenant columns to existing tables
        _Session = sessionmaker(_engine, expire_on_commit=False)
        sync_template_defaults(
            _engine
        )  # refresh built-in themes from files (never touches overrides)
    return _engine


# Folder (under templates/) → catalog `kind`. Example files are named "<type_id>.md".
_THEME_DIRS = {"petition": "dilekce", "contract": "sozlesme"}


def effective_theme(s: Session, office_id: int, user_id: int | None, type_id: str) -> str | None:
    """Resolve a type's theme: lawyer override > office override > built-in default.

    Returns the theme markdown the LLM should draw inspiration from, or None if the type has no
    default and no override. `user_id` None skips the personal layer (e.g. system-initiated drafts).
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
            TemplateOverride.office_id == office_id,
            TemplateOverride.user_id.is_(None),
            TemplateOverride.type_id == type_id,
        )
    )
    if office is not None:
        return office.content
    default = s.scalar(select(TemplateDefault).where(TemplateDefault.type_id == type_id))
    return default.content if default is not None else None


def office_model_cfg(s: Session, office_id: int) -> ModelCfg | None:
    """The office's custom model as a router `ModelCfg`, or None if unset/disabled (→ default)."""
    row = s.scalar(select(OfficeModel).where(OfficeModel.office_id == office_id))
    if row is None or not row.enabled or not row.model or not row.base_url:
        return None
    return ModelCfg(
        model=row.model,
        base_url=row.base_url,
        api_key=row.api_key or None,
        local=row.is_local,
    )


def office_channel_config(s: Session, office_id: int, kind: str) -> dict | None:
    """The office's config dict for an enabled outbound channel (`kind`), or None if unset/off."""
    row = s.scalar(
        select(OfficeChannel).where(
            OfficeChannel.office_id == office_id, OfficeChannel.kind == kind
        )
    )
    if row is None or not row.enabled:
        return None
    return dict(row.config or {})


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


def _migrate_legacy(engine) -> None:
    """Old per-user `messages` schema (no `conversation_id`) → drop chat tables; init_db rebuilds.
    Chat history is disposable; accounts/2FA/recovery/audit are preserved.
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


def _migrate_tenant(engine) -> None:
    """Multi-tenant upgrade: add `office_id`/`role` to existing rows WITHOUT data loss.

    A pre-tenant DB has a `users` table with no `office_id`. We create a default office, add the
    columns (SQLite ADD COLUMN), assign every existing user/conversation to that office, and make
    the earliest user its owner. Idempotent (skips once `office_id` exists).
    """
    insp = inspect(engine)
    if "users" not in insp.get_table_names():
        return  # fresh DB — init_db already created the correct schema
    if "office_id" in {c["name"] for c in insp.get_columns("users")}:
        return  # already migrated
    with engine.begin() as conn:
        oid = conn.execute(text("SELECT id FROM offices ORDER BY id LIMIT 1")).scalar()
        if oid is None:
            # Backfill a single office for pre-multitenant data; name is configurable.
            default_office = os.getenv("KORKUT_DEFAULT_OFFICE", "Hukuk Ofisi")
            conn.execute(
                text("INSERT INTO offices (name, created_at) VALUES (:name, CURRENT_TIMESTAMP)"),
                {"name": default_office},
            )
            oid = conn.execute(text("SELECT id FROM offices ORDER BY id LIMIT 1")).scalar()
        conn.execute(text("ALTER TABLE users ADD COLUMN office_id INTEGER"))
        conn.execute(text("ALTER TABLE users ADD COLUMN role VARCHAR(16) DEFAULT 'member'"))
        conn.execute(text(f"UPDATE users SET office_id = {oid}"))
        conn.execute(
            text(
                "UPDATE users SET role = 'owner' "
                "WHERE id = (SELECT id FROM users ORDER BY id LIMIT 1)"
            )
        )
        if "conversations" in insp.get_table_names() and "office_id" not in {
            c["name"] for c in insp.get_columns("conversations")
        }:
            conn.execute(text("ALTER TABLE conversations ADD COLUMN office_id INTEGER"))
            conn.execute(text(f"UPDATE conversations SET office_id = {oid}"))


def session() -> Session:
    engine()
    assert _Session is not None
    return _Session()
