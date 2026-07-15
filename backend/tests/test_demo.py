"""Self-serve DEMO REQUEST flow: a Google-verified new firm requests access, which creates a
DISABLED office + owner. No session is issued until a superadmin enables the office; disabling it
blocks sign-in again. The Google network is patched, so it stays offline.
"""

from __future__ import annotations

import importlib
from unittest.mock import patch

from fastapi.testclient import TestClient

from korkut.api import google_auth, google_oidc


def _fresh(tmp_path, monkeypatch, *, demo: bool = True):
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 't.db'}")
    monkeypatch.setenv("KORKUT_SECRET", "test-secret-stable-0123456789abcdef")
    monkeypatch.setenv("GOOGLE_CLIENT_ID", "cid")
    monkeypatch.setenv("GOOGLE_CLIENT_SECRET", "csecret")
    monkeypatch.setenv("GOOGLE_REDIRECT_URI", "https://korkut.test/api/auth/google/callback")
    monkeypatch.setenv("KORKUT_ALLOW_DEMO", "1" if demo else "0")

    import korkut.api.auth as auth_mod
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    auth_mod._secret_cache = None
    import korkut.api.app as app_mod

    importlib.reload(app_mod)
    return app_mod, store_mod


def _callback(app_mod, *, sub, email, picture=None):
    c = TestClient(app_mod.app)
    identity = google_oidc.GoogleIdentity(sub=sub, email=email, hd=None, picture=picture)
    state = google_auth.make_state()
    with (
        patch("korkut.api.app.google_auth.exchange_code", return_value="idtok"),
        patch("korkut.api.app.google_oidc.verify_id_token", return_value=identity),
    ):
        r = c.get(f"/api/auth/google/callback?code=x&state={state}", follow_redirects=False)
    return c, r


def _enable_office(store_mod, office_id):
    from korkut.api.models import Office
    from korkut.core.tenancy import reset_current_office, set_current_office

    tok = set_current_office(office_id)
    try:
        with store_mod.session() as s:
            s.get(Office, office_id).disabled = False
            s.commit()
    finally:
        reset_current_office(tok)


def test_new_user_gets_the_request_screen(tmp_path, monkeypatch):
    app_mod, _ = _fresh(tmp_path, monkeypatch, demo=True)
    c, r = _callback(app_mod, sub="s", email="founder@firm.co")
    assert r.status_code == 303 and r.headers["location"] == "/kayit"
    assert c.get("/api/signup/pending").json()["email"] == "founder@firm.co"


def test_demo_request_creates_a_disabled_office_and_no_session(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch, demo=True)
    c, _ = _callback(app_mod, sub="s1", email="founder@firm.co")

    r = c.post("/api/demo-request", json={"office_name": "Yeni Hukuk"})
    assert r.status_code == 200 and r.json() == {"pending": True}
    # NO session was issued — the owner can't enter yet
    assert c.get("/api/me").status_code == 401

    from korkut.api.models import Office, User
    from korkut.core.tenancy import reset_current_office, set_current_office

    with store_mod.session() as s:
        office = s.scalars(store_mod.select(Office)).one()
        assert office.name == "Yeni Hukuk" and office.disabled is True
        oid = office.id
    tok = set_current_office(oid)
    try:
        with store_mod.session() as s:
            u = s.scalars(store_mod.select(User)).one()
            assert u.role == "owner" and u.email == "founder@firm.co"
    finally:
        reset_current_office(tok)


def test_demo_request_alerts_the_operator(tmp_path, monkeypatch):
    monkeypatch.setenv("KORKUT_ADMIN_NOTIFY_EMAIL", "ops@korkut.test, second@korkut.test")
    app_mod, _ = _fresh(tmp_path, monkeypatch, demo=True)
    from korkut.messaging import SendResult

    sent: list[tuple] = []

    def _fake(to_addr, subject, body):
        sent.append((to_addr, subject, body))
        return SendResult(ok=True)

    monkeypatch.setattr(app_mod, "send_simple_email", _fake)
    c, _ = _callback(app_mod, sub="s1", email="founder@firm.co")
    r = c.post("/api/demo-request", json={"office_name": "Yeni Hukuk"})
    assert r.status_code == 200
    # both operators alerted, and the office/e-mail appear in the body
    assert [x[0] for x in sent] == ["ops@korkut.test", "second@korkut.test"]
    assert "Yeni Hukuk" in sent[0][2] and "founder@firm.co" in sent[0][2]


def test_demo_request_without_notify_env_sends_nothing(tmp_path, monkeypatch):
    monkeypatch.delenv("KORKUT_ADMIN_NOTIFY_EMAIL", raising=False)
    app_mod, _ = _fresh(tmp_path, monkeypatch, demo=True)
    from korkut.messaging import SendResult

    calls: list = []
    monkeypatch.setattr(
        app_mod, "send_simple_email", lambda *a: calls.append(a) or SendResult(ok=True)
    )
    c, _ = _callback(app_mod, sub="s1", email="founder@firm.co")
    assert c.post("/api/demo-request", json={"office_name": "Yeni Hukuk"}).status_code == 200
    assert calls == []


def test_demo_owner_gets_google_picture_at_creation(tmp_path, monkeypatch):
    # The Google profile photo rides the signup ticket, so the owner row has it immediately —
    # no need to wait for the first post-approval sign-in.
    app_mod, store_mod = _fresh(tmp_path, monkeypatch, demo=True)
    pic = "https://lh3.googleusercontent.com/a/founder-photo"
    c, _ = _callback(app_mod, sub="s1", email="founder@firm.co", picture=pic)
    c.post("/api/demo-request", json={"office_name": "Yeni Hukuk"})

    from korkut.api.models import Office, User
    from korkut.core.tenancy import reset_current_office, set_current_office

    with store_mod.session() as s:
        oid = s.scalars(store_mod.select(Office)).one().id
    tok = set_current_office(oid)
    try:
        with store_mod.session() as s:
            u = s.scalars(store_mod.select(User)).one()
            assert u.picture == pic
    finally:
        reset_current_office(tok)


def test_owner_cannot_sign_in_until_approved(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch, demo=True)
    c, _ = _callback(app_mod, sub="s1", email="founder@firm.co")
    c.post("/api/demo-request", json={"office_name": "Yeni Hukuk"})

    # signing in again while the office is disabled -> pending, still no session
    c2, r = _callback(app_mod, sub="s1", email="founder@firm.co")
    assert r.status_code == 303 and "error=pending" in r.headers["location"]
    assert c2.get("/api/me").status_code == 401

    # a superadmin approves (enables) the office -> sign-in now issues a session
    from korkut.api.models import Office

    with store_mod.session() as s:
        oid = s.scalars(store_mod.select(Office)).one().id
    _enable_office(store_mod, oid)

    c3, r3 = _callback(app_mod, sub="s1", email="founder@firm.co")
    assert r3.status_code == 303 and r3.headers["location"] == "/"
    me = c3.get("/api/me").json()
    assert me["email"] == "founder@firm.co" and me["office"] == "Yeni Hukuk"


def test_existing_account_is_conflict(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch, demo=True)
    from korkut.api import session as sess
    from korkut.api.models import Office, User
    from korkut.core.tenancy import reset_current_office, set_current_office

    with store_mod.session() as s:
        o = Office(name="Mevcut")
        s.add(o)
        s.commit()
        oid = o.id
    tok = set_current_office(oid)
    try:
        with store_mod.session() as s:
            s.add(User(role="owner", email="dupe@firm.co", username="dupe", google_sub="existing"))
            s.commit()
    finally:
        reset_current_office(tok)

    # a demo request whose ticket carries that already-registered email is rejected
    c = TestClient(app_mod.app)
    c.cookies.set("korkut_signup", sess.mint_signup_ticket("new-sub", "dupe@firm.co"))
    assert c.post("/api/demo-request", json={"office_name": "X"}).status_code == 409


def test_demo_disabled_closes_the_door(tmp_path, monkeypatch):
    app_mod, _ = _fresh(tmp_path, monkeypatch, demo=False)
    _c, r = _callback(app_mod, sub="s", email="founder@firm.co")
    assert r.status_code == 303 and "error=no_office" in r.headers["location"]
    # the endpoint is closed too
    assert (
        TestClient(app_mod.app).post("/api/demo-request", json={"office_name": "X"}).status_code
        == 404
    )
