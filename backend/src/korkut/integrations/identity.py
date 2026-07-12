"""Identity binding — map a channel sender to a korkut `User`.

The whole security model of Korkut is per-user (Google OIDC sign-in, office scoping, per-author
audit). A Telegram/WhatsApp/e-mail sender carries none of that, so before the
gateway will act on a message it must know *which lawyer* sent it. That mapping is an
`IntegrationLink`:

1. **Generate** — in the web UI a lawyer asks to link a channel; `generate_code` writes a
   ``pending`` link (a short one-time code, no sender id yet) and returns the code.
2. **Bind** — the lawyer sends that code FROM the channel; `try_bind` finds the pending link by
   code, captures the sender's ``external_id`` and flips it to ``active``.
3. **Resolve** — every later inbound is authenticated by `resolve` (active link lookup).

Codes are short and one-time; a pending link expires (`_CODE_TTL`) so a leaked code is useless
after a few minutes.
"""

from __future__ import annotations

import re
import secrets
from datetime import UTC, datetime, timedelta

from sqlalchemy import select
from sqlalchemy.orm import Session

from korkut.api.models import IntegrationLink, User

# Unambiguous alphabet (no 0/O/1/I) — codes are read off a screen and typed on a phone.
_ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"
_CODE_LEN = 6
_CODE_TTL = timedelta(minutes=15)

# A binding message is just the code, optionally after a keyword: "BAGLAN ABC123" / "/link ABC123".
_BIND_RE = re.compile(rf"(?:^|\b)(?:baglan|bağlan|link|kod)?\s*([A-Z2-9]{{{_CODE_LEN}}})\b", re.I)


def _utcnow() -> datetime:
    return datetime.now(UTC).replace(tzinfo=None)


def _gen_code() -> str:
    return "".join(secrets.choice(_ALPHABET) for _ in range(_CODE_LEN))


def generate_code(s: Session, user_id: int, channel: str) -> str:
    """Create (or refresh) a pending link for this user+channel and return its one-time code.

    Re-issuing replaces any earlier pending code for the same user+channel, so a lawyer who lost
    the first code just generates another; existing *active* links are untouched.
    """
    pending = s.scalar(
        select(IntegrationLink).where(
            IntegrationLink.user_id == user_id,
            IntegrationLink.channel == channel,
            IntegrationLink.status == "pending",
        )
    )
    code = _gen_code()
    if pending is None:
        pending = IntegrationLink(user_id=user_id, channel=channel, status="pending")
        s.add(pending)
    pending.code = code
    pending.created_at = _utcnow()
    s.commit()
    return code


def resolve(s: Session, channel: str, external_id: str) -> User | None:
    """Return the `User` an active link points to for (channel, external_id), or None."""
    if not external_id:
        return None
    link = s.scalar(
        select(IntegrationLink).where(
            IntegrationLink.channel == channel,
            IntegrationLink.external_id == external_id,
            IntegrationLink.status == "active",
        )
    )
    if link is None:
        return None
    return s.get(User, link.user_id)


def try_bind(s: Session, channel: str, external_id: str, text: str) -> User | None:
    """If `text` carries a valid, unexpired pending code for this channel, activate the link to
    `external_id` and return the bound `User`. Returns None when there is nothing to bind.
    """
    m = _BIND_RE.search(text or "")
    if not m:
        return None
    code = m.group(1).upper()
    link = s.scalar(
        select(IntegrationLink).where(
            IntegrationLink.channel == channel,
            IntegrationLink.status == "pending",
            IntegrationLink.code == code,
        )
    )
    if link is None or link.created_at is None:
        return None
    if _utcnow() - link.created_at > _CODE_TTL:
        return None  # expired; the lawyer must generate a fresh code
    # Drop any prior active link for this exact sender (re-link to a new account).
    prior = s.scalar(
        select(IntegrationLink).where(
            IntegrationLink.channel == channel,
            IntegrationLink.external_id == external_id,
            IntegrationLink.status == "active",
        )
    )
    if prior is not None and prior.id != link.id:
        s.delete(prior)
    link.external_id = external_id
    link.code = None
    link.status = "active"
    link.linked_at = _utcnow()
    s.commit()
    return s.get(User, link.user_id)
