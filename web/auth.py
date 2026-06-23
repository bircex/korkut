"""Authentication primitives — stdlib only (no extra dependencies).

- Passwords / recovery codes: PBKDF2-HMAC-SHA256 with a per-secret salt, stored as
  `pbkdf2_sha256$<iterations>$<salt_hex>$<hash_hex>`.
- Session cookie: signed value `<user_id>.<epoch>.<issued_at>.<hmac>`. The `epoch` is the
  user's `token_epoch`; bumping it server-side (logout, password change, 2FA reset, disable)
  instantly invalidates every outstanding cookie — stateless but revocable.
- Step-up tickets: short-lived signed value `<purpose>.<user_id>.<issued_at>.<hmac>` used
  between password success and 2FA completion. A ticket can NEVER authorise a request on its
  own — only the full session cookie can.

KORKUT_SECRET must be stable across restarts in production (otherwise everyone is logged out
and all tickets break on restart). If unset, a process-random secret is used — local trial only.
"""

from __future__ import annotations

import base64
import hashlib
import hmac
import os
import secrets

_ITERATIONS = 240_000
COOKIE_NAME = "korkut_session"
MAX_AGE = 60 * 60 * 12  # 12h absolute session lifetime
TICKET_MAX_AGE = 5 * 60  # step-up tickets expire fast

PURPOSE_MFA = "mfa"
PURPOSE_ENROLL = "enroll"

_secret_cache: bytes | None = None


def _secret() -> bytes:
    global _secret_cache
    if _secret_cache is None:
        env = os.getenv("KORKUT_SECRET")
        _secret_cache = env.encode() if env else secrets.token_bytes(32)
    return _secret_cache


# ---------- password / recovery-code hashing ----------

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


# Back-compat aliases (password is just a hashed secret).
hash_password = hash_secret
verify_password = verify_secret


# ---------- signing ----------

def _sign(payload: str) -> str:
    sig = hmac.new(_secret(), payload.encode(), hashlib.sha256).digest()
    return base64.urlsafe_b64encode(sig).decode().rstrip("=")


def _verify(payload: str, sig: str) -> bool:
    return hmac.compare_digest(sig, _sign(payload))


# ---------- session cookie ----------

def make_session_token(user_id: int, epoch: int, *, now: int) -> str:
    payload = f"{user_id}.{epoch}.{now}"
    return f"{payload}.{_sign(payload)}"


def read_session_token(token: str, *, now: int) -> tuple[int, int] | None:
    """Return (user_id, epoch) if the cookie is well-formed and unexpired, else None.

    The caller MUST still check that `epoch` equals the user's current `token_epoch`.
    """
    try:
        uid_s, epoch_s, issued_s, sig = token.split(".")
        payload = f"{uid_s}.{epoch_s}.{issued_s}"
    except ValueError:
        return None
    if not _verify(payload, sig):
        return None
    if now - int(issued_s) > MAX_AGE:
        return None
    return int(uid_s), int(epoch_s)


# ---------- step-up ticket (password done, 2FA pending) ----------

def make_ticket(purpose: str, user_id: int, *, now: int) -> str:
    payload = f"{purpose}.{user_id}.{now}"
    return f"{payload}.{_sign(payload)}"


def read_ticket(token: str, purpose: str, *, now: int) -> int | None:
    """Return the user id if the ticket is valid, matches `purpose`, and is unexpired."""
    try:
        p, uid_s, issued_s, sig = token.split(".")
        payload = f"{p}.{uid_s}.{issued_s}"
    except ValueError:
        return None
    if p != purpose or not _verify(payload, sig):
        return None
    if now - int(issued_s) > TICKET_MAX_AGE:
        return None
    return int(uid_s)


def new_recovery_codes(n: int = 10) -> list[str]:
    """Human-typeable single-use codes, e.g. 'a1b2-c3d4-e5f6'."""
    alphabet = "abcdefghijkmnpqrstuvwxyz23456789"  # no ambiguous chars
    codes = []
    for _ in range(n):
        raw = "".join(secrets.choice(alphabet) for _ in range(12))
        codes.append(f"{raw[0:4]}-{raw[4:8]}-{raw[8:12]}")
    return codes
