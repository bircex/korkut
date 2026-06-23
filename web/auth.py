"""Authentication primitives — stdlib only (no extra dependencies).

- Passwords: PBKDF2-HMAC-SHA256 with a per-password salt, stored as
  `pbkdf2_sha256$<iterations>$<salt_hex>$<hash_hex>`.
- Sessions: a signed cookie value `<user_id>.<issued_at>.<hmac>` verified with KORKUT_SECRET.
  No server-side session store; the signature is the only trust anchor.

KORKUT_SECRET must be stable across restarts in production (otherwise everyone is logged out
on restart). If unset, a process-random secret is used — fine for a local trial only.
"""

from __future__ import annotations

import base64
import hashlib
import hmac
import os
import secrets

_ITERATIONS = 240_000
_COOKIE_NAME = "korkut_session"
_MAX_AGE = 60 * 60 * 24 * 14  # 14 days

_secret_cache: bytes | None = None


def _secret() -> bytes:
    global _secret_cache
    if _secret_cache is None:
        env = os.getenv("KORKUT_SECRET")
        _secret_cache = env.encode() if env else secrets.token_bytes(32)
    return _secret_cache


def hash_password(password: str) -> str:
    salt = secrets.token_bytes(16)
    dk = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, _ITERATIONS)
    return f"pbkdf2_sha256${_ITERATIONS}${salt.hex()}${dk.hex()}"


def verify_password(password: str, stored: str) -> bool:
    try:
        algo, iters, salt_hex, hash_hex = stored.split("$")
        if algo != "pbkdf2_sha256":
            return False
        dk = hashlib.pbkdf2_hmac("sha256", password.encode(), bytes.fromhex(salt_hex), int(iters))
    except (ValueError, TypeError):
        return False
    return hmac.compare_digest(dk.hex(), hash_hex)


def _sign(payload: str) -> str:
    sig = hmac.new(_secret(), payload.encode(), hashlib.sha256).digest()
    return base64.urlsafe_b64encode(sig).decode().rstrip("=")


def make_session_token(user_id: int, *, now: int) -> str:
    payload = f"{user_id}.{now}"
    return f"{payload}.{_sign(payload)}"


def read_session_token(token: str, *, now: int) -> int | None:
    """Return the user id if the token is valid and unexpired, else None."""
    try:
        uid_s, issued_s, sig = token.split(".")
        payload = f"{uid_s}.{issued_s}"
    except ValueError:
        return None
    if not hmac.compare_digest(sig, _sign(payload)):
        return None
    if now - int(issued_s) > _MAX_AGE:
        return None
    return int(uid_s)


COOKIE_NAME = _COOKIE_NAME
MAX_AGE = _MAX_AGE
