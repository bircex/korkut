"""Agent-tool tests for the matter domain — the per-request handler factory.

Builds `matter_tools.build_matter_tools(user)` against a seeded single-office DB and exercises the
handlers the model would call. Asserts writes land and bad input degrades to a structured error
(never an exception).
"""

import importlib
import types

import pytest


@pytest.fixture
def seeded(tmp_path, monkeypatch):
    """A fresh DB with the single office + one user. Yields (store, user)."""
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 't.db'}")
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    from korkut.api.models import Office, User

    store_mod.engine()
    with store_mod.session() as s:
        office = Office(name="A")
        s.add(office)
        s.flush()
        ua = User(username="a", password_hash="x", role="owner")
        s.add(ua)
        s.commit()
        # Lightweight stand-in (the factory only reads id).
        user_a = types.SimpleNamespace(id=ua.id)
    return store_mod, user_a


def _tools(user):
    from korkut.api import matter_tools

    _, handlers = matter_tools.build_matter_tools(user)
    return handlers


def test_create_list_get_hearing(seeded):
    _, user_a = seeded
    h = _tools(user_a)

    r = h["create_matter"]({"title": "Ahmet Y. – Boşanma", "client_name": "Ahmet Yılmaz"})
    assert r["ok"] is True
    mid = r["matter"]["id"]

    assert [m["title"] for m in h["list_matters"]({})["matters"]] == ["Ahmet Y. – Boşanma"]
    assert h["list_clients"]({})["count"] == 1

    add = h["add_hearing"](
        {"matter_id": mid, "date": "2099-07-12", "time": "10:30", "type": "ön inceleme"}
    )
    assert add["ok"] is True and add["hearing"]["time"] == "10:30"

    detail = h["get_matter"]({"matter_id": mid})["matter"]
    assert len(detail["hearings"]) == 1

    upd = h["update_matter"]({"matter_id": mid, "status": "closed"})
    assert upd["ok"] is True and upd["matter"]["status"] == "closed"


def test_client_dedup_on_create(seeded):
    _, user_a = seeded
    h = _tools(user_a)
    h["create_matter"]({"title": "Dosya 1", "client_name": "Ayşe Demir"})
    h["create_matter"]({"title": "Dosya 2", "client_name": "ayşe demir"})  # same name, diff case
    assert h["list_clients"]({})["count"] == 1  # one client, reused


def test_error_paths_never_raise(seeded):
    _, user_a = seeded
    h = _tools(user_a)
    assert h["create_matter"]({"title": "   "})["ok"] is False
    assert h["get_matter"]({"matter_id": 9999})["error"] == "not_found"
    assert h["add_hearing"]({"matter_id": 1, "date": "12 temmuz"})["ok"] is False  # bad date
    assert h["update_matter"]({})["ok"] is False  # missing matter_id


def test_upcoming_agenda_merges_hearings_and_deadlines(seeded):
    from datetime import date, timedelta

    _, user_a = seeded
    h = _tools(user_a)
    mid = h["create_matter"]({"title": "D"})["matter"]["id"]
    soon = (date.today() + timedelta(days=3)).isoformat()
    far = (date.today() + timedelta(days=100)).isoformat()
    h["add_hearing"]({"matter_id": mid, "date": soon})
    assert h["add_deadline"](
        {"matter_id": mid, "due_date": soon, "title": "Cevap", "type": "cevap"}
    )["ok"]
    h["add_deadline"]({"matter_id": mid, "due_date": far, "title": "Temyiz"})

    ag = h["upcoming_deadlines"]({"days": 14})
    assert ag["count"] == 2 and {i["kind"] for i in ag["agenda"]} == {"hearing", "deadline"}
    assert h["upcoming_deadlines"]({"days": 200})["count"] == 3  # far deadline now in window

    # Completing the near deadline removes it from the 14-day agenda (hearing stays).
    did = h["list_deadlines"]({"matter_id": mid})["deadlines"][0][
        "id"
    ]  # ordered by due_date → near
    assert h["complete_deadline"]({"deadline_id": did})["ok"]
    assert h["upcoming_deadlines"]({"days": 14})["count"] == 1


def test_documents_linked_and_prepare_hearing(seeded):
    from datetime import date, timedelta

    store_mod, user_a = seeded
    from korkut.api.models import Conversation, Document

    h = _tools(user_a)
    mid = h["create_matter"]({"title": "Dosya"})["matter"]["id"]
    # A draft in a conversation filed under the matter → should surface as the matter's document.
    with store_mod.session() as s:
        conv = Conversation(matter_id=mid, title="Sohbet")
        s.add(conv)
        s.flush()
        s.add(
            Document(
                conversation_id=conv.id, title="Dava Dilekçesi", content_md="...", status="draft"
            )
        )
        s.commit()

    full = h["get_matter"]({"matter_id": mid})["matter"]
    assert [d["title"] for d in full["documents"]] == ["Dava Dilekçesi"]

    # prepare_hearing bundles matter + upcoming agenda + linked documents + a brief instruction.
    h["add_hearing"]({"matter_id": mid, "date": (date.today() + timedelta(days=2)).isoformat()})
    pack = h["prepare_hearing"]({"matter_id": mid})
    assert pack["matter"]["title"] == "Dosya"
    assert len(pack["agenda"]) == 1 and pack["agenda"][0]["kind"] == "hearing"
    assert "DURUŞMA HAZIRLIK" in pack["note"]
    assert [d["title"] for d in pack["matter"]["documents"]] == ["Dava Dilekçesi"]


def test_prepare_hearing_not_found(seeded):
    _, user_a = seeded
    ha = _tools(user_a)
    assert ha["prepare_hearing"]({"matter_id": 9999}) == {
        "error": "not_found",
        "detail": "Dosya bulunamadı.",
    }


def test_deadline_not_found_and_errors(seeded):
    _, user_a = seeded
    ha = _tools(user_a)
    mid = ha["create_matter"]({"title": "G"})["matter"]["id"]
    # A deadline on a nonexistent matter fails.
    assert (
        ha["add_deadline"]({"matter_id": 9999, "due_date": "2099-01-01", "title": "X"})["ok"]
        is False
    )
    # Completing a nonexistent deadline fails.
    assert ha["complete_deadline"]({"deadline_id": 9999})["ok"] is False
    # Bad date / blank title degrade to a structured error.
    assert (
        ha["add_deadline"]({"matter_id": mid, "due_date": "kötü tarih", "title": "S"})["ok"]
        is False
    )
    assert (
        ha["add_deadline"]({"matter_id": mid, "due_date": "2099-01-01", "title": "  "})["ok"]
        is False
    )
