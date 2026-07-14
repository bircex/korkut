"""Google OIDC sign-in end-to-end: state CSRF, code exchange, the callback binds the account and
issues a working session. The network (JWKS verify + token exchange) is patched, so it's offline.
"""

from __future__ import annotations

import importlib
import io
import json
from unittest.mock import patch

import pytest
from fastapi.testclient import TestClient

from korkut.api import google_auth, google_oidc


def _fresh(tmp_path, monkeypatch, *, configured: bool = True):
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 't.db'}")
    monkeypatch.setenv("KORKUT_SECRET", "test-secret-stable-0123456789abcdef")
    monkeypatch.setenv("KORKUT_ALLOW_DEMO", "0")
    if configured:
        monkeypatch.setenv("GOOGLE_CLIENT_ID", "cid")
        monkeypatch.setenv("GOOGLE_CLIENT_SECRET", "csecret")
        monkeypatch.setenv("GOOGLE_REDIRECT_URI", "https://korkut.test/api/auth/google/callback")
    else:
        monkeypatch.delenv("GOOGLE_CLIENT_ID", raising=False)

    import korkut.api.auth as auth_mod
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    auth_mod._secret_cache = None
    import korkut.api.app as app_mod

    importlib.reload(app_mod)
    return app_mod, store_mod


def _seed_owner(store_mod, *, email: str = "owner@x.co", sub: str | None = None):
    from korkut.api.models import Office, User
    from korkut.core.tenancy import reset_current_office, set_current_office

    with store_mod.session() as s:
        o = Office(name="Test Hukuk")
        s.add(o)
        s.commit()
        oid = o.id
    tok = set_current_office(oid)
    try:
        with store_mod.session() as s:
            u = User(role="owner", email=email, username="owner", google_sub=sub)
            s.add(u)
            s.commit()
            return oid, u.id
    finally:
        reset_current_office(tok)


class _Resp(io.BytesIO):
    def __enter__(self):
        return self

    def __exit__(self, *a):
        return False


def _opener(payload: dict):
    def opener(req, timeout=0):
        return _Resp(json.dumps(payload).encode())

    return opener


# ---------------- state (CSRF) ----------------


def test_state_roundtrip_and_expiry():
    st = google_auth.make_state(now=1000)
    assert google_auth.check_state(st, now=1000)
    assert google_auth.check_state(st, now=1000 + 599)
    assert not google_auth.check_state(st, now=1000 + 601)  # expired
    assert not google_auth.check_state(st + "x", now=1000)  # tampered signature
    assert not google_auth.check_state(None, now=1000)
    assert not google_auth.check_state("garbage", now=1000)


# ---------------- code exchange ----------------


def test_exchange_code_returns_id_token():
    cfg = google_oidc.GoogleConfig("cid", "sec", "uri")
    assert google_auth.exchange_code(cfg, "code", opener=_opener({"id_token": "tok"})) == "tok"


def test_exchange_code_without_token_raises():
    cfg = google_oidc.GoogleConfig("cid", "sec", "uri")
    with pytest.raises(google_oidc.GoogleAuthError):
        google_auth.exchange_code(cfg, "code", opener=_opener({"error": "invalid_grant"}))


# ---------------- start ----------------


def test_start_redirects_to_google_when_configured(tmp_path, monkeypatch):
    app_mod, _ = _fresh(tmp_path, monkeypatch)
    r = TestClient(app_mod.app).get("/api/auth/google/start", follow_redirects=False)
    assert r.status_code == 307
    assert "accounts.google.com" in r.headers["location"]
    assert "client_id=cid" in r.headers["location"]


def test_start_503_when_unconfigured(tmp_path, monkeypatch):
    app_mod, _ = _fresh(tmp_path, monkeypatch, configured=False)
    r = TestClient(app_mod.app).get("/api/auth/google/start", follow_redirects=False)
    assert r.status_code == 503


# ---------------- callback ----------------


def _callback(app_mod, identity):
    c = TestClient(app_mod.app)
    state = google_auth.make_state()
    with (
        patch("korkut.api.app.google_auth.exchange_code", return_value="idtok"),
        patch("korkut.api.app.google_oidc.verify_id_token", return_value=identity),
    ):
        r = c.get(f"/api/auth/google/callback?code=abc&state={state}", follow_redirects=False)
    return c, r


def test_callback_binds_sub_and_starts_a_working_session(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch)
    oid, uid = _seed_owner(store_mod, email="owner@x.co", sub=None)
    identity = google_oidc.GoogleIdentity(sub="google-123", email="owner@x.co", hd=None)

    c, r = _callback(app_mod, identity)
    assert r.status_code == 303
    assert r.headers["location"] == "/"

    me = c.get("/api/me")
    assert me.status_code == 200
    assert me.json()["email"] == "owner@x.co"

    # the Google sub was bound to the pre-provisioned row on first sign-in
    from korkut.api.models import User
    from korkut.core.tenancy import reset_current_office, set_current_office

    tok = set_current_office(oid)
    try:
        with store_mod.session() as s:
            assert s.get(User, uid).google_sub == "google-123"
    finally:
        reset_current_office(tok)


def test_callback_unknown_account_redirects_to_signin(tmp_path, monkeypatch):
    app_mod, store_mod = _fresh(tmp_path, monkeypatch)
    _seed_owner(store_mod, email="owner@x.co")
    identity = google_oidc.GoogleIdentity(sub="x", email="stranger@x.co", hd=None)
    _c, r = _callback(app_mod, identity)
    assert r.status_code == 303
    assert "error=no_office" in r.headers["location"]


def test_callback_rejects_bad_state(tmp_path, monkeypatch):
    app_mod, _ = _fresh(tmp_path, monkeypatch)
    r = TestClient(app_mod.app).get(
        "/api/auth/google/callback?code=abc&state=forged", follow_redirects=False
    )
    assert r.status_code == 400
