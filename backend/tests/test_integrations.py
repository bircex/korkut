"""Tests for the two-way integrations layer.

Connectors are exercised with injected fakes (no network), exactly like the messaging tests.
Identity + gateway run against a fresh SQLite file with the brain patched, like the auth tests.
"""

from __future__ import annotations

import importlib
from email.message import EmailMessage
from unittest.mock import patch

import pytest

from korkut.integrations.base import InboundMessage, OutboundReply
from korkut.integrations.email_imap import EmailConnector
from korkut.integrations.telegram import TelegramConnector
from korkut.integrations.whatsapp import WhatsAppConnector
from korkut.messaging import SendResult

# ----------------------------- Telegram -----------------------------


class FakeTelegramApi:
    def __init__(self, updates):
        self._updates = updates
        self.sent: list = []

    def __call__(self, method, payload):
        if method == "getUpdates":
            return {"ok": True, "result": self._updates}
        if method == "sendMessage":
            self.sent.append(payload)
            return {"ok": True}
        raise AssertionError(method)


def _tg_update(uid, chat_id, from_id, text, first="Av", last="Test"):
    return {
        "update_id": uid,
        "message": {
            "message_id": uid,
            "from": {"id": from_id, "first_name": first, "last_name": last},
            "chat": {"id": chat_id, "type": "private"},
            "text": text,
        },
    }


def test_telegram_poll_parses_and_advances_offset():
    api = FakeTelegramApi([_tg_update(10, 555, 555, "merhaba"), _tg_update(11, 555, 555, "selam")])
    c = TelegramConnector(token="t", offset=0, api=api)
    msgs = c.poll()
    assert [m.text for m in msgs] == ["merhaba", "selam"]
    assert msgs[0].channel == "telegram"
    assert msgs[0].external_id == "555" and msgs[0].thread_id == "555"
    assert msgs[0].sender_name == "Av Test"
    # offset advances past the last update so the next poll won't replay
    assert c.state_patch() == {"_cursor": {"offset": 12}}


def test_telegram_skips_non_text_and_send():
    api = FakeTelegramApi([{"update_id": 5, "message": {"from": {"id": 1}, "chat": {"id": 1}}}])
    c = TelegramConnector(token="t", api=api)
    assert c.poll() == []
    res = c.send(OutboundReply(thread_id="555", text="cevap"))
    assert res.ok
    assert api.sent == [{"chat_id": "555", "text": "cevap"}]


def test_telegram_no_token_is_quiet():
    c = TelegramConnector(token="", api=FakeTelegramApi([]))
    assert c.poll() == []
    assert not c.send(OutboundReply(thread_id="1", text="x")).ok


# ----------------------------- WhatsApp -----------------------------


class FakeWhatsAppApi:
    def __init__(self, inbox):
        self._inbox = inbox
        self.sent: list = []

    def __call__(self, method, path, payload):
        if method == "GET" and path == "/inbox":
            return self._inbox
        if method == "POST" and path == "/send":
            self.sent.append(payload)
            return {"ok": True}
        raise AssertionError((method, path))


def test_whatsapp_poll_and_cursor():
    api = FakeWhatsAppApi(
        {
            "messages": [
                {
                    "id": 4,
                    "from": "905001@s.whatsapp.net",
                    "chat": "905001@s.whatsapp.net",
                    "text": "selam",
                    "name": "Müvekkil",
                },
            ],
            "cursor": 4,
        }
    )
    c = WhatsAppConnector(token="secret", cursor="", api=api)
    msgs = c.poll()
    assert len(msgs) == 1
    assert msgs[0].channel == "whatsapp"
    assert msgs[0].external_id == "905001@s.whatsapp.net"
    assert msgs[0].text == "selam"
    assert c.state_patch() == {"_cursor": {"after": "4"}}


def test_whatsapp_send():
    api = FakeWhatsAppApi({"messages": [], "cursor": 0})
    c = WhatsAppConnector(token="secret", api=api)
    assert c.poll() == []
    res = c.send(OutboundReply(thread_id="905001@s.whatsapp.net", text="cevap"))
    assert res.ok
    assert api.sent == [{"to": "905001@s.whatsapp.net", "text": "cevap"}]


# ----------------------------- E-mail -----------------------------


class FakeChannel:
    """Stand-in for the outbound GmailChannel; records the draft it was asked to send."""

    kind = "email"

    def __init__(self):
        self.sent: list = []

    def send(self, draft):
        self.sent.append(draft)
        return SendResult(ok=True)


class FakeIMAP:
    def __init__(self, raw_messages):
        self._raw = raw_messages
        self.flags: list = []
        self.logged_out = False

    def login(self, user, password):
        self.user = user

    def select(self, mailbox):
        pass

    def search(self, charset, *criteria):
        ids = b" ".join(str(i + 1).encode() for i in range(len(self._raw)))
        return ("OK", [ids])

    def fetch(self, num, spec):
        return ("OK", [(b"hdr", self._raw[int(num) - 1])])

    def store(self, num, flag, value):
        self.flags.append((num, value))

    def logout(self):
        self.logged_out = True


def _raw_email(frm, subject, body):
    m = EmailMessage()
    m["From"] = frm
    m["Subject"] = subject
    m.set_content(body)
    return m.as_bytes()


def test_email_poll_parses_marks_seen_and_skips_self():
    imap = FakeIMAP(
        [
            _raw_email("Av. Test <muvekkil@example.com>", "Soru", "Merhaba korkut"),
            _raw_email("ofis@gmail.com", "kendi", "kendi mailim"),  # our own → skipped
        ]
    )
    c = EmailConnector(
        imap_host="imap",
        imap_port=993,
        imap_user="ofis@gmail.com",
        imap_password="pw",
        out_channel=FakeChannel(),
        imap_factory=lambda: imap,
    )
    msgs = c.poll()
    assert len(msgs) == 1
    m = msgs[0]
    assert m.channel == "email"
    assert m.external_id == "muvekkil@example.com"
    assert m.thread_id == "muvekkil@example.com"
    assert m.subject == "Soru"
    assert "Merhaba korkut" in m.text
    # both messages flagged \Seen (the skipped one too) and the connection closed
    assert len(imap.flags) == 2 and imap.logged_out


def test_email_send_threads_reply():
    ch = FakeChannel()
    c = EmailConnector(
        imap_host="imap",
        imap_port=993,
        imap_user="ofis@gmail.com",
        imap_password="pw",
        out_channel=ch,
        imap_factory=lambda: FakeIMAP([]),
    )
    res = c.send(OutboundReply(thread_id="muvekkil@example.com", text="cevap", subject="Soru"))
    assert res.ok
    draft = ch.sent[0]
    assert draft.to_addr == "muvekkil@example.com"
    assert draft.subject == "Re: Soru"
    assert draft.body == "cevap"


# ----------------------------- identity + gateway -----------------------------


@pytest.fixture
def store_db(tmp_path, monkeypatch):
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 'it.db'}")
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    from korkut.api.models import Office, User

    with store_mod.session() as s:
        office = Office(name="Test Hukuk")
        s.add(office)
        s.flush()
        user = User(
            role="owner",
            username="av",
            full_name="Av. Test",
            password_hash="x",
            is_active=True,
        )
        s.add(user)
        s.commit()
        user_id = user.id
    return store_mod, user_id


def _inbound(text, external_id="999", thread_id="999"):
    return InboundMessage(
        channel="telegram",
        external_id=external_id,
        thread_id=thread_id,
        text=text,
    )


def test_unlinked_sender_gets_link_prompt(store_db):
    from korkut.integrations import gateway

    reply = gateway.handle(_inbound("merhaba"))
    assert "bağlanma kodu" in reply.lower() or "bağlı değil" in reply.lower()


def test_link_code_binds_then_chats(store_db):
    store_mod, user_id = store_db
    from korkut.integrations import gateway, identity

    with store_mod.session() as s:
        code = identity.generate_code(s, user_id, "telegram")

    # Sending the code from the channel binds the sender.
    welcome = gateway.handle(_inbound(f"BAGLAN {code}"))
    assert "bağlandınız" in welcome.lower()

    # Now resolves to the user, and a real message runs through the brain + persists.
    with patch.object(gateway.resilience, "guarded_answer", return_value="Korkut cevabı.") as ans:
        reply = gateway.handle(_inbound("bir dava sorum var"))
    assert reply == "Korkut cevabı."
    assert ans.called

    # A conversation + thread were created and both turns stored.
    from korkut.api.models import Conversation, IntegrationThread, Message

    with store_mod.session() as s:
        threads = s.query(IntegrationThread).all()
        assert len(threads) == 1 and threads[0].channel == "telegram"
        conv = s.get(Conversation, threads[0].conversation_id)
        msgs = s.query(Message).filter(Message.conversation_id == conv.id).all()
        assert [m.role for m in msgs] == ["user", "assistant"]
        assert msgs[1].content == "Korkut cevabı."

    # A second message reuses the same conversation (no duplicate thread).
    with patch.object(gateway.resilience, "guarded_answer", return_value="ikinci"):
        gateway.handle(_inbound("ikinci mesaj"))
    with store_mod.session() as s:
        assert s.query(IntegrationThread).count() == 1


def test_model_failure_replies_to_channel(store_db):
    """A model failure must reach the sender (not silence) and not be stored as an answer."""
    store_mod, user_id = store_db
    from korkut.brain import resilience
    from korkut.integrations import gateway, identity

    with store_mod.session() as s:
        code = identity.generate_code(s, user_id, "telegram")
    gateway.handle(_inbound(f"BAGLAN {code}"))  # bind first

    err = resilience.ModelError("Yapay zeka modeline ulaşılamadı.")
    with patch.object(gateway.resilience, "guarded_answer", side_effect=err):
        reply = gateway.handle(_inbound("bir dava sorum var"))
    assert reply == "Yapay zeka modeline ulaşılamadı."

    # The user turn is kept, but the failure note is NOT persisted as an assistant answer.
    from korkut.api.models import Conversation, IntegrationThread, Message

    with store_mod.session() as s:
        conv = s.get(Conversation, s.query(IntegrationThread).one().conversation_id)
        roles = [m.role for m in s.query(Message).filter(Message.conversation_id == conv.id)]
        assert roles == ["user"]


def test_expired_or_wrong_code_does_not_bind(store_db):
    from korkut.integrations import gateway

    # A made-up code never binds.
    reply = gateway.handle(_inbound("BAGLAN ZZZZZZ"))
    assert "bağlandınız" not in reply.lower()
