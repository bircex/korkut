"""Agent-tool tests for the full office toolset (office_tools.build_office_tools).

Seeds a single-office DB, builds the toolset for a user, and exercises the handlers the model would
call across müvekkil / takvim / mali / görev / evrak / rapor. Confirms writes land, the toolset is
create/read/update only (no delete tool), and account-settings tools are absent.
"""

import importlib
import types

import pytest


@pytest.fixture
def seeded(tmp_path, monkeypatch):
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 't.db'}")
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    from korkut.api.models import Office, User

    store_mod.engine()
    with store_mod.session() as s:
        s.add(Office(name="A"))
        s.flush()
        ua = User(username="a", password_hash="x", role="owner", full_name="Av. A")
        s.add(ua)
        s.commit()
        user_a = types.SimpleNamespace(id=ua.id)
    return store_mod, user_a


def _tools(user):
    from korkut.api import office_tools

    return office_tools.build_office_tools(user)


def test_toolset_is_create_read_update_only(seeded):
    _, user = seeded
    specs, handlers = _tools(user)
    names = set(handlers)
    # The whole office surface is reachable...
    assert {"create_client", "create_event", "add_expense", "add_task", "add_evrak"} <= names
    assert {
        "list_events",
        "list_tasks",
        "list_evrak",
        "finance_summary",
        "office_overview",
    } <= names
    # ...but nothing destructive or account-settings related is exposed.
    assert not any("delete" in n or "remove" in n for n in names)
    assert not any(n in names for n in ("update_persona", "set_model", "add_member", "enroll_2fa"))
    # specs and handlers stay in sync
    assert {t.name for t in specs} == names


def test_client_calendar_finance_task_evrak_flow(seeded):
    _, user = seeded
    _, h = _tools(user)

    cid = h["create_client"]({"full_name": "Ayşe Yıldız", "phone": "0555"})["client"]["id"]

    ev = h["create_event"](
        {
            "category": "appointment",
            "title": "İlk görüşme",
            "start_at": "2099-03-10T14:30:00",
            "client_id": cid,
        }
    )
    assert ev["ok"] and ev["event"]["client_id"] == cid
    listed = h["list_events"]({"start": "2099-03-01T00:00:00", "end": "2099-03-31T23:59:59"})
    assert listed["count"] == 1 and listed["events"][0]["title"] == "İlk görüşme"

    assert h["add_expense"]({"amount": 150, "kind": "harç", "client_id": cid})["ok"]
    assert h["finance_summary"]({"client_id": cid})["expenses"] == 150.0

    me = h["list_members"]({})["members"][0]
    t = h["add_task"]({"title": "Dilekçe yaz", "assignee_id": me["id"]})
    assert t["ok"] and t["task"]["assignee_name"] == "Av. A"
    assert h["list_tasks"]({"status": "open"})["count"] == 1
    assert h["complete_task"]({"task_id": t["task"]["id"]})["ok"]
    assert h["list_tasks"]({"status": "open"})["count"] == 0

    assert h["add_evrak"]({"title": "Tebligat", "direction": "incoming"})["ok"]
    assert h["list_evrak"]({})["count"] == 1

    overview = h["office_overview"]({})
    assert overview["clients"]["total"] == 1 and overview["evrak"]["total"] == 1


def test_recurring_and_reminder_event_via_agent(seeded):
    _, user = seeded
    _, h = _tools(user)
    ev = h["create_event"](
        {
            "title": "Ekip toplantısı",
            "start_at": "2099-04-06T10:00:00",
            "rrule": "FREQ=WEEKLY;INTERVAL=1;UNTIL=2099-04-27",
            "remind_before_min": 60,
        }
    )
    assert ev["ok"]
    assert ev["event"]["rrule"] == "FREQ=WEEKLY;INTERVAL=1;UNTIL=2099-04-27"
    assert ev["event"]["remind_before_min"] == 60
    # The weekly rule expands to four Mondays in April.
    listed = h["list_events"]({"start": "2099-04-01T00:00:00", "end": "2099-04-30T23:59:59"})
    assert listed["count"] == 4


def test_bad_input_degrades_to_error(seeded):
    _, user = seeded
    _, h = _tools(user)
    assert h["create_client"]({"full_name": "   "})["ok"] is False
    assert h["create_event"]({"title": "X"})["ok"] is False  # missing start_at
    assert h["create_event"]({"title": "X", "start_at": "kötü tarih"})["ok"] is False
    assert h["complete_task"]({"task_id": 9999})["ok"] is False
