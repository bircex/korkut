"""The connector abstraction — one interface every two-way integration implements.

This is the inbound/outbound sibling of `korkut.messaging.Channel` (which is send-only). A
`Connector` knows how to do two things over one medium (Telegram, WhatsApp, e-mail):

* ``poll()`` — fetch messages that arrived since the last poll (`InboundMessage`s). The runner
  calls this on a schedule, exactly like the send-only dispatcher; no long-lived process or
  webhooks are required, which keeps deployment identical to the existing cron worker.
* ``send(reply)`` — deliver Korkut's answer back to the originating chat.

Connectors are built from a plain ``config`` dict (the credentials stored on
`OfficeChannel.config`) via ``from_config``, so the registry can construct any connector without
knowing its internals — the same contract the messaging channels use.

A connector keeps its read cursor (Telegram's update offset, an IMAP UID, the sidecar's queue
position) inside ``config['_cursor']`` and exposes any change via ``state_patch()``; the runner
persists that back onto the `OfficeChannel` row so the next poll resumes where this one stopped.
"""

from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from typing import Any, Self

from korkut.messaging import SendResult

__all__ = ["Connector", "InboundMessage", "OutboundReply", "SendResult"]


@dataclass(frozen=True)
class InboundMessage:
    """One message that arrived on a channel.

    `external_id` identifies the *sender* (used for identity resolution); `thread_id` identifies
    the *conversation* to reply into (a chat id, or the correspondent's address). For direct
    messages the two often coincide, but they differ for group chats, so both are carried.
    """

    channel: str
    external_id: str
    thread_id: str
    text: str
    sender_name: str = ""
    subject: str = ""  # e-mail subject; used to thread the reply ("Re: …")


@dataclass(frozen=True)
class OutboundReply:
    thread_id: str
    text: str
    subject: str = ""


class Connector(ABC):
    """One two-way medium. Holds the credentials + read cursor; stateless per message."""

    #: Stable identifier persisted on OfficeChannel.kind ("telegram", "whatsapp", "email").
    kind: str = ""

    @classmethod
    @abstractmethod
    def from_config(cls, config: dict[str, Any]) -> Self:
        """Build a configured connector from its stored config dict."""

    @abstractmethod
    def poll(self) -> list[InboundMessage]:
        """Return messages received since the last poll. Must not raise — return [] on failure."""

    @abstractmethod
    def send(self, reply: OutboundReply) -> SendResult:
        """Deliver one reply. Must not raise — failures come back as SendResult(ok=False)."""

    def state_patch(self) -> dict[str, Any]:
        """Cursor changes to persist onto OfficeChannel.config after a poll (default: none)."""
        return {}


@dataclass
class _Cursor:
    """Small helper connectors can use to track + diff their read position."""

    value: Any = None
    _dirty: bool = field(default=False, repr=False)

    def set(self, value: Any) -> None:
        if value != self.value:
            self.value = value
            self._dirty = True

    def patch(self, key: str) -> dict[str, Any]:
        return {key: self.value} if self._dirty else {}
