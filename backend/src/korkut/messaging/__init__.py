"""Outbound messaging — channel-agnostic sending (Gmail now; Telegram/SMTP/Outlook later).

Korkut proposes a message; the lawyer confirms; a `Channel` sends it. Channels are a Strategy
pattern (like the LLM providers): one `Channel` interface, many implementations, registered in
`registry`. Scheduling lives one level up in the `OutboundMessage` queue, so every channel gets
"send now" and "send at <date>" for free. See docs/MESSAGING.md.
"""

from .channel import Channel, OutboundDraft, SendResult
from .registry import CHANNELS, build_channel

__all__ = ["CHANNELS", "Channel", "OutboundDraft", "SendResult", "build_channel"]
