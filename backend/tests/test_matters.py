"""HTTP endpoint tests for the matter domain (/api/matters, /api/clients, /api/calendar).

Mirrors test_web_auth.py: a fresh SQLite file per run, the brain patched offline, and a password
login to authenticate (2FA is off by default). Covers CRUD and the chat↔matter link.
"""

from datetime import date, timedelta
from unittest.mock import patch

import pytest
from fastapi.testclient import TestClient


@pytest.fixture
def app_mod(tmp_path, monkeypatch):
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 't.db'}")
    monkeypatch.setenv("KORKUT_SECRET", "test-secret-stable")
    import importlib

    import korkut.api.auth as auth_mod
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    auth_mod._secret_cache = None
    import korkut.api.app as app_mod

    importlib.reload(app_mod)

    import korkut.api.adduser as adduser

    adduser.main(["avukat", "--password", "parola123", "--name", "Av. Test"])
    with patch.object(app_mod.agent, "answer", return_value="DRAFT yanıt."):
        yield app_mod


def _enroll(c: TestClient, username: str, password: str) -> None:
    """Sign in to a full session; 2FA is off by default, so the password alone authenticates."""
    c.post("/api/login", json={"username": username, "password": password})


def _existing(app_mod) -> TestClient:
    c = TestClient(app_mod.app)
    _enroll(c, "avukat", "parola123")
    return c


def test_matter_crud(app_mod):
    c = _existing(app_mod)
    # create (auto-creates + links the client)
    m = c.post(
        "/api/matters",
        json={
            "title": "Ahmet Y. – Boşanma",
            "client_name": "Ahmet Yılmaz",
            "court": "İstanbul 3. Aile",
        },
    ).json()
    mid = m["id"]
    assert m["title"].startswith("Ahmet") and m["client"]["full_name"] == "Ahmet Yılmaz"
    assert m["court"] == "İstanbul 3. Aile"

    # list
    assert any(x["id"] == mid for x in c.get("/api/matters").json()["items"])
    # client was created
    assert any(cl["full_name"] == "Ahmet Yılmaz" for cl in c.get("/api/clients").json()["items"])

    # update
    assert c.patch(f"/api/matters/{mid}", json={"status": "closed"}).json()["status"] == "closed"

    # hearing (near future so it lands in the calendar window)
    fdate = (date.today() + timedelta(days=5)).isoformat()
    h = c.post(
        f"/api/matters/{mid}/hearings", json={"date": fdate, "time": "10:30", "type": "ön inceleme"}
    )
    assert h.status_code == 200 and h.json()["date"] == fdate and h.json()["time"] == "10:30"

    # detail includes the hearing; calendar surfaces it
    assert len(c.get(f"/api/matters/{mid}").json()["hearings"]) == 1
    assert any(
        x["matter_id"] == mid for x in c.get("/api/calendar/upcoming?days=14").json()["items"]
    )

    # delete → gone
    assert c.delete(f"/api/matters/{mid}").json()["ok"] is True
    assert c.get(f"/api/matters/{mid}").status_code == 404


def test_bad_hearing_date_is_400(app_mod):
    c = _existing(app_mod)
    mid = c.post("/api/matters", json={"title": "Dosya"}).json()["id"]
    assert c.post(f"/api/matters/{mid}/hearings", json={"date": "12 temmuz"}).status_code == 400


def test_session_links_to_matter(app_mod):
    c = _existing(app_mod)
    mid = c.post("/api/matters", json={"title": "Dosya X"}).json()["id"]
    sess = c.post(f"/api/sessions?matter_id={mid}").json()
    assert sess["matter_id"] == mid
    # an unknown matter is rejected
    assert c.post("/api/sessions?matter_id=9999").status_code == 404


def test_matter_not_found_is_404(app_mod):
    c = _existing(app_mod)
    # A nonexistent matter is a 404 on every route.
    assert c.get("/api/matters/9999").status_code == 404
    assert c.patch("/api/matters/9999", json={"status": "closed"}).status_code == 404
    assert c.post("/api/matters/9999/hearings", json={"date": "2099-01-01"}).status_code == 404
    assert c.delete("/api/matters/9999").status_code == 404


def test_deadlines_and_unified_agenda(app_mod):
    c = _existing(app_mod)
    mid = c.post("/api/matters", json={"title": "Dosya"}).json()["id"]
    soon = (date.today() + timedelta(days=3)).isoformat()

    d = c.post(
        f"/api/matters/{mid}/deadlines",
        json={"due_date": soon, "title": "Cevap süresi", "type": "cevap"},
    )
    assert d.status_code == 200 and d.json()["status"] == "pending"
    did = d.json()["id"]
    c.post(f"/api/matters/{mid}/hearings", json={"date": soon, "time": "09:30"})

    # The calendar is unified: a hearing AND a deadline, each tagged with `kind`.
    items = c.get("/api/calendar/upcoming?days=14").json()["items"]
    assert sorted(i["kind"] for i in items) == ["deadline", "hearing"]
    # Matter detail carries its deadlines (the file's timeline).
    assert len(c.get(f"/api/matters/{mid}").json()["deadlines"]) == 1

    # Completing a deadline drops it from the agenda.
    assert c.post(f"/api/deadlines/{did}/complete").json()["status"] == "done"
    assert all(
        i["kind"] != "deadline" for i in c.get("/api/calendar/upcoming?days=14").json()["items"]
    )


def test_deadline_not_found_is_404(app_mod):
    c = _existing(app_mod)
    # A deadline on a nonexistent matter, and completing a nonexistent deadline, are both 404.
    assert (
        c.post(
            "/api/matters/9999/deadlines", json={"due_date": "2099-01-01", "title": "X"}
        ).status_code
        == 404
    )
    assert c.post("/api/deadlines/9999/complete").status_code == 404


def test_matter_detail_includes_documents(app_mod):
    c = _existing(app_mod)
    mid = c.post("/api/matters", json={"title": "X"}).json()["id"]
    # The full matter view always carries a documents list (empty until drafts are filed here).
    assert c.get(f"/api/matters/{mid}").json()["documents"] == []


def test_requires_auth(app_mod):
    c = TestClient(app_mod.app)  # no login
    assert c.get("/api/matters").status_code == 401
    assert c.post("/api/matters", json={"title": "x"}).status_code == 401


def test_collect_known_entities_gathers_stored_pii(app_mod):
    # Layer 4 source: the exact PII the office stores becomes (value, kind) pairs for the redactor.
    from sqlalchemy import select

    from korkut.api import matters_store as ms
    from korkut.api.models import Client, Matter, User
    from korkut.api.store import session

    with session() as s:
        s.scalar(select(User)).bar_registry_no = "34567"  # the bootstrapped lawyer's bar number
        s.add(
            Client(
                full_name="Oğuzhan Karaca",
                national_id="10000000146",
                phone="05321234567",
                email="o@x.tr",
                address="Bağdat Cad. No:12",
                date_of_birth=date(1990, 1, 1),
            )
        )
        s.add(Matter(title="Dava", counterparty="Cengiz Aktürk"))
        s.commit()

    with session() as s:
        entities = ms.collect_known_entities(s)

    assert ("Oğuzhan Karaca", "PERSON") in entities  # client name
    assert ("10000000146", "TCKN") in entities
    assert ("05321234567", "PHONE") in entities
    assert ("o@x.tr", "EMAIL") in entities
    assert ("Bağdat Cad. No:12", "ADDRESS") in entities
    assert ("01.01.1990", "DOB") in entities  # birth date, formatted dd.mm.yyyy
    assert ("Cengiz Aktürk", "PERSON") in entities  # matter counterparty
    assert ("Av. Test", "PERSON") in entities  # bootstrapped lawyer (adduser fixture)
    assert ("34567", "BAR_NO") in entities  # lawyer bar-registry number


def test_client_create_and_update_pii(app_mod):
    c = _existing(app_mod)  # authenticated TestClient (2FA-enrolled)
    created = c.post(
        "/api/clients",
        json={
            "full_name": "Ayşe Yıldız",
            "national_id": "10000000146",
            "address": "Moda Cad. 5",
            "date_of_birth": "1985-06-15",
        },
    ).json()
    assert created["national_id"] == "10000000146"
    assert created["address"] == "Moda Cad. 5"
    assert created["date_of_birth"] == "1985-06-15"

    updated = c.patch(
        f"/api/clients/{created['id']}", json={"phone": "05321234567", "address": ""}
    ).json()
    assert updated["phone"] == "05321234567"
    assert updated["address"] is None  # cleared
    assert updated["national_id"] == "10000000146"  # untouched fields survive
    assert c.patch("/api/clients/99999", json={"phone": "x"}).status_code == 404


def test_collect_memory_formats_notes_as_bullets(app_mod):
    from korkut.api.models import MemoryItem
    from korkut.api.store import collect_memory, session

    with session() as s:
        assert collect_memory(s) == ""  # empty before any note

    with session() as s:
        s.add(MemoryItem(text="Video duruşma tercih edilir"))
        s.add(MemoryItem(text="İcra dosyalarında X şablonu kullanılır"))
        s.add(MemoryItem(text="   "))  # blank is skipped
        s.commit()

    with session() as s:
        block = collect_memory(s)
    assert "- Video duruşma tercih edilir" in block
    assert "- İcra dosyalarında X şablonu kullanılır" in block
    assert block.count("\n") == 1  # two bullets, one newline between; blank skipped


def test_collect_memory_excludes_pending(app_mod):
    from korkut.api.models import MemoryItem
    from korkut.api.store import collect_memory, session

    with session() as s:
        s.add(MemoryItem(text="Onaylı not", status="active"))
        s.add(MemoryItem(text="Bekleyen öneri", status="pending"))
        s.commit()

    with session() as s:
        block = collect_memory(s)
    assert "Onaylı not" in block
    assert "Bekleyen öneri" not in block  # pending proposals never reach the model


def test_finance_invoices_expenses_and_summary(app_mod):
    c = _existing(app_mod)
    cid = c.post("/api/clients", json={"full_name": "Ayşe Yıldız"}).json()["id"]

    # Invoice: net 1000 + %20 KDV → gross 1200; issued so it counts toward the account.
    inv = c.post(
        "/api/finance/invoices",
        json={"client_id": cid, "amount": 1000, "vat_rate": 20, "status": "issued"},
    ).json()
    assert inv["amount"] == 1000.0 and inv["gross"] == 1200.0 and inv["status"] == "issued"

    c.post("/api/finance/expenses", json={"client_id": cid, "amount": 150, "kind": "harç"})

    summ = c.get(f"/api/finance/summary?client_id={cid}").json()
    assert summ == {"invoiced": 1000.0, "paid": 0.0, "outstanding": 1000.0, "expenses": 150.0}

    # Mark paid → collected rises, outstanding clears.
    c.patch(f"/api/finance/invoices/{inv['id']}", json={"status": "paid"})
    summ2 = c.get(f"/api/finance/summary?client_id={cid}").json()
    assert summ2["paid"] == 1000.0 and summ2["outstanding"] == 0.0

    assert len(c.get(f"/api/finance/invoices?client_id={cid}").json()["items"]) == 1
    c.delete(f"/api/finance/invoices/{inv['id']}")
    assert c.get(f"/api/finance/invoices?client_id={cid}").json()["items"] == []
    assert c.patch("/api/finance/invoices/99999", json={"status": "paid"}).status_code == 404


def test_tasks_crud_assign_and_filter(app_mod):
    c = _existing(app_mod)
    me = next(m for m in c.get("/api/members").json() if m["username"] == "avukat")

    t = c.post(
        "/api/tasks",
        json={"title": "Cevap dilekçesi yaz", "assignee_id": me["id"], "due_date": "2026-08-01"},
    ).json()
    assert t["title"] == "Cevap dilekçesi yaz"
    assert t["assignee_name"] == "Av. Test" and t["status"] == "open"
    assert t["due_date"] == "2026-08-01"

    assert len(c.get("/api/tasks?status=open").json()["items"]) == 1

    done = c.patch(f"/api/tasks/{t['id']}", json={"status": "done"}).json()
    assert done["status"] == "done"
    assert c.get("/api/tasks?status=open").json()["items"] == []
    assert len(c.get("/api/tasks").json()["items"]) == 1

    c.delete(f"/api/tasks/{t['id']}")
    assert c.get("/api/tasks").json()["items"] == []
    assert c.patch("/api/tasks/99999", json={"status": "done"}).status_code == 404


def test_evrak_register_crud_and_filter(app_mod):
    c = _existing(app_mod)
    mid = c.post("/api/matters", json={"title": "Dava"}).json()["id"]

    d = c.post(
        "/api/evrak",
        json={
            "matter_id": mid,
            "direction": "incoming",
            "title": "Tebligat",
            "reference_no": "2026/123",
            "doc_date": "2026-07-01",
        },
    ).json()
    assert d["title"] == "Tebligat" and d["direction"] == "incoming"
    assert d["reference_no"] == "2026/123" and d["doc_date"] == "2026-07-01"

    c.post("/api/evrak", json={"direction": "outgoing", "title": "Cevap dilekçesi"})
    assert len(c.get("/api/evrak").json()["items"]) == 2
    assert len(c.get("/api/evrak?direction=incoming").json()["items"]) == 1
    assert len(c.get(f"/api/evrak?matter_id={mid}").json()["items"]) == 1

    c.delete(f"/api/evrak/{d['id']}")
    assert len(c.get("/api/evrak").json()["items"]) == 1
    assert c.post("/api/evrak", json={"title": "   "}).status_code == 400


def test_reports_overview_rolls_up_every_module(app_mod):
    c = _existing(app_mod)
    me = next(m for m in c.get("/api/members").json() if m["username"] == "avukat")
    cid = c.post("/api/clients", json={"full_name": "Ayşe Yıldız"}).json()["id"]
    mid = c.post("/api/matters", json={"title": "Dava", "client_name": "Ayşe Yıldız"}).json()["id"]

    # Finance: one paid invoice + one expense.
    inv = c.post(
        "/api/finance/invoices",
        json={"client_id": cid, "amount": 1000, "vat_rate": 20, "status": "issued"},
    ).json()
    c.patch(f"/api/finance/invoices/{inv['id']}", json={"status": "paid"})
    c.post("/api/finance/expenses", json={"client_id": cid, "amount": 150, "kind": "harç"})

    # Tasks: one open-and-overdue, one done.
    past = (date.today() - timedelta(days=2)).isoformat()
    c.post("/api/tasks", json={"title": "Geciken görev", "assignee_id": me["id"], "due_date": past})
    done = c.post("/api/tasks", json={"title": "Biten görev"}).json()
    c.patch(f"/api/tasks/{done['id']}", json={"status": "done"})

    # Agenda: an upcoming hearing, an upcoming deadline, and an overdue deadline.
    soon = (date.today() + timedelta(days=3)).isoformat()
    c.post(f"/api/matters/{mid}/hearings", json={"date": soon, "time": "09:30"})
    c.post(f"/api/matters/{mid}/deadlines", json={"due_date": soon, "title": "Cevap süresi"})
    c.post(f"/api/matters/{mid}/deadlines", json={"due_date": past, "title": "Geçen süre"})

    # Evrak: one incoming, one outgoing.
    c.post("/api/evrak", json={"matter_id": mid, "direction": "incoming", "title": "Tebligat"})
    c.post("/api/evrak", json={"direction": "outgoing", "title": "Cevap"})

    o = c.get("/api/reports/overview").json()
    assert o["matters"]["total"] == 1 and o["matters"]["by_status"]["open"] == 1
    assert o["clients"]["total"] == 1
    assert o["finance"] == {
        "invoiced": 1000.0,
        "paid": 1000.0,
        "outstanding": 0.0,
        "expenses": 150.0,
    }
    assert o["tasks"] == {"open": 1, "done": 1, "overdue": 1}
    assert o["agenda"]["hearings_upcoming"] == 1
    assert o["agenda"]["deadlines_upcoming"] == 1
    assert o["agenda"]["deadlines_overdue"] == 1
    assert o["evrak"]["total"] == 2
    assert o["evrak"]["by_direction"] == {"incoming": 1, "outgoing": 1}


def test_reports_overview_requires_auth(app_mod):
    assert TestClient(app_mod.app).get("/api/reports/overview").status_code == 401


def test_calendar_events_crud_and_range(app_mod):
    c = _existing(app_mod)
    cid = c.post("/api/clients", json={"full_name": "Ayşe Yıldız"}).json()["id"]

    # A client meeting (appointment) with a full datetime, linked to the client.
    ev = c.post(
        "/api/calendar/events",
        json={
            "category": "appointment",
            "type": "müvekkil görüşme",
            "title": "İlk görüşme",
            "start_at": "2026-08-10T14:30:00",
            "client_id": cid,
            "location": "Ofis",
        },
    ).json()
    assert ev["category"] == "appointment" and ev["title"] == "İlk görüşme"
    assert ev["client_id"] == cid and ev["client_name"] == "Ayşe Yıldız"
    assert ev["start_at"].startswith("2026-08-10T14:30") and ev["status"] == "planned"

    # A reminder later in the year.
    c.post(
        "/api/calendar/events",
        json={"category": "reminder", "title": "Vekalet bitişi", "start_at": "2026-12-01"},
    )

    # Range filter: only the August event falls in the window.
    aug = c.get("/api/calendar/events?start=2026-08-01T00:00:00&end=2026-08-31T23:59:59").json()
    assert [e["title"] for e in aug["items"]] == ["İlk görüşme"]
    # Category filter across the whole year.
    reminders = c.get("/api/calendar/events?category=reminder").json()
    assert [e["title"] for e in reminders["items"]] == ["Vekalet bitişi"]

    # Patch: move it and mark done.
    upd = c.patch(
        f"/api/calendar/events/{ev['id']}",
        json={"start_at": "2026-08-11T15:00:00", "status": "done"},
    ).json()
    assert upd["start_at"].startswith("2026-08-11T15:00") and upd["status"] == "done"

    c.delete(f"/api/calendar/events/{ev['id']}")
    assert [e["title"] for e in c.get("/api/calendar/events").json()["items"]] == ["Vekalet bitişi"]
    assert c.patch("/api/calendar/events/99999", json={"title": "x"}).status_code == 404
    assert (
        c.post("/api/calendar/events", json={"title": "  ", "start_at": "2026-01-01"}).status_code
        == 400
    )


def test_recurring_events_expand_within_the_window(app_mod):
    c = _existing(app_mod)
    # A weekly meeting every Monday in August, ending mid-month.
    ev = c.post(
        "/api/calendar/events",
        json={
            "category": "appointment",
            "title": "Ekip toplantısı",
            "start_at": "2026-08-03T10:00:00",  # a Monday
            "rrule": "FREQ=WEEKLY;INTERVAL=1;UNTIL=2026-08-24",
        },
    ).json()
    assert ev["rrule"] == "FREQ=WEEKLY;INTERVAL=1;UNTIL=2026-08-24"

    # August window: occurrences on the 3rd, 10th, 17th, 24th (UNTIL caps it — no 31st).
    aug = c.get("/api/calendar/events?start=2026-08-01T00:00:00&end=2026-08-31T23:59:59").json()
    dates = [e["start_at"][:10] for e in aug["items"]]
    assert dates == ["2026-08-03", "2026-08-10", "2026-08-17", "2026-08-24"]
    assert all(e["recurring"] and e["id"] == ev["id"] for e in aug["items"])
    assert all(e["start_at"].endswith("10:00:00") for e in aug["items"])

    # A narrower window returns only the occurrences inside it.
    mid = c.get("/api/calendar/events?start=2026-08-09T00:00:00&end=2026-08-18T23:59:59").json()
    assert [e["start_at"][:10] for e in mid["items"]] == ["2026-08-10", "2026-08-17"]

    # A bogus FREQ is dropped: the event stays a single, non-recurring entry.
    single = c.post(
        "/api/calendar/events",
        json={
            "category": "reminder",
            "title": "Tek",
            "start_at": "2026-08-05",
            "rrule": "FREQ=YEARLY",
        },
    ).json()
    assert single["rrule"] is None


def test_hearings_and_deadlines_share_the_event_table(app_mod):
    # Hearings and deadlines are now CalendarEvent rows; they surface via /api/calendar/events too.
    c = _existing(app_mod)
    mid = c.post("/api/matters", json={"title": "Dava"}).json()["id"]
    c.post(f"/api/matters/{mid}/hearings", json={"date": "2026-09-01", "time": "09:30"})
    c.post(f"/api/matters/{mid}/deadlines", json={"due_date": "2026-09-05", "title": "Cevap"})

    cats = sorted(
        e["category"] for e in c.get(f"/api/calendar/events?matter_id={mid}").json()["items"]
    )
    assert cats == ["deadline", "hearing"]
    # The legacy matter-detail shape still lists them split out.
    detail = c.get(f"/api/matters/{mid}").json()
    assert len(detail["hearings"]) == 1 and len(detail["deadlines"]) == 1
    assert detail["hearings"][0]["time"] == "09:30"
