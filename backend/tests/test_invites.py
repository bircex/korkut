"""Invite-token onboarding: an owner issues a one-time invite; a new person redeems it via Google
sign-in and joins THAT office. Scoping, expiry, single-use, and the email pin are all enforced, and
the raw token is never stored. The Google network calls are patched, so it stays offline.
"""

from __future__ import annotations

import importlib
from datetime import UTC, datetime, timedelta
from unittest.mock import patch

from fastapi.testclient import TestClient

from korkut.api import google_auth, google_oidc


def _fresh(tmp_path, monkeypatch):
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 't.db'}")
    monkeypatch.setenv("KORKUT_SECRET", "test-secret-stable-0123456789abcdef")
    monkeypatch.setenv("GOOGLE_CLIENT_ID", "cid")
    monkeypatch.setenv("GOOGLE_CLIENT_SECRET", "csecret")
    monkeypatch.setenv("GOOGLE_REDIRECT_URI", "https://korkut.test/api/auth/google/callback")

    import korkut.api.auth as auth_mod
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    auth_mod._secret_cache = None
    import korkut.api.app as app_mod

    importlib.reload(app_mod)
    return app_mod, store_mod


def _owner_client(app_mod, store_mod, *, name="A", owner_email="owner@a.co"):
    from korkut.api import auth, session
    from korkut.api.models import Office, User
    from korkut.core.tenancy import reset_current_office, set_current_office

    with store_mod.session() as s:
        o = Office(name=name)
        s.add(o)
        s.commit()
        oid = o.id
    tok = set_current_office(oid)
    try:
        with store_mod.session() as s:
            u = User(role="owner", email=owner_email, username="owner", google_sub=f"sub-{name}")
            s.add(u)
            s.commit()
            uid, epoch = u.id, u.token_epoch
    finally:
        reset_current_office(tok)
    c = TestClient(app_mod.app)
    c.cookies.set(auth.COOKIE_NAME, session.mint(uid, oid, "owner", epoch))
    return c, oid


def _redeem(app_mod, token, *, sub, email):
    """Run the callback carrying the invite cookie + a mocked Google identity."""
    c = TestClient(app_mod.app)
    c.cookies.set("korkut_invite", token)
    identity = google_oidc.GoogleIdentity(sub=sub, email=email, hd=None)
    state = google_auth.make_state()
    with (
        patch("korkut.api.app.google_auth.exchange_code", return_value="idtok"),
        patch("korkut.api.app.google_oidc.verify_id_token", return_value=identity),
    ):
        r = c.get(f"/api/auth/google/callback?code=x&state={state}", follow_redirects=False)
    return c, r


def _force_expire(store_mod, oid):
    from korkut.api.models import OfficeInvite
    from korkut.core.tenancy import reset_current_office, set_current_office

    tok = set_current_office(oid)
    try:
        with store_mod.session() as s:
            inv = s.scalars(store_mod.select(OfficeInvite)).one()
            inv.expires_at = datetime.now(UTC).replace(tzinfo=None) - timedelta(minutes=1)
            s.commit()
    finally:
        reset_current_office(tok)


def test_owner_issues_and_a_new_user_redeems(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch)
    owner, _ = _owner_client(app_mod, store_mod)

    body = owner.post("/api/invites", json={"role": "member"}).json()
    assert body["redeemed"] is False and body["role"] == "member"
    assert "google/start?invite=" in body["path"]

    joiner, resp = _redeem(app_mod, body["token"], sub="new-sub", email="newbie@a.co")
    assert resp.status_code == 303
    me = joiner.get("/api/me").json()
    assert me["email"] == "newbie@a.co" and me["role"] == "member" and me["office"] == "A"

    # the invite is now spent, and the roster has both people
    invites = owner.get("/api/invites").json()
    assert len(invites) == 1 and invites[0]["redeemed"] is True
    assert len(owner.get("/api/members").json()) == 2


def test_only_the_hash_is_stored(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch)
    owner, oid = _owner_client(app_mod, store_mod)
    token = owner.post("/api/invites", json={}).json()["token"]

    from korkut.api.models import OfficeInvite
    from korkut.core.tenancy import reset_current_office, set_current_office

    tok = set_current_office(oid)
    try:
        with store_mod.session() as s:
            inv = s.scalars(store_mod.select(OfficeInvite)).one()
            assert inv.token_hash != token and token not in inv.token_hash
    finally:
        reset_current_office(tok)


def test_email_pinned_invite_rejects_other_email(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch)
    owner, _ = _owner_client(app_mod, store_mod)
    token = owner.post("/api/invites", json={"email": "pinned@a.co"}).json()["token"]
    _c, r = _redeem(app_mod, token, sub="s", email="someone-else@a.co")
    assert r.status_code == 303 and "error=invite" in r.headers["location"]


def test_redeemed_invite_cannot_be_reused(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch)
    owner, _ = _owner_client(app_mod, store_mod)
    token = owner.post("/api/invites", json={}).json()["token"]
    _c1, r1 = _redeem(app_mod, token, sub="s1", email="first@a.co")
    assert r1.status_code == 303 and r1.headers["location"] == "/"
    _c2, r2 = _redeem(app_mod, token, sub="s2", email="second@a.co")
    assert r2.status_code == 303 and "error=no_office" in r2.headers["location"]  # single-use


def test_expired_invite_is_not_redeemable(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch)
    owner, oid = _owner_client(app_mod, store_mod)
    token = owner.post("/api/invites", json={}).json()["token"]
    _force_expire(store_mod, oid)
    _c, r = _redeem(app_mod, token, sub="s", email="late@a.co")
    assert r.status_code == 303 and "error=no_office" in r.headers["location"]


def test_invites_are_office_scoped(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch)
    owner_a, _ = _owner_client(app_mod, store_mod, name="A", owner_email="owner@a.co")
    owner_b, _ = _owner_client(app_mod, store_mod, name="B", owner_email="owner@b.co")
    a_invite_id = owner_a.post("/api/invites", json={}).json()["id"]
    # office B neither sees nor can revoke office A's invite
    assert all(i["id"] != a_invite_id for i in owner_b.get("/api/invites").json())
    assert owner_b.delete(f"/api/invites/{a_invite_id}").status_code == 404


def test_member_cannot_issue_invites(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch)
    _owner, oid = _owner_client(app_mod, store_mod)

    from korkut.api import auth, session
    from korkut.api.models import User
    from korkut.core.tenancy import reset_current_office, set_current_office

    tok = set_current_office(oid)
    try:
        with store_mod.session() as s:
            m = User(role="member", email="m@a.co", username="m", google_sub="ms")
            s.add(m)
            s.commit()
            mid, mepoch = m.id, m.token_epoch
    finally:
        reset_current_office(tok)
    mc = TestClient(app_mod.app)
    mc.cookies.set(auth.COOKIE_NAME, session.mint(mid, oid, "member", mepoch))
    assert mc.post("/api/invites", json={}).status_code == 403
