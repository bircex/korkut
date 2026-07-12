"""Google OAuth2 authorization-code flow mechanics (pure, DB-free, testable).

Builds the consent-screen URL, mints/checks the CSRF ``state``, and exchanges the returned code
for an ID token at Google's token endpoint. ID-token verification lives in ``google_oidc``; the
DB-touching sign-in (resolve office, bind the Google ``sub``, mint the session) lives in the app.
The HTTP call in ``exchange_code`` uses stdlib urllib and takes an injectable opener for tests.
"""

from __future__ import annotations

import json
import secrets
import time
import urllib.parse
import urllib.request
from collections.abc import Callable
from typing import Any

from korkut.api import auth, google_oidc

_STATE_TTL = 10 * 60  # the consent round-trip must complete within 10 minutes


def build_auth_url(cfg: google_oidc.GoogleConfig, state: str) -> str:
    """The Google consent URL to redirect the browser to."""
    params = {
        "client_id": cfg.client_id,
        "redirect_uri": cfg.redirect_uri,
        "response_type": "code",
        "scope": "openid email profile",
        "state": state,
        "access_type": "online",
        "prompt": "select_account",
    }
    return f"{cfg.auth_endpoint}?{urllib.parse.urlencode(params)}"


def make_state(*, now: int | None = None) -> str:
    """A signed, time-stamped CSRF ``state`` (nonce.issued.hmac) — self-verifying, no store."""
    issued = int(now if now is not None else time.time())
    payload = f"{secrets.token_urlsafe(16)}.{issued}"
    return f"{payload}.{auth._sign(payload)}"


def check_state(state: str | None, *, now: int) -> bool:
    """True if ``state`` is well-formed, correctly signed, and still fresh."""
    if not state:
        return False
    try:
        nonce, issued_s, sig = state.split(".")
        payload = f"{nonce}.{issued_s}"
    except ValueError:
        return False
    if not auth._verify(payload, sig):
        return False
    try:
        return 0 <= now - int(issued_s) <= _STATE_TTL
    except ValueError:
        return False


def exchange_code(
    cfg: google_oidc.GoogleConfig,
    code: str,
    *,
    opener: Callable[..., Any] = urllib.request.urlopen,
) -> str:
    """Exchange an authorization ``code`` for the Google ID token, or raise ``GoogleAuthError``.

    ``opener`` is injectable so tests exercise the flow without a network call.
    """
    body = urllib.parse.urlencode(
        {
            "code": code,
            "client_id": cfg.client_id,
            "client_secret": cfg.client_secret,
            "redirect_uri": cfg.redirect_uri,
            "grant_type": "authorization_code",
        }
    ).encode()
    req = urllib.request.Request(
        cfg.token_endpoint,
        data=body,
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )
    try:
        with opener(req, timeout=10) as resp:
            payload = json.loads(resp.read().decode())
    except Exception as exc:  # network error, non-2xx, malformed JSON
        raise google_oidc.GoogleAuthError(f"token exchange failed: {exc}") from exc
    id_token = payload.get("id_token")
    if not id_token:
        raise google_oidc.GoogleAuthError("no id_token in token response")
    return str(id_token)
