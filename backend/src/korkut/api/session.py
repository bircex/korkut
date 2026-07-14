"""Backend-minted session JWT — the tamper-proof carrier of the tenant + role.

Replaces the legacy HMAC cookie: ``office_id`` and ``role`` are **signed claims**, so a client can
never switch tenant or escalate privilege by editing the cookie. Signed HS256 with the operator's
stable ``KORKUT_SECRET`` (see ``auth._secret``). Revocation stays via ``token_epoch``: the caller
checks the token's ``ep`` claim against the user's current epoch, so logout / a role-or-office
change / a compromise invalidates every live token at once. See docs/SAAS_ARCHITECTURE.md §4.
"""

from __future__ import annotations

import time
from typing import NamedTuple

import jwt

from korkut.api import auth

__all__ = [
    "SESSION_TTL",
    "SIGNUP_TTL",
    "Session",
    "mint",
    "mint_signup_ticket",
    "read",
    "read_signup_ticket",
]

SESSION_TTL = 60 * 60 * 12  # 12h absolute lifetime (matches the legacy session)
_ALG = "HS256"


class Session(NamedTuple):
    user_id: int
    office_id: int
    role: str
    epoch: int


def mint(user_id: int, office_id: int, role: str, epoch: int, *, now: int | None = None) -> str:
    """Sign a session token. ``office_id``/``role``/``epoch`` become tamper-proof claims."""
    issued = int(now if now is not None else time.time())
    claims = {
        "sub": str(user_id),
        "oid": office_id,
        "role": role,
        "ep": epoch,
        "iat": issued,
        "exp": issued + SESSION_TTL,
    }
    return jwt.encode(claims, auth._secret(), algorithm=_ALG)


def read(token: str) -> Session | None:
    """Verify signature + expiry and return the claims, or None if invalid/expired/tampered.

    The caller MUST still check the returned ``epoch`` equals the user's current ``token_epoch``.
    """
    try:
        c = jwt.decode(token, auth._secret(), algorithms=[_ALG])
        return Session(int(c["sub"]), int(c["oid"]), str(c["role"]), int(c["ep"]))
    except (jwt.InvalidTokenError, KeyError, ValueError, TypeError):
        return None


# ---- signup ticket: a Google-VERIFIED identity waiting to create a new office (self-serve) ----

SIGNUP_TTL = 15 * 60  # the "name your office" step must finish within 15 minutes


def mint_signup_ticket(sub: str, email: str, *, now: int | None = None) -> str:
    """Sign a short-lived ticket proving Google verified this ``sub``/``email`` — no account yet."""
    issued = int(now if now is not None else time.time())
    claims = {
        "typ": "signup",
        "sub": sub,
        "email": email,
        "iat": issued,
        "exp": issued + SIGNUP_TTL,
    }
    return jwt.encode(claims, auth._secret(), algorithm=_ALG)


def read_signup_ticket(token: str) -> tuple[str, str] | None:
    """Return the verified ``(sub, email)`` from a signup ticket, or None if invalid/expired."""
    try:
        c = jwt.decode(token, auth._secret(), algorithms=[_ALG])
        if c.get("typ") != "signup":
            return None
        return str(c["sub"]), str(c["email"])
    except (jwt.InvalidTokenError, KeyError, ValueError, TypeError):
        return None
