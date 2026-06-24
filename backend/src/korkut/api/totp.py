"""Time-based one-time passwords (RFC 6238 / HOTP RFC 4226) — stdlib only.

This is the security-critical core of 2FA, so it is intentionally small and is verified
against the published RFC 6238 test vectors in tests/test_totp.py. Compatible with Google
Authenticator, Authy, 1Password, etc. (SHA1, 6 digits, 30s period — the de-facto standard).

Anti-replay is the caller's job: `verify` returns the matched counter so the caller can
reject a counter that was already used (see web/app.py).
"""

from __future__ import annotations

import base64
import hmac
import secrets
import struct
import time
from urllib.parse import quote

DIGITS = 6
PERIOD = 30
_ALGO = "sha1"


def new_secret(length: int = 20) -> str:
    """A fresh base32 secret (20 random bytes = 160 bits, the RFC-recommended size)."""
    return base64.b32encode(secrets.token_bytes(length)).decode("ascii")


def _hotp(secret_b32: str, counter: int, *, digits: int = DIGITS) -> str:
    key = base64.b32decode(secret_b32, casefold=True)
    msg = struct.pack(">Q", counter)
    digest = hmac.new(key, msg, _ALGO).digest()
    offset = digest[-1] & 0x0F
    code_int = struct.unpack(">I", digest[offset : offset + 4])[0] & 0x7FFFFFFF
    return str(code_int % (10**digits)).zfill(digits)


def counter_at(timestamp: float, *, period: int = PERIOD) -> int:
    return int(timestamp // period)


def verify(
    secret_b32: str,
    code: str,
    *,
    timestamp: float | None = None,
    window: int = 1,
    digits: int = DIGITS,
    period: int = PERIOD,
) -> int | None:
    """Return the matched counter if `code` is valid within ±window steps, else None.

    Comparison is constant-time. `window=1` tolerates one period of clock skew each way.
    """
    code = (code or "").strip().replace(" ", "")
    if not code.isdigit() or len(code) != digits:
        return None
    now = time.time() if timestamp is None else timestamp
    base = counter_at(now, period=period)
    for offset in range(-window, window + 1):
        counter = base + offset
        if counter < 0:
            continue
        if hmac.compare_digest(_hotp(secret_b32, counter, digits=digits), code):
            return counter
    return None


def provisioning_uri(secret_b32: str, account: str, issuer: str) -> str:
    """otpauth:// URI for QR enrolment in an authenticator app."""
    label = f"{quote(issuer)}:{quote(account)}"
    params = (
        f"secret={secret_b32}&issuer={quote(issuer)}&algorithm=SHA1&digits={DIGITS}&period={PERIOD}"
    )
    return f"otpauth://totp/{label}?{params}"
