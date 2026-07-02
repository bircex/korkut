"""WhatsApp connector — HTTP bridge to the Node/Baileys sidecar.

WhatsApp has no bot API the way Telegram does; clawbot (and everyone else) drives it through
WhatsApp Web via the Baileys library, which is Node-only. So Korkut keeps a small **sidecar**
process (`integrations/sidecar/`) that owns the WhatsApp Web session (QR-paired once) and exposes
a tiny local HTTP API:

    GET  /inbox?after=<cursor>   -> {"messages": [{id, from, chat, text, name, mentions, quoted}],
                                     "cursor": <id>}   # mentions/quoted drive group @mention gating
    GET  /me                     -> {"number": "<bot's own number>"}   # parallel of Telegram getMe
    POST /send  {to, text}       -> {"ok": true}

This connector is the Python half: it polls ``/inbox`` (acknowledging by advancing the cursor) and
posts replies to ``/send``. All traffic is localhost between the sidecar and Korkut; the shared
``token`` guards the sidecar so nothing else on the host can drive the WhatsApp session.

Config (`OfficeChannel.config`, kind="whatsapp"):
    base_url   sidecar URL (default http://127.0.0.1:8099)
    token      shared secret sent as Bearer auth to the sidecar
    _cursor    internal: last delivered message id (managed here, persisted by the runner)
"""

from __future__ import annotations

import contextlib
import re
from collections.abc import Callable
from typing import Any, Self

from korkut.integrations.base import Connector, InboundMessage, OutboundReply
from korkut.messaging import SendResult

# WhatsApp group chat jids end with this suffix; direct chats end with @s.whatsapp.net.
_GROUP_SUFFIX = "@g.us"

_HTTP_TIMEOUT = 30.0
_DEFAULT_BASE = "http://127.0.0.1:8099"

# (method, path, payload) -> parsed JSON. payload is querystring for GET, body for POST.
ApiCall = Callable[[str, str, dict[str, Any]], dict[str, Any]]


def _default_api(base_url: str, token: str) -> ApiCall:
    import httpx

    headers = {"Authorization": f"Bearer {token}"} if token else {}
    base = base_url.rstrip("/")

    def call(method: str, path: str, payload: dict[str, Any]) -> dict[str, Any]:
        url = f"{base}{path}"
        if method == "GET":
            resp = httpx.get(url, params=payload, headers=headers, timeout=_HTTP_TIMEOUT)
        else:
            resp = httpx.post(url, json=payload, headers=headers, timeout=_HTTP_TIMEOUT)
        resp.raise_for_status()
        return resp.json()

    return call


class WhatsAppConnector(Connector):
    kind = "whatsapp"

    def __init__(
        self,
        *,
        base_url: str = _DEFAULT_BASE,
        token: str = "",
        cursor: str = "",
        api: ApiCall | None = None,
    ):
        self._base_url = base_url or _DEFAULT_BASE
        self._token = token
        self._cursor = cursor
        self._start_cursor = cursor
        self._api = api or _default_api(self._base_url, token)
        self._self_number_cache: str | None = None

    def _self_number(self) -> str:
        """The bot's own WhatsApp number (cached), from the sidecar's ``/me``. Empty on failure.

        The parallel of Telegram's ``getMe``: needed to tell whether a group message @mentions the
        bot or replies to it. An older sidecar without ``/me`` yields "" → in that case group
        messages simply go unanswered (a safe default), while direct chats are unaffected.
        """
        if self._self_number_cache is None:
            self._self_number_cache = ""
            with contextlib.suppress(Exception):
                data = self._api("GET", "/me", {})
                self._self_number_cache = str(data.get("number") or "")
        return self._self_number_cache

    def _gate_group(self, chat: str, text: str, msg: dict[str, Any]) -> str | None:
        """Group gating: answer only when addressed; strip the @mention. Returns None to skip.

        Direct chats pass through untouched. In a group the bot acts only if it is @mentioned
        (its number in ``mentions``) or the message replies to one of its own (``quoted`` == self),
        mirroring the Telegram connector. The sidecar supplies ``mentions``/``quoted`` per message.
        """
        if not chat.endswith(_GROUP_SUFFIX):
            return text
        self_num = self._self_number()
        if not self_num:
            return None  # can't tell who was addressed → stay quiet in the group
        mentions = {str(x) for x in (msg.get("mentions") or [])}
        replied = str(msg.get("quoted") or "") == self_num
        if self_num not in mentions and not replied:
            return None
        if self_num in mentions:
            text = re.sub(rf"@{re.escape(self_num)}\b", " ", text).strip()
        return text or None

    @classmethod
    def from_config(cls, config: dict[str, Any]) -> Self:
        cursor = config.get("_cursor") or {}
        return cls(
            base_url=config.get("base_url") or _DEFAULT_BASE,
            token=config.get("token", ""),
            cursor=str(cursor.get("after") or ""),
        )

    def poll(self) -> list[InboundMessage]:
        try:
            data = self._api("GET", "/inbox", {"after": self._cursor})
        except Exception:
            return []

        out: list[InboundMessage] = []
        for msg in data.get("messages", []):
            text = msg.get("text")
            sender = msg.get("from")
            if not text or not sender:
                continue
            chat = str(msg.get("chat") or sender)
            gated = self._gate_group(chat, text, msg)
            if gated is None:
                continue  # a group message not addressed to the bot
            out.append(
                InboundMessage(
                    channel=self.kind,
                    external_id=str(sender),
                    thread_id=chat,
                    text=gated,
                    sender_name=str(msg.get("name") or ""),
                )
            )
        cursor = data.get("cursor")
        if cursor is not None:
            self._cursor = str(cursor)
        return out

    def send(self, reply: OutboundReply) -> SendResult:
        try:
            data = self._api("POST", "/send", {"to": reply.thread_id, "text": reply.text})
        except Exception as exc:
            return SendResult(ok=False, detail=str(exc))
        if not data.get("ok"):
            return SendResult(ok=False, detail=str(data.get("error") or "gönderilemedi"))
        return SendResult(ok=True)

    def state_patch(self) -> dict[str, Any]:
        if self._cursor == self._start_cursor:
            return {}
        return {"_cursor": {"after": self._cursor}}
