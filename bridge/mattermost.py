"""Mattermost bridge — single Korkut bot identity, listens on channels (@Korkut) and DMs.

Connects to a self-hosted Mattermost via the bot token, listens over the WebSocket, applies
the trigger rules (any DM, or @Korkut mention / thread reply in a channel), runs the Korkut
agent, and posts the reply back as Korkut. See ARCHITECTURE.md §2-§3.

Env:
  MATTERMOST_URL       e.g. http://localhost:8065   (default)
  MATTERMOST_TOKEN     the Korkut bot's access token (required)
  MATTERMOST_BOT_NAME  the bot username to detect @mentions (default: korkut)
  ANTHROPIC_API_KEY    used by the agent's cloud model
"""

from __future__ import annotations

import json
import logging
import os
from urllib.parse import urlparse

from korkut.agent import answer

logger = logging.getLogger("korkut.bridge")


def _driver():
    from mattermostdriver import Driver

    url = os.environ.get("MATTERMOST_URL", "http://localhost:8065")
    parsed = urlparse(url)
    return Driver(
        {
            "url": parsed.hostname or "localhost",
            "scheme": parsed.scheme or "http",
            "port": parsed.port or (443 if parsed.scheme == "https" else 8065),
            "token": os.environ["MATTERMOST_TOKEN"],
            "basepath": "/api/v4",
            "verify": parsed.scheme == "https",
        }
    )


class Bridge:
    def __init__(self) -> None:
        self.driver = _driver()
        self.bot_name = os.environ.get("MATTERMOST_BOT_NAME", "korkut").lower()
        self.bot_id: str | None = None

    def run(self) -> None:
        me = self.driver.login()
        self.bot_id = me["id"]
        logger.info("Korkut connected as @%s (%s)", me.get("username"), self.bot_id)
        self.driver.init_websocket(self._on_event)

    async def _on_event(self, raw) -> None:
        try:
            event = json.loads(raw) if isinstance(raw, str) else raw
        except (TypeError, ValueError):
            return
        if event.get("event") != "posted":
            return
        data = event.get("data", {})
        try:
            post = json.loads(data.get("post", "{}"))
        except (TypeError, ValueError):
            return

        if post.get("user_id") == self.bot_id:  # loop protection: ignore own messages
            return

        message = post.get("message", "")
        is_dm = data.get("channel_type") == "D"
        mentioned = f"@{self.bot_name}" in message.lower()
        if not (is_dm or mentioned):
            return  # channel message not addressed to Korkut → ignore

        text = message.replace(f"@{self.bot_name}", "").replace(f"@{self.bot_name.title()}", "").strip()
        logger.info("→ %s", text[:80])
        try:
            reply = answer(text)
        except Exception as exc:  # noqa: BLE001 — never crash the listener
            logger.exception("agent error")
            reply = f"Şu an yanıt veremedim ({type(exc).__name__}). Tekrar dener misiniz?"

        self.driver.posts.create_post(
            {
                "channel_id": post["channel_id"],
                "message": reply,
                "root_id": post.get("root_id") or post["id"],
            }
        )
