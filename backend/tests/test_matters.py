"""HTTP endpoint tests for the matter domain (/api/matters, /api/clients, /api/calendar).

Mirrors test_web_auth.py: a fresh SQLite file per run, the brain patched offline, and the full
two-step + 2FA login to obtain an authenticated client. Covers CRUD, the chat↔matter link, and
cross-office isolation (another office's matter is a 404, never visible).
"""

import time
from datetime import date, timedelta
from unittest.mock import patch

import pytest
from fastapi.testclient import TestClient

from korkut.api import totp


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


def _totp_now(secret: str) -> str:
    return totp._hotp(secret, totp.counter_at(time.time()))


def _enroll(c: TestClient, username: str, password: str) -> None:
    c.post("/api/login", json={"username": username, "password": password})
    secret = c.get("/api/2fa/provision").json()["secret"]
    c.post("/api/2fa/enroll", json={"code": _totp_now(secret)})


def _existing(app_mod) -> TestClient:
    c = TestClient(app_mod.app)
    _enroll(c, "avukat", "parola123")
    return c


def _new_office(app_mod, office: str, username: str) -> TestClient:
    c = TestClient(app_mod.app)
    c.post(
        "/api/register",
        json={
            "office_name": office,
            "full_name": username,
            "username": username,
            "password": "parola123",
        },
    )
    secret = c.get("/api/2fa/provision").json()["secret"]
    c.post("/api/2fa/enroll", json={"code": _totp_now(secret)})
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
    # an unknown / other-office matter is rejected
    assert c.post("/api/sessions?matter_id=9999").status_code == 404


def test_office_isolation(app_mod):
    a = _existing(app_mod)
    b = _new_office(app_mod, "Ofis B", "bavukat")
    mid = a.post("/api/matters", json={"title": "Gizli Dosya"}).json()["id"]

    # B can neither see nor touch A's matter — always 404, never a leak.
    assert all(x["id"] != mid for x in b.get("/api/matters").json()["items"])
    assert b.get(f"/api/matters/{mid}").status_code == 404
    assert b.patch(f"/api/matters/{mid}", json={"status": "closed"}).status_code == 404
    assert b.post(f"/api/matters/{mid}/hearings", json={"date": "2099-01-01"}).status_code == 404
    assert b.delete(f"/api/matters/{mid}").status_code == 404


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


def test_deadline_office_isolation(app_mod):
    a = _existing(app_mod)
    b = _new_office(app_mod, "Ofis B", "bavukat")
    mid = a.post("/api/matters", json={"title": "X"}).json()["id"]
    did = a.post(
        f"/api/matters/{mid}/deadlines", json={"due_date": "2099-01-01", "title": "Süre"}
    ).json()["id"]
    assert (
        b.post(
            f"/api/matters/{mid}/deadlines", json={"due_date": "2099-01-01", "title": "X"}
        ).status_code
        == 404
    )
    assert b.post(f"/api/deadlines/{did}/complete").status_code == 404


def test_matter_detail_includes_documents(app_mod):
    c = _existing(app_mod)
    mid = c.post("/api/matters", json={"title": "X"}).json()["id"]
    # The full matter view always carries a documents list (empty until drafts are filed here).
    assert c.get(f"/api/matters/{mid}").json()["documents"] == []


def test_requires_auth(app_mod):
    c = TestClient(app_mod.app)  # no login
    assert c.get("/api/matters").status_code == 401
    assert c.post("/api/matters", json={"title": "x"}).status_code == 401
