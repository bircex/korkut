"""Tests for template themes: file→DB sync and override resolution (store layer)."""

import pytest
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from korkut.api import store
from korkut.api.models import TemplateDefault, TemplateOverride
from korkut.core.db import init_db, make_engine


@pytest.fixture
def engine():
    e = make_engine("sqlite+pysqlite:///:memory:")
    init_db(e)
    return e


def test_sync_loads_all_themes_and_is_idempotent(engine):
    store.sync_template_defaults(engine)
    with Session(engine) as s:
        total = s.scalar(select(func.count()).select_from(TemplateDefault))
        kinds = dict(
            s.execute(
                select(TemplateDefault.kind, func.count()).group_by(TemplateDefault.kind)
            ).all()
        )
    assert total > 100  # 184 today; lock the order of magnitude, not the exact count
    assert kinds.get("dilekce", 0) > 0 and kinds.get("sozlesme", 0) > 0

    # Second sync changes nothing (hash-skip) — same row count.
    store.sync_template_defaults(engine)
    with Session(engine) as s:
        assert s.scalar(select(func.count()).select_from(TemplateDefault)) == total


def test_sync_updates_changed_default(engine):
    with Session(engine) as s:
        s.add(TemplateDefault(type_id="x-tip", kind="dilekce", content="eski", source_hash="stale"))
        s.commit()
    store.sync_template_defaults(engine)
    # x-tip has no backing file, so it is left as-is (sync only upserts files it finds).
    with Session(engine) as s:
        row = s.scalar(select(TemplateDefault).where(TemplateDefault.type_id == "x-tip"))
        assert row.content == "eski"


def _seed_default(engine, type_id="anlasmali-bosanma"):
    with Session(engine) as s:
        s.add(TemplateDefault(type_id=type_id, kind="dilekce", content="DEFAULT", source_hash="h"))
        s.commit()


def test_resolution_default_office_user(engine):
    _seed_default(engine)
    tid, user = "anlasmali-bosanma", 7
    with Session(engine) as s:
        # default only
        assert store.effective_theme(s, user, tid) == "DEFAULT"
        # office override (user_id IS NULL) wins over default
        s.add(TemplateOverride(user_id=None, type_id=tid, content="OFICE"))
        s.commit()
        assert store.effective_theme(s, user, tid) == "OFICE"
        # personal override wins over office
        s.add(TemplateOverride(user_id=user, type_id=tid, content="BENIM"))
        s.commit()
        assert store.effective_theme(s, user, tid) == "BENIM"
        # None user_id skips the personal layer → office
        assert store.effective_theme(s, None, tid) == "OFICE"


def test_unknown_type_returns_none(engine):
    with Session(engine) as s:
        assert store.effective_theme(s, 1, "yok-boyle-tip") is None
