"""Agent-tool tests for the matter domain — the per-request handler factory.

Builds `matter_tools.build_matter_tools(user)` against a seeded two-office DB and exercises the
handlers the model would call. Asserts writes land, office scoping holds (one office never sees
another's data), and bad input degrades to a structured error (never an exception).
"""

import importlib
import types

import pytest


@pytest.fixture
def seeded(tmp_path, monkeypatch):
    """A fresh DB with two offices, each with one user. Yields (store, userA, userB)."""
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 't.db'}")
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    from korkut.api.models import Office, User

    store_mod.engine()
    with store_mod.session() as s:
        oa, ob = Office(name="A"), Office(name="B")
        s.add_all([oa, ob])
        s.flush()
        ua = User(office_id=oa.id, username="a", password_hash="x", role="owner")
        ub = User(office_id=ob.id, username="b", password_hash="x", role="owner")
        s.add_all([ua, ub])
        s.commit()
        # Lightweight stand-ins (the factory only reads office_id + id).
        user_a = types.SimpleNamespace(office_id=oa.id, id=ua.id)
        user_b = types.SimpleNamespace(office_id=ob.id, id=ub.id)
    return store_mod, user_a, user_b


def _tools(user):
    from korkut.api import matter_tools

    _, handlers = matter_tools.build_matter_tools(user)
    return handlers


def test_create_list_get_hearing(seeded):
    _, user_a, _ = seeded
    h = _tools(user_a)

    r = h["create_matter"]({"title": "Ahmet Y. – Boşanma", "client_name": "Ahmet Yılmaz"})
    assert r["ok"] is True
    mid = r["matter"]["id"]

    assert [m["title"] for m in h["list_matters"]({})["matters"]] == ["Ahmet Y. – Boşanma"]
    assert h["list_clients"]({})["count"] == 1

    add = h["add_hearing"]({"matter_id": mid, "date": "2099-07-12", "time": "10:30", "type": "ön inceleme"})
    assert add["ok"] is True and add["hearing"]["time"] == "10:30"

    detail = h["get_matter"]({"matter_id": mid})["matter"]
    assert len(detail["hearings"]) == 1

    upd = h["update_matter"]({"matter_id": mid, "status": "closed"})
    assert upd["ok"] is True and upd["matter"]["status"] == "closed"


def test_client_dedup_on_create(seeded):
    _, user_a, _ = seeded
    h = _tools(user_a)
    h["create_matter"]({"title": "Dosya 1", "client_name": "Ayşe Demir"})
    h["create_matter"]({"title": "Dosya 2", "client_name": "ayşe demir"})  # same name, diff case
    assert h["list_clients"]({})["count"] == 1  # one client, reused


def test_office_isolation(seeded):
    _, user_a, user_b = seeded
    ha, hb = _tools(user_a), _tools(user_b)
    mid = ha["create_matter"]({"title": "Gizli"})["matter"]["id"]

    assert hb["list_matters"]({})["count"] == 0
    assert hb["get_matter"]({"matter_id": mid}) == {"error": "not_found", "detail": "Dosya bulunamadı."}
    assert hb["add_hearing"]({"matter_id": mid, "date": "2099-01-01"})["ok"] is False
    assert hb["update_matter"]({"matter_id": mid, "status": "closed"})["ok"] is False


def test_error_paths_never_raise(seeded):
    _, user_a, _ = seeded
    h = _tools(user_a)
    assert h["create_matter"]({"title": "   "})["ok"] is False
    assert h["get_matter"]({"matter_id": 9999})["error"] == "not_found"
    assert h["add_hearing"]({"matter_id": 1, "date": "12 temmuz"})["ok"] is False  # bad date
    assert h["update_matter"]({})["ok"] is False  # missing matter_id


def test_upcoming_deadlines_window(seeded):
    from datetime import date, timedelta

    _, user_a, _ = seeded
    h = _tools(user_a)
    mid = h["create_matter"]({"title": "D"})["matter"]["id"]
    soon = (date.today() + timedelta(days=3)).isoformat()
    far = (date.today() + timedelta(days=100)).isoformat()
    h["add_hearing"]({"matter_id": mid, "date": soon})
    h["add_hearing"]({"matter_id": mid, "date": far})
    assert h["upcoming_deadlines"]({"days": 14})["count"] == 1  # only the near one
    assert h["upcoming_deadlines"]({"days": 200})["count"] == 2
