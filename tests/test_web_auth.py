"""End-to-end auth flow: two-step login, mandatory 2FA, lockout, replay, session revocation.

The brain (`agent.answer`) is patched so these tests stay offline. A fresh SQLite file per
run is used via KORKUT_DB_URL set in conftest/below.
"""

import time
from unittest.mock import patch

import pytest
from fastapi.testclient import TestClient

from web import totp


@pytest.fixture()
def client(tmp_path, monkeypatch):
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path/'t.db'}")
    monkeypatch.setenv("KORKUT_SECRET", "test-secret-stable")
    # Reset cached singletons that read those envs at import time.
    import importlib

    import web.auth as auth_mod
    import web.store as store_mod
    importlib.reload(store_mod)
    auth_mod._secret_cache = None
    import web.app as app_mod
    importlib.reload(app_mod)

    import web.adduser as adduser
    adduser.main(["avukat", "--password", "parola123", "--name", "Av. Test"])

    with patch.object(app_mod.agent, "answer", return_value="DRAFT yanıt."):
        yield TestClient(app_mod.app)


def _totp_now(secret: str) -> str:
    return totp._hotp(secret, totp.counter_at(time.time()))


def _totp_next(secret: str) -> str:
    # A code for the next period — distinct counter from enrolment, so anti-replay (which
    # consumed the enrolment counter) does not reject a genuine subsequent login in-test.
    return totp._hotp(secret, totp.counter_at(time.time()) + 1)


def test_no_register_endpoint(client):
    assert client.post("/api/register", json={}).status_code == 404


def test_password_then_enroll_then_chat(client):
    # Step 1: password → enrol step (no full session yet).
    r = client.post("/api/login", json={"username": "avukat", "password": "parola123"})
    assert r.status_code == 200 and r.json()["step"] == "enroll"
    # No full session before 2FA.
    assert client.get("/api/me").status_code == 401
    assert client.post("/api/chat", json={"text": "selam"}).status_code == 401

    # Provision a secret, confirm with a live code → session + recovery codes.
    prov = client.get("/api/2fa/provision").json()
    secret = prov["secret"]
    assert prov["otpauth_uri"].startswith("otpauth://") and prov["qr"].startswith("data:image/png")
    r = client.post("/api/2fa/enroll", json={"code": _totp_now(secret)})
    assert r.status_code == 200, r.text
    codes = r.json()["recovery_codes"]
    assert len(codes) == 10

    # Now authenticated.
    assert client.get("/api/me").json()["full_name"] == "Av. Test"
    assert "DRAFT" in client.post("/api/chat", json={"text": "soru"}).json()["reply"]


def test_second_login_requires_totp(client):
    # Enrol first.
    client.post("/api/login", json={"username": "avukat", "password": "parola123"})
    secret = client.get("/api/2fa/provision").json()["secret"]
    client.post("/api/2fa/enroll", json={"code": _totp_now(secret)})
    client.post("/api/logout")
    client.cookies.clear()

    # Re-login: password → mfa step, wrong code rejected, right code works.
    r = client.post("/api/login", json={"username": "avukat", "password": "parola123"})
    assert r.json()["step"] == "mfa"
    assert client.post("/api/2fa/verify", json={"code": "000000"}).status_code == 401
    r = client.post("/api/2fa/verify", json={"code": _totp_next(secret)})
    assert r.status_code == 200 and client.get("/api/me").status_code == 200


def test_recovery_code_single_use(client):
    client.post("/api/login", json={"username": "avukat", "password": "parola123"})
    secret = client.get("/api/2fa/provision").json()["secret"]
    codes = client.post("/api/2fa/enroll", json={"code": _totp_now(secret)}).json()["recovery_codes"]
    client.post("/api/logout"); client.cookies.clear()

    client.post("/api/login", json={"username": "avukat", "password": "parola123"})
    assert client.post("/api/2fa/verify", json={"code": codes[0]}).status_code == 200
    client.post("/api/logout"); client.cookies.clear()

    # The same recovery code cannot be reused.
    client.post("/api/login", json={"username": "avukat", "password": "parola123"})
    assert client.post("/api/2fa/verify", json={"code": codes[0]}).status_code == 401


def test_totp_replay_blocked(client):
    client.post("/api/login", json={"username": "avukat", "password": "parola123"})
    secret = client.get("/api/2fa/provision").json()["secret"]
    client.post("/api/2fa/enroll", json={"code": _totp_now(secret)})
    client.post("/api/logout"); client.cookies.clear()

    code = _totp_next(secret)
    client.post("/api/login", json={"username": "avukat", "password": "parola123"})
    assert client.post("/api/2fa/verify", json={"code": code}).status_code == 200
    client.post("/api/logout"); client.cookies.clear()

    # Same code, same period → replay rejected.
    client.post("/api/login", json={"username": "avukat", "password": "parola123"})
    assert client.post("/api/2fa/verify", json={"code": code}).status_code == 401


def test_bad_password_and_lockout(client):
    for _ in range(5):
        assert client.post("/api/login", json={"username": "avukat", "password": "yanlis"}).status_code == 401
    # 6th attempt: locked even with the correct password.
    r = client.post("/api/login", json={"username": "avukat", "password": "parola123"})
    assert r.status_code == 429


def test_unknown_user_generic_401(client):
    r = client.post("/api/login", json={"username": "yok", "password": "whatever1"})
    assert r.status_code == 401 and r.json()["detail"] == "Kullanıcı adı veya parola hatalı"


def test_logout_revokes_all_sessions(client):
    client.post("/api/login", json={"username": "avukat", "password": "parola123"})
    secret = client.get("/api/2fa/provision").json()["secret"]
    client.post("/api/2fa/enroll", json={"code": _totp_now(secret)})
    cookie = client.cookies.get("korkut_session")
    assert client.get("/api/me").status_code == 200

    client.post("/api/logout")
    # Re-presenting the old cookie must fail (epoch bumped).
    client.cookies.clear()
    client.cookies.set("korkut_session", cookie)
    assert client.get("/api/me").status_code == 401
