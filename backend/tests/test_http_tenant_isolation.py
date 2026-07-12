"""HTTP-level tenant isolation: with the tenant middleware wired, a request authenticated as one
office sees only that office's data — even when it asks for another office's ids directly (IDOR).

End-to-end proof that the request context is established from the session cookie: two offices share
ONE store, and each office's cookie scopes every read to its own tenant. This is the load-bearing
guarantee the app-layer filter + RLS only deliver once the middleware pins the context per request.
"""

from __future__ import annotations

import importlib
import time
from unittest.mock import patch

import pytest
from fastapi.testclient import TestClient


@pytest.fixture
def two_office_client(tmp_path, monkeypatch):
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 't.db'}")
    monkeypatch.setenv("KORKUT_SECRET", "test-secret-stable")

    import korkut.api.auth as auth_mod
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    auth_mod._secret_cache = None
    import korkut.api.app as app_mod

    importlib.reload(app_mod)

    from korkut.api.models import Matter, Office, User
    from korkut.core.tenancy import reset_current_office, set_current_office

    def seed(name: str, matters: int) -> dict:
        with store_mod.session() as s:
            office = Office(name=name)
            s.add(office)
            s.commit()
            oid = office.id
        tok = set_current_office(oid)
        try:
            with store_mod.session() as s:
                u = User(username=f"{name.lower()}-owner", password_hash="x", role="owner")
                s.add(u)
                s.commit()
                uid, epoch = u.id, u.token_epoch
                for i in range(matters):
                    s.add(Matter(title=f"{name}-matter-{i}", created_by=uid))
                s.commit()
                first_mid = s.scalars(store_mod.select(Matter)).first().id
        finally:
            reset_current_office(tok)
        cookie = auth_mod.make_session_token(uid, epoch, now=int(time.time()))
        return {"office_id": oid, "matter_id": first_mid, "cookie": cookie}

    a = seed("A", matters=1)
    b = seed("B", matters=2)
    with patch("korkut.brain.agent.answer", return_value="x"):
        yield app_mod, a, b


def _client(app_mod, cookie: str | None) -> TestClient:
    from korkut.api import auth

    c = TestClient(app_mod.app)
    if cookie is not None:
        c.cookies.set(auth.COOKIE_NAME, cookie)
    return c


def test_each_office_sees_only_its_own_matter_count(two_office_client):
    app_mod, a, b = two_office_client
    # distinct counts (A=1, B=2) prove the scope follows the cookie, not a shared/stuck context
    ra = _client(app_mod, a["cookie"]).get("/api/matters").json()
    rb = _client(app_mod, b["cookie"]).get("/api/matters").json()
    assert ra["total"] == 1
    assert rb["total"] == 2
    assert "B-matter" not in repr(ra["items"])
    assert "A-matter" not in repr(rb["items"])


def test_cross_office_matter_id_is_404(two_office_client):
    app_mod, a, b = two_office_client
    # office A's session asks for office B's matter id directly -> 404, never a cross-tenant leak
    resp = _client(app_mod, a["cookie"]).get(f"/api/matters/{b['matter_id']}")
    assert resp.status_code == 404
    # ...and B reaching for A's matter is equally refused
    resp = _client(app_mod, b["cookie"]).get(f"/api/matters/{a['matter_id']}")
    assert resp.status_code == 404


def test_no_session_cookie_is_unauthorized(two_office_client):
    app_mod, _a, _b = two_office_client
    assert _client(app_mod, None).get("/api/matters").status_code == 401
