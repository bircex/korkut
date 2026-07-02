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

    #: Whether this channel can show live progress while Korkut works (typing + a status line).
    #: The runner only wires progress reporting for connectors that opt in.
    supports_progress: bool = False

    # ----- optional live progress (default no-ops; a channel like Telegram overrides them) -----
    # These let the runner surface "Korkut is working" the way a chat app would: a typing hint
    # plus one status line ("🔍 Yargıtay'da arıyorum…") edited in place as steps change and removed
    # just before the real answer is sent, so the thread stays clean.

    def typing(self, thread_id: str) -> None:  # noqa: B027  (optional hook; default no-op)
        """Signal that Korkut is working (e.g. a chat 'typing' action). Must not raise."""

    def post_status(self, thread_id: str, text: str) -> str | None:
        """Post a status line and return a handle to update it later (or None). Must not raise."""
        return None

    def edit_status(self, thread_id: str, handle: str, text: str) -> None:  # noqa: B027
        """Update a previously posted status line in place. Must not raise."""

    def clear_status(self, thread_id: str, handle: str) -> None:  # noqa: B027
        """Remove a status line (before sending the real answer). Must not raise."""

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
