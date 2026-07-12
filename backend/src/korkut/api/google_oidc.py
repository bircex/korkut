"""Google OIDC — verify a Google-issued ID token, and read the OAuth client config from env.

Verification is full and strict (docs/SAAS_ARCHITECTURE.md §4/§15): RS256 signature against
Google's published keys (``jwt.PyJWKClient`` fetches the JWKS over stdlib urllib — no ``requests``
dependency), plus ``aud`` = our client id, ``iss`` = Google, unexpired, and ``email_verified`` is
true. The JWK client is injectable so tests verify a self-signed token without touching the network.

Google credentials are read from the environment and may be absent (the app runs before they are
provisioned); ``config()`` returns ``None`` then, and the auth endpoints report "not configured".
"""

from __future__ import annotations

import os
from typing import NamedTuple

import jwt

_GOOGLE_ISS = {"https://accounts.google.com", "accounts.google.com"}
_GOOGLE_CERTS = "https://www.googleapis.com/oauth2/v3/certs"
_AUTH_ENDPOINT = "https://accounts.google.com/o/oauth2/v2/auth"
_TOKEN_ENDPOINT = "https://oauth2.googleapis.com/token"

_jwk_client: jwt.PyJWKClient | None = None


class GoogleConfig(NamedTuple):
    client_id: str
    client_secret: str
    redirect_uri: str
    auth_endpoint: str = _AUTH_ENDPOINT
    token_endpoint: str = _TOKEN_ENDPOINT


class GoogleIdentity(NamedTuple):
    sub: str
    email: str
    hd: str | None  # Workspace domain, if any


class GoogleAuthError(Exception):
    """The ID token is invalid, expired, or its email is unverified."""


def config() -> GoogleConfig | None:
    """OAuth client config from env, or None if not provisioned yet."""
    cid = os.getenv("GOOGLE_CLIENT_ID")
    secret = os.getenv("GOOGLE_CLIENT_SECRET")
    redirect = os.getenv("GOOGLE_REDIRECT_URI")
    if not (cid and secret and redirect):
        return None
    return GoogleConfig(client_id=cid, client_secret=secret, redirect_uri=redirect)


def _client() -> jwt.PyJWKClient:
    global _jwk_client
    if _jwk_client is None:
        _jwk_client = jwt.PyJWKClient(_GOOGLE_CERTS)
    return _jwk_client


def verify_id_token(
    id_token: str, client_id: str, *, jwk_client: jwt.PyJWKClient | None = None
) -> GoogleIdentity:
    """Verify a Google ID token and return the identity, or raise ``GoogleAuthError``.

    ``jwk_client`` is injectable for tests (a client that serves a self-signed key).
    """
    client = jwk_client or _client()
    try:
        key = client.get_signing_key_from_jwt(id_token).key
        claims = jwt.decode(
            id_token,
            key,
            algorithms=["RS256"],
            audience=client_id,
            options={"require": ["aud", "iss", "exp", "sub"]},
        )
    except jwt.InvalidTokenError as exc:
        raise GoogleAuthError(f"invalid Google ID token: {exc}") from exc
    if claims.get("iss") not in _GOOGLE_ISS:
        raise GoogleAuthError("unexpected issuer")
    if claims.get("email_verified") is not True:
        raise GoogleAuthError("email not verified")
    email = claims.get("email")
    if not email:
        raise GoogleAuthError("no email in token")
    return GoogleIdentity(sub=str(claims["sub"]), email=str(email), hd=claims.get("hd"))
