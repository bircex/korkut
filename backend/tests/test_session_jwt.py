"""The backend-minted session JWT: signed office_id/role/epoch, revocation, tamper-proofing."""

from __future__ import annotations

import time

import jwt
import pytest

from korkut.api import auth, session


@pytest.fixture(autouse=True)
def _stable_secret(monkeypatch):
    monkeypatch.setenv("KORKUT_SECRET", "test-secret-stable-0123456789abcdef")
    auth._secret_cache = None
    yield
    auth._secret_cache = None


def test_mint_read_roundtrip():
    tok = session.mint(7, 3, "owner", 5)
    s = session.read(tok)
    assert s == session.Session(user_id=7, office_id=3, role="owner", epoch=5)


def test_tampering_office_id_is_rejected():
    # decode payload, bump oid, re-encode with a WRONG key -> signature check must fail
    tok = session.mint(7, 3, "member", 1)
    claims = jwt.decode(tok, auth._secret(), algorithms=["HS256"])
    claims["oid"] = 999
    forged = jwt.encode(claims, b"attacker-key-0123456789abcdef0123", algorithm="HS256")
    assert session.read(forged) is None


def test_expired_token_is_rejected():
    past = int(time.time()) - session.SESSION_TTL - 10
    tok = session.mint(7, 3, "member", 1, now=past)
    assert session.read(tok) is None


def test_wrong_secret_is_rejected(monkeypatch):
    tok = session.mint(7, 3, "member", 1)
    monkeypatch.setenv("KORKUT_SECRET", "a-different-secret-0123456789abcdef")
    auth._secret_cache = None
    assert session.read(tok) is None


def test_garbage_is_rejected():
    assert session.read("not-a-jwt") is None
    assert session.read("") is None


def test_alg_none_token_rejected():
    # an unsigned 'none'-algorithm token must never be accepted (algorithm-confusion attack)
    forged = jwt.encode(
        {"sub": "7", "oid": 3, "role": "owner", "ep": 1, "exp": 9999999999}, None, algorithm="none"
    )
    assert session.read(forged) is None


def test_missing_claims_rejected():
    # a validly-signed token that omits the office/epoch claims is not a usable session
    tok = jwt.encode({"sub": "7", "iat": 0, "exp": 9999999999}, auth._secret(), algorithm="HS256")
    assert session.read(tok) is None
