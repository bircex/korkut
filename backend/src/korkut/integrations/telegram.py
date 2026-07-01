"""Telegram connector — two-way chat over the Bot API (long-poll, pure Python).

Telegram is the cleanest channel: BotFather hands you a bot token, and the bot is a first-class
identity (no phone number, unlike WhatsApp). We pull updates with ``getUpdates`` using a stored
offset and reply with ``sendMessage`` — no webhook/public URL needed, so it runs from the same
cron/runner as everything else.

Config (`OfficeChannel.config`, kind="telegram"):
    bot_token   the BotFather token
    _cursor     internal: the next getUpdates offset (managed here, persisted by the runner)

The low-level HTTP call is injectable (`api`) so the connector is unit-testable without a network
— the same pattern the LLM providers and the Gmail channel use.
"""

from __future__ import annotations

from collections.abc import Callable
from typing import Any, Self

from korkut.integrations.base import Connector, InboundMessage, OutboundReply
from korkut.messaging import SendResult

# Short poll: we drain whatever is waiting and return immediately (the runner calls us on a
# schedule). A long timeout here would block the whole runner pass.
_GET_TIMEOUT = 0
_HTTP_TIMEOUT = 30.0

ApiCall = Callable[[str, dict[str, Any]], dict[str, Any]]


def _default_api(token: str) -> ApiCall:
    """Real Bot API caller over httpx. Returns the parsed JSON envelope ({"ok":..,"result":..})."""
    import httpx

    base = f"https://api.telegram.org/bot{token}"

    def call(method: str, payload: dict[str, Any]) -> dict[str, Any]:
        resp = httpx.post(f"{base}/{method}", json=payload, timeout=_HTTP_TIMEOUT)
        resp.raise_for_status()
        return resp.json()

    return call


def _sender_name(frm: dict[str, Any]) -> str:
    name = " ".join(p for p in (frm.get("first_name"), frm.get("last_name")) if p).strip()
    return name or (frm.get("username") or "")


class TelegramConnector(Connector):
    kind = "telegram"

    def __init__(self, *, token: str, offset: int = 0, api: ApiCall | None = None):
        self._token = token
        self._offset = offset
        self._start_offset = offset
        self._api = api or _default_api(token)

    @classmethod
    def from_config(cls, config: dict[str, Any]) -> Self:
        cursor = config.get("_cursor") or {}
        return cls(
            token=config.get("bot_token", ""),
            offset=int(cursor.get("offset") or 0),
        )

    def poll(self) -> list[InboundMessage]:
        if not self._token:
            return []
        try:
            payload: dict[str, Any] = {
                "timeout": _GET_TIMEOUT,
                "allowed_updates": ["message"],
            }
            if self._offset:
                payload["offset"] = self._offset
            data = self._api("getUpdates", payload)
        except Exception:
            return []
        if not data.get("ok"):
            return []

        out: list[InboundMessage] = []
        for upd in data.get("result", []):
            self._offset = max(self._offset, int(upd["update_id"]) + 1)
            msg = upd.get("message") or {}
            text = msg.get("text")
            frm = msg.get("from") or {}
            chat = msg.get("chat") or {}
            if not text or "id" not in frm or "id" not in chat:
                continue
            out.append(
                InboundMessage(
                    channel=self.kind,
                    external_id=str(frm["id"]),
                    thread_id=str(chat["id"]),
                    text=text,
                    sender_name=_sender_name(frm),
                )
            )
        return out

    def send(self, reply: OutboundReply) -> SendResult:
        if not self._token:
            return SendResult(ok=False, detail="Telegram bot token eksik")
        try:
            data = self._api("sendMessage", {"chat_id": reply.thread_id, "text": reply.text})
        except Exception as exc:
            return SendResult(ok=False, detail=str(exc))
        if not data.get("ok"):
            return SendResult(ok=False, detail=str(data.get("description") or "gönderilemedi"))
        return SendResult(ok=True)

    def state_patch(self) -> dict[str, Any]:
        if self._offset == self._start_offset:
            return {}
        return {"_cursor": {"offset": self._offset}}
