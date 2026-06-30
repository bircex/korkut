"""The channel abstraction — one interface every outbound integration implements.

A `Channel` knows how to deliver a single `OutboundDraft` (recipient + subject + body) over one
medium (email, Telegram, …) and reports a `SendResult`. Implementations are built from a plain
``config`` dict (the per-office credentials stored on `OfficeChannel.config`) via `from_config`,
so the registry can construct any channel without knowing its internals.
"""

from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Any, Self


@dataclass(frozen=True)
class OutboundDraft:
    """A single message to deliver. `to_addr` is medium-specific (email address, chat id, …)."""

    to_addr: str
    subject: str
    body: str


@dataclass(frozen=True)
class SendResult:
    ok: bool
    detail: str = ""


class Channel(ABC):
    """One outbound medium. Stateless w.r.t. the message; holds the office's credentials."""

    #: Stable identifier persisted on OfficeChannel.kind / OutboundMessage.kind ("email", …).
    kind: str = ""

    @classmethod
    @abstractmethod
    def from_config(cls, config: dict[str, Any]) -> Self:
        """Build a configured channel from an office's stored config dict."""

    @abstractmethod
    def send(self, draft: OutboundDraft) -> SendResult:
        """Deliver one message. Must not raise — failures come back as SendResult(ok=False)."""
