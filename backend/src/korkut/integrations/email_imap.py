"""E-mail connector — two-way over IMAP (inbound) + SMTP (outbound).

Mail becomes a chat channel: a lawyer e-mails Korkut from their linked address and Korkut replies
to the same thread. Inbound is an IMAP poll of unseen messages; outbound reuses the existing
send-only `GmailChannel` (SMTP/App Password), so the office needs just one mailbox.

Config (`OfficeChannel.config`, kind="email") — the SMTP keys already used by the send-only
channel, plus IMAP. IMAP credentials default to the SMTP ones (one Gmail App Password covers
both):

    from_address, from_name, app_password, host, port   (SMTP — see messaging.gmail)
    imap_host   default imap.gmail.com
    imap_port   default 993
    imap_user   default = from_address
    imap_password default = app_password

Security note: inbound mail is trusted as the *linked* sender, but the `From` header is spoofable.
Use a dedicated mailbox for this channel and treat its contents as lower-trust; identity binding
plus the office's own redaction are the safety net, but a spoofed `From` matching a linked address
is a known limitation.
"""

from __future__ import annotations

import contextlib
import email
from collections.abc import Callable
from email.message import Message as EmailMsg
from email.utils import parseaddr
from typing import Any, Self

from korkut.integrations.base import Connector, InboundMessage, OutboundReply
from korkut.messaging import OutboundDraft, SendResult
from korkut.messaging.channel import Channel
from korkut.messaging.gmail import GmailChannel

_DEFAULT_IMAP_HOST = "imap.gmail.com"
_DEFAULT_IMAP_PORT = 993
_MAX_BODY = 20_000  # cap a single inbound body so a huge mail can't blow up a model prompt


def _default_imap_factory(host: str, port: int) -> Callable[[], Any]:
    import imaplib

    return lambda: imaplib.IMAP4_SSL(host, port)


def _plain_body(msg: EmailMsg) -> str:
    """Best-effort plain-text body: first text/plain part that is not an attachment."""
    if msg.is_multipart():
        for part in msg.walk():
            if part.get_content_type() != "text/plain":
                continue
            if "attachment" in str(part.get("Content-Disposition", "")).lower():
                continue
            payload = part.get_payload(decode=True)
            if payload:
                return payload.decode(part.get_content_charset() or "utf-8", "replace")
        return ""
    payload = msg.get_payload(decode=True)
    if payload:
        return payload.decode(msg.get_content_charset() or "utf-8", "replace")
    return str(msg.get_payload() or "")


class EmailConnector(Connector):
    kind = "email"

    def __init__(
        self,
        *,
        office_id: int,
        imap_host: str,
        imap_port: int,
        imap_user: str,
        imap_password: str,
        out_channel: Channel,
        imap_factory: Callable[[], Any] | None = None,
    ):
        self._office_id = office_id
        self._imap_host = imap_host
        self._imap_port = imap_port
        self._imap_user = imap_user
        self._imap_password = imap_password
        self._out = out_channel
        self._imap_factory = imap_factory or _default_imap_factory(imap_host, imap_port)

    @classmethod
    def from_config(cls, office_id: int, config: dict[str, Any]) -> Self:
        return cls(
            office_id=office_id,
            imap_host=config.get("imap_host") or _DEFAULT_IMAP_HOST,
            imap_port=int(config.get("imap_port") or _DEFAULT_IMAP_PORT),
            imap_user=config.get("imap_user") or config.get("from_address", ""),
            imap_password=config.get("imap_password") or config.get("app_password", ""),
            out_channel=GmailChannel.from_config(config),
        )

    def poll(self) -> list[InboundMessage]:
        if not self._imap_user or not self._imap_password:
            return []
        try:
            imap = self._imap_factory()
        except Exception:
            return []
        out: list[InboundMessage] = []
        try:
            imap.login(self._imap_user, self._imap_password)
            imap.select("INBOX")
            typ, data = imap.search(None, "UNSEEN")
            if typ != "OK" or not data or not data[0]:
                return []
            for num in data[0].split():
                typ, raw = imap.fetch(num, "(RFC822)")
                if typ != "OK" or not raw or not raw[0]:
                    continue
                msg = email.message_from_bytes(raw[0][1])
                sender_name, sender_addr = parseaddr(msg.get("From", ""))
                if not sender_addr:
                    continue
                # Don't answer our own outgoing mail (avoids a reply loop with the sent copy).
                if sender_addr.lower() == self._imap_user.lower():
                    imap.store(num, "+FLAGS", "\\Seen")
                    continue
                body = _plain_body(msg).strip()[:_MAX_BODY]
                imap.store(num, "+FLAGS", "\\Seen")
                if not body:
                    continue
                addr = sender_addr.lower()
                out.append(
                    InboundMessage(
                        office_id=self._office_id,
                        channel=self.kind,
                        external_id=addr,
                        thread_id=addr,
                        text=body,
                        sender_name=sender_name,
                        subject=msg.get("Subject", "") or "",
                    )
                )
            return out
        except Exception:
            return out
        finally:
            with contextlib.suppress(Exception):
                imap.logout()

    def send(self, reply: OutboundReply) -> SendResult:
        subject = reply.subject or ""
        if subject and not subject.lower().startswith("re:"):
            subject = f"Re: {subject}"
        return self._out.send(
            OutboundDraft(to_addr=reply.thread_id, subject=subject or "Korkut", body=reply.text)
        )
