"""Self-serve office signup: a Google-verified new user with a valid code creates a new office and
becomes its owner. Gated by KORKUT_ALLOW_SIGNUP + a signup code; the Google network is patched.
"""

from __future__ import annotations

import importlib
from datetime import UTC, datetime, timedelta
from unittest.mock import patch

from fastapi.testclient import TestClient

from korkut.api import google_auth, google_oidc


def _fresh(tmp_path, monkeypatch, *, signup: bool = True):
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 't.db'}")
    monkeypatch.setenv("KORKUT_SECRET", "test-secret-stable-0123456789abcdef")
    monkeypatch.setenv("GOOGLE_CLIENT_ID", "cid")
    monkeypatch.setenv("GOOGLE_CLIENT_SECRET", "csecret")
    monkeypatch.setenv("GOOGLE_REDIRECT_URI", "https://korkut.test/api/auth/google/callback")
    if signup:
        monkeypatch.setenv("KORKUT_ALLOW_SIGNUP", "1")
    else:
        monkeypatch.delenv("KORKUT_ALLOW_SIGNUP", raising=False)

    import korkut.api.auth as auth_mod
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    auth_mod._secret_cache = None
    import korkut.api.app as app_mod

    importlib.reload(app_mod)
    return app_mod, store_mod


def _make_code(store_mod, code="LETMEIN", *, max_uses=1, disabled=False, expires_at=None):
    from korkut.api import auth
    from korkut.api.models import SignupCode

    with store_mod.session() as s:
        row = SignupCode(
            code_hash=auth.token_hash(code),
            max_uses=max_uses,
            disabled=disabled,
            expires_at=expires_at,
        )
        s.add(row)
        s.commit()
        return row.id


def _signed_in_new_user(app_mod, *, sub="new-sub", email="new@firm.co"):
    """Run the callback for an account that doesn't exist yet; returns a client holding the signup
    cookie (signup enabled) and the callback response."""
    c = TestClient(app_mod.app)
    identity = google_oidc.GoogleIdentity(sub=sub, email=email, hd=None)
    state = google_auth.make_state()
    with (
        patch("korkut.api.app.google_auth.exchange_code", return_value="idtok"),
        patch("korkut.api.app.google_oidc.verify_id_token", return_value=identity),
    ):
        r = c.get(f"/api/auth/google/callback?code=x&state={state}", follow_redirects=False)
    return c, r


def test_callback_offers_signup_when_enabled(tmp_path, monkeypatch):
    app_mod, _ = _fresh(tmp_path, monkeypatch, signup=True)
    c, r = _signed_in_new_user(app_mod, email="founder@firm.co")
    assert r.status_code == 303 and r.headers["location"] == "/kayit"
    # the name-your-office screen can read the verified email from the signup ticket
    assert c.get("/api/signup/pending").json()["email"] == "founder@firm.co"


def test_callback_no_signup_when_disabled(tmp_path, monkeypatch):
    app_mod, _ = _fresh(tmp_path, monkeypatch, signup=False)
    _c, r = _signed_in_new_user(app_mod)
    assert r.status_code == 303 and "error=no_office" in r.headers["location"]


def test_signup_creates_office_and_owner(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch, signup=True)
    _make_code(store_mod, "GOODCODE", max_uses=2)
    c, _ = _signed_in_new_user(app_mod, sub="s1", email="founder@firm.co")

    r = c.post("/api/signup", json={"code": "GOODCODE", "office_name": "Yeni Hukuk"})
    assert r.status_code == 200
    assert r.json()["user"]["role"] == "owner"

    me = c.get("/api/me").json()
    assert me["email"] == "founder@firm.co" and me["office"] == "Yeni Hukuk"

    # the code's use was counted, and the owner is bound to the office as its only member
    from korkut.api.models import SignupCode, User
    from korkut.core.tenancy import reset_current_office, set_current_office

    with store_mod.session() as s:
        assert s.scalars(store_mod.select(SignupCode)).one().used_count == 1
    tok = set_current_office(1)
    try:
        with store_mod.session() as s:
            owners = [u.email for u in s.scalars(store_mod.select(User)).all()]
            assert owners == ["founder@firm.co"]
    finally:
        reset_current_office(tok)


def test_signup_bad_code_is_rejected(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch, signup=True)
    _make_code(store_mod, "GOODCODE")
    c, _ = _signed_in_new_user(app_mod)
    assert c.post("/api/signup", json={"code": "WRONG", "office_name": "X"}).status_code == 403


def test_signup_code_respects_max_uses(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch, signup=True)
    _make_code(store_mod, "ONCE", max_uses=1)
    c1, _ = _signed_in_new_user(app_mod, sub="s1", email="a@firm.co")
    assert c1.post("/api/signup", json={"code": "ONCE", "office_name": "A"}).status_code == 200
    c2, _ = _signed_in_new_user(app_mod, sub="s2", email="b@firm.co")
    assert c2.post("/api/signup", json={"code": "ONCE", "office_name": "B"}).status_code == 403


def test_signup_expired_code_is_rejected(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch, signup=True)
    past = datetime.now(UTC).replace(tzinfo=None) - timedelta(minutes=1)
    _make_code(store_mod, "OLD", expires_at=past)
    c, _ = _signed_in_new_user(app_mod)
    assert c.post("/api/signup", json={"code": "OLD", "office_name": "X"}).status_code == 403


def test_signup_existing_account_is_conflict(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch, signup=True)
    _make_code(store_mod, "GOODCODE")
    # seed an office + owner with this email already
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

    c, _ = _signed_in_new_user(app_mod, sub="new", email="dupe@firm.co")
    assert c.post("/api/signup", json={"code": "GOODCODE", "office_name": "X"}).status_code == 409


def test_signup_endpoint_404_when_disabled(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch, signup=False)
    _make_code(store_mod, "GOODCODE")
    # no signup cookie can exist (callback wouldn't issue one), but the endpoint is closed anyway
    assert (
        TestClient(app_mod.app)
        .post("/api/signup", json={"code": "GOODCODE", "office_name": "X"})
        .status_code
        == 404
    )
