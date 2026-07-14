"""Tests for the messaging channel layer (Gmail SMTP mocked with a fake transport)."""

import pytest
from sqlalchemy.orm import Session

from korkut.api.models import OutboundMessage
from korkut.core.db import init_db, make_engine
from korkut.messaging import OutboundDraft, build_channel
from korkut.messaging.gmail import GmailChannel


class FakeSMTP:
    def __init__(self) -> None:
        self.calls: list = []
        self.sent: list = []

    def starttls(self) -> None:
        self.calls.append("starttls")

    def login(self, user: str, password: str) -> None:
        self.calls.append(("login", user, password))

    def send_message(self, msg) -> None:
        self.sent.append(msg)

    def quit(self) -> None:
        self.calls.append("quit")


def test_gmail_send_builds_and_sends():
    fake = FakeSMTP()
    ch = GmailChannel(
        from_address="ofis@gmail.com",
        app_password="app-pw",
        from_name="Örnek Hukuk",
        smtp_factory=lambda: fake,
    )
    res = ch.send(OutboundDraft(to_addr="muvekkil@example.com", subject="Konu", body="Merhaba"))
    assert res.ok
    assert "starttls" in fake.calls
    assert ("login", "ofis@gmail.com", "app-pw") in fake.calls
    assert "quit" in fake.calls
    msg = fake.sent[0]
    assert msg["To"] == "muvekkil@example.com"
    assert msg["Subject"] == "Konu"
    assert msg["From"] == "Örnek Hukuk <ofis@gmail.com>"
    assert "Merhaba" in msg.get_content()


def test_gmail_missing_config_is_not_ok():
    ch = GmailChannel(from_address="", app_password="")
    res = ch.send(OutboundDraft("x@y.com", "k", "b"))
    assert not res.ok and res.detail


def test_gmail_smtp_error_is_captured():
    def boom() -> FakeSMTP:
        raise OSError("connection refused")

    ch = GmailChannel(from_address="a@gmail.com", app_password="p", smtp_factory=boom)
    res = ch.send(OutboundDraft("x@y.com", "k", "b"))
    assert not res.ok
    assert "connection refused" in res.detail


def test_build_channel_email_and_unknown():
    ch = build_channel("email", {"from_address": "a@gmail.com", "app_password": "p"})
    assert isinstance(ch, GmailChannel) and ch.kind == "email"
    with pytest.raises(ValueError):
        build_channel("sms", {})


def test_outbound_message_defaults():
    engine = make_engine("sqlite+pysqlite:///:memory:")
    init_db(engine)
    with Session(engine) as s:
        msg = OutboundMessage(kind="email", to_addr="b@y.com", subject="S", body="B")
        s.add(msg)
        s.commit()
        assert msg.attempts == 0 and msg.send_at is None
