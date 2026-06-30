"""Channel registry — map a `kind` string to its `Channel` implementation.

Adding a new outbound integration (Telegram, generic SMTP, Outlook) is a one-line registration
here plus its `Channel` subclass; nothing else in the messaging/queue layer changes.
"""

from __future__ import annotations

from typing import Any

from .channel import Channel
from .gmail import GmailChannel

CHANNELS: dict[str, type[Channel]] = {
    GmailChannel.kind: GmailChannel,
}


def build_channel(kind: str, config: dict[str, Any]) -> Channel:
    """Construct a configured channel for `kind` from an office's stored config dict."""
    impl = CHANNELS.get(kind)
    if impl is None:
        raise ValueError(f"unknown channel kind: {kind!r}")
    return impl.from_config(config)
