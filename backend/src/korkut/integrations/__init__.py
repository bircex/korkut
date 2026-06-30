"""Two-way messaging integrations — chat with Korkut over Telegram, WhatsApp and e-mail.

This is the inbound/outbound counterpart to `korkut.messaging` (which only *sends*). A lawyer
links a channel to their account once, then converses with Korkut from that channel; every
message runs through the same brain (`brain.agent.answer`) and the same redaction as the web app.

Layout (clawbot's gateway + pluggable-channel shape, in Korkut's Python stack):

    base.py        Connector ABC + InboundMessage / OutboundReply
    registry.py    CONNECTORS: kind -> Connector ; build_connector(kind, office_id, config)
    identity.py    link a channel sender to a korkut User (one-time code → IntegrationLink)
    gateway.py     inbound message -> resolve user -> agent.answer -> reply (the brain bridge)
    telegram.py    Telegram Bot API connector (httpx long-poll)
    whatsapp.py    WhatsApp connector — HTTP bridge to the Node/Baileys sidecar
    email_imap.py  e-mail connector (IMAP inbound poll + SMTP send, reusing the Gmail channel)
    runner.py      `python -m korkut.integrations.runner` — polls every enabled office connector
    sidecar/       the Node/Baileys WhatsApp Web service (separate process)

See docs/ARCHITECTURE.md.
"""

from korkut.integrations.base import Connector, InboundMessage, OutboundReply, SendResult
from korkut.integrations.registry import CONNECTORS, build_connector

__all__ = [
    "CONNECTORS",
    "Connector",
    "InboundMessage",
    "OutboundReply",
    "SendResult",
    "build_connector",
]
