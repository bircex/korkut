"""Authentication primitives — stdlib only (no extra dependencies).

Google OIDC is the only sign-in (see ``google_oidc.py``); the session itself is a signed JWT minted
by ``session.py``. This module keeps the shared crypto helpers those rely on:

- ``_secret()`` — the stable HMAC/JWT key from ``KORKUT_SECRET``.
- ``_sign``/``_verify`` — sign a short opaque value (the OAuth ``state`` CSRF token).
- ``hash_secret``/``verify_secret`` — PBKDF2 hashing for at-rest secrets (e.g. invite tokens).

``KORKUT_SECRET`` must be stable across restarts in production (otherwise every session and every
in-flight OAuth ``state`` breaks on restart). If unset, a process-random secret is used — local
trial only.
"""

from __future__ import annotations

import base64
import hashlib
import hmac
import os
import secrets

_ITERATIONS = 240_000
COOKIE_NAME = "korkut_session"

_secret_cache: bytes | None = None


def _secret() -> bytes:
    global _secret_cache
    if _secret_cache is None:
        env = os.getenv("KORKUT_SECRET")
        _secret_cache = env.encode() if env else secrets.token_bytes(32)
    return _secret_cache


# ---------- at-rest secret hashing (invite tokens) ----------


def hash_secret(value: str) -> str:
    salt = secrets.token_bytes(16)
    dk = hashlib.pbkdf2_hmac("sha256", value.encode(), salt, _ITERATIONS)
    return f"pbkdf2_sha256${_ITERATIONS}${salt.hex()}${dk.hex()}"


def verify_secret(value: str, stored: str) -> bool:
    try:
        algo, iters, salt_hex, hash_hex = stored.split("$")
        if algo != "pbkdf2_sha256":
            return False
        dk = hashlib.pbkdf2_hmac("sha256", value.encode(), bytes.fromhex(salt_hex), int(iters))
    except (ValueError, TypeError):
        return False
    return hmac.compare_digest(dk.hex(), hash_hex)


def token_hash(token: str) -> str:
    """Deterministic HMAC of a HIGH-ENTROPY bearer token (invite tokens), for equality lookup.

    Unlike ``hash_secret`` (salted PBKDF2, for low-entropy passwords), this is unsalted so the same
    token always hashes the same way — required to look an invite up by its token. Safe because the
    token itself is 24 random bytes; a single keyed hash is not brute-forceable.
    """
    return hmac.new(_secret(), token.encode(), hashlib.sha256).hexdigest()


# ---------- signing (OAuth `state` CSRF token) ----------


def _sign(payload: str) -> str:
    sig = hmac.new(_secret(), payload.encode(), hashlib.sha256).digest()
    return base64.urlsafe_b64encode(sig).decode().rstrip("=")


def _verify(payload: str, sig: str) -> bool:
    return hmac.compare_digest(sig, _sign(payload))
