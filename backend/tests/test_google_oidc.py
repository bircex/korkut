"""Verify the Google ID-token verifier without touching the network: a self-signed RSA token +
a fake JWK client that serves the matching public key."""

from __future__ import annotations

import time
from types import SimpleNamespace

import jwt
import pytest
from cryptography.hazmat.primitives.asymmetric import rsa

from korkut.api import google_oidc as g

CLIENT_ID = "korkut.apps.googleusercontent.com"


@pytest.fixture(scope="module")
def keypair():
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    return key, key.public_key()


@pytest.fixture
def jwk(keypair):
    _, public = keypair
    return SimpleNamespace(get_signing_key_from_jwt=lambda _t: SimpleNamespace(key=public))


def _token(keypair, **overrides):
    private, _ = keypair
    now = int(time.time())
    claims = {
        "iss": "https://accounts.google.com",
        "aud": CLIENT_ID,
        "sub": "1234567890",
        "email": "avukat@firma.com",
        "email_verified": True,
        "iat": now,
        "exp": now + 600,
    }
    claims.update(overrides)
    return jwt.encode(claims, private, algorithm="RS256")


def test_valid_token(keypair, jwk):
    ident = g.verify_id_token(_token(keypair, hd="firma.com"), CLIENT_ID, jwk_client=jwk)
    assert ident == g.GoogleIdentity(sub="1234567890", email="avukat@firma.com", hd="firma.com")


def test_unverified_email_rejected(keypair, jwk):
    with pytest.raises(g.GoogleAuthError):
        g.verify_id_token(_token(keypair, email_verified=False), CLIENT_ID, jwk_client=jwk)


def test_wrong_audience_rejected(keypair, jwk):
    with pytest.raises(g.GoogleAuthError):
        g.verify_id_token(_token(keypair, aud="someone-else"), CLIENT_ID, jwk_client=jwk)


def test_wrong_issuer_rejected(keypair, jwk):
    with pytest.raises(g.GoogleAuthError):
        g.verify_id_token(_token(keypair, iss="https://evil.example"), CLIENT_ID, jwk_client=jwk)


def test_expired_token_rejected(keypair, jwk):
    now = int(time.time())
    tok = _token(keypair, iat=now - 1000, exp=now - 400)
    with pytest.raises(g.GoogleAuthError):
        g.verify_id_token(tok, CLIENT_ID, jwk_client=jwk)


def test_config_absent_and_present(monkeypatch):
    for k in ("GOOGLE_CLIENT_ID", "GOOGLE_CLIENT_SECRET", "GOOGLE_REDIRECT_URI"):
        monkeypatch.delenv(k, raising=False)
    assert g.config() is None
    monkeypatch.setenv("GOOGLE_CLIENT_ID", "cid")
    monkeypatch.setenv("GOOGLE_CLIENT_SECRET", "secret")
    monkeypatch.setenv("GOOGLE_REDIRECT_URI", "https://app.korkutai.com/api/auth/google/callback")
    cfg = g.config()
    assert cfg is not None and cfg.client_id == "cid"
    assert cfg.token_endpoint.startswith("https://oauth2.googleapis.com")


def test_jwks_lookup_failure_becomes_google_auth_error(keypair):
    # unknown kid / JWKS fetch failure raises jwt.PyJWKClientError (NOT an InvalidTokenError);
    # it must be converted to GoogleAuthError, not escape as a 500.
    def _raise(_t):
        raise jwt.PyJWKClientError("no matching key")

    bad = SimpleNamespace(get_signing_key_from_jwt=_raise)
    with pytest.raises(g.GoogleAuthError):
        g.verify_id_token(_token(keypair), CLIENT_ID, jwk_client=bad)


def test_email_verified_string_true_accepted(keypair, jwk):
    ident = g.verify_id_token(_token(keypair, email_verified="true"), CLIENT_ID, jwk_client=jwk)
    assert ident.email == "avukat@firma.com"
