"""Connector registry — map a `kind` string to its `Connector` implementation.

Adding a new two-way integration is a one-line registration here plus its `Connector` subclass;
the gateway, runner and API never change. Mirrors `korkut.messaging.registry`.
"""

from __future__ import annotations

from typing import Any

from korkut.integrations.base import Connector
from korkut.integrations.email_imap import EmailConnector
from korkut.integrations.telegram import TelegramConnector
from korkut.integrations.whatsapp import WhatsAppConnector

CONNECTORS: dict[str, type[Connector]] = {
    TelegramConnector.kind: TelegramConnector,
    WhatsAppConnector.kind: WhatsAppConnector,
    EmailConnector.kind: EmailConnector,
}


def build_connector(kind: str, office_id: int, config: dict[str, Any]) -> Connector:
    """Construct a configured connector for `kind` from one office's stored config dict."""
    impl = CONNECTORS.get(kind)
    if impl is None:
        raise ValueError(f"unknown connector kind: {kind!r}")
    return impl.from_config(office_id, config)
