"""Gmail channel — send-only over SMTP with an App Password (smtp.gmail.com:587, STARTTLS).

Send-only by design: no OAuth, no Google Cloud project. Gmail records messages sent through its
SMTP server in the account's "Sent", so the lawyer keeps a copy. The office supplies its own
address + App Password (stored on `OfficeChannel.config`), so mail leaves from the office's own
account and data stays with the office.

The SMTP connection is injectable (`smtp_factory`) so the channel is unit-testable without a
network — the same pattern the LLM providers use for their HTTP client.
"""

from __future__ import annotations

import smtplib
from collections.abc import Callable
from email.message import EmailMessage
from typing import Any, Self

from .channel import Channel, OutboundDraft, SendResult

DEFAULT_HOST = "smtp.gmail.com"
DEFAULT_PORT = 587


class GmailChannel(Channel):
    kind = "email"

    def __init__(
        self,
        *,
        from_address: str,
        app_password: str,
        from_name: str = "",
        host: str = DEFAULT_HOST,
        port: int = DEFAULT_PORT,
        smtp_factory: Callable[[], smtplib.SMTP] | None = None,
    ) -> None:
        self._from = from_address
        self._password = app_password
        self._from_name = from_name
        self._host = host
        self._port = port
        # Injectable for tests; returns an open SMTP-like connection.
        self._smtp_factory = smtp_factory

    @classmethod
    def from_config(cls, config: dict[str, Any]) -> Self:
        return cls(
            from_address=config.get("from_address", ""),
            app_password=config.get("app_password", ""),
            from_name=config.get("from_name", ""),
            host=config.get("host") or DEFAULT_HOST,
            port=int(config.get("port") or DEFAULT_PORT),
        )

    def _build_message(self, draft: OutboundDraft) -> EmailMessage:
        msg = EmailMessage()
        msg["From"] = f"{self._from_name} <{self._from}>" if self._from_name else self._from
        msg["To"] = draft.to_addr
        msg["Subject"] = draft.subject
        msg.set_content(draft.body)
        return msg

    def _connect(self) -> smtplib.SMTP:
        if self._smtp_factory is not None:
            return self._smtp_factory()
        return smtplib.SMTP(self._host, self._port, timeout=30)

    def send(self, draft: OutboundDraft) -> SendResult:
        if not self._from or not self._password:
            return SendResult(
                ok=False, detail="Gmail yapılandırması eksik (adres / uygulama şifresi)"
            )
        try:
            smtp = self._connect()
            try:
                smtp.starttls()
                smtp.login(self._from, self._password)
                smtp.send_message(self._build_message(draft))
            finally:
                smtp.quit()
            return SendResult(ok=True)
        except Exception as exc:
            return SendResult(ok=False, detail=str(exc))
