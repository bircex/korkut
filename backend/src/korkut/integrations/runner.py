"""Integration runner — poll every enabled connector and answer inbound messages.

    python -m korkut.integrations.runner

One pass: for each enabled `OfficeChannel` whose kind is a two-way connector, build the connector,
poll for new messages, run each through the `gateway` (resolve → answer), and send the reply back.
Then persist the connector's read cursor onto the row so the next pass resumes cleanly.

This is the inbound twin of `korkut.api.dispatcher` (which flushes *outbound* scheduled sends).
Run it on the same schedule — the compose `integrations` service loops it every few seconds; a
non-Docker host can cron it (see deploy/cron/korkut.crontab). Each connector is isolated in a
try/except so one channel's bad token can't stall the others.
"""

from __future__ import annotations

import time

from sqlalchemy import select

from korkut.api.models import OfficeChannel
from korkut.api.store import session
from korkut.brain import activity
from korkut.core.env import load_dotenv
from korkut.integrations import build_connector
from korkut.integrations.base import Connector, OutboundReply
from korkut.integrations.gateway import handle
from korkut.integrations.registry import CONNECTORS

# Refresh the channel "typing" hint at most this often (Telegram's action lasts ~5s).
_TYPING_REFRESH_SECONDS = 4.0


class _Progress:
    """Turns the brain's raw events into a channel's live-progress affordance (typing + status).

    One status line is posted on the first tool step and edited in place as steps change; token
    events only keep the typing hint alive (chat channels don't render partial text). `finish`
    removes the status line just before the real answer is sent, leaving a clean thread.
    """

    def __init__(self, connector: Connector, thread_id: str) -> None:
        self._c = connector
        self._t = thread_id
        self._handle: str | None = None
        self._last_line: str | None = None
        self._last_typing = 0.0

    def _keepalive(self) -> None:
        now = time.monotonic()
        if now - self._last_typing >= _TYPING_REFRESH_SECONDS:
            self._c.typing(self._t)
            self._last_typing = now

    def on_event(self, event: dict) -> None:
        self._keepalive()
        if event.get("type") != "tool":
            return
        line = f"🔍 {activity.tool_label(event.get('name', ''), event.get('args'))}"
        if line == self._last_line:
            return
        self._last_line = line
        if self._handle is None:
            self._handle = self._c.post_status(self._t, line)
        else:
            self._c.edit_status(self._t, self._handle, line)

    def finish(self) -> None:
        if self._handle is not None:
            self._c.clear_status(self._t, self._handle)
            self._handle = None


def _enabled_channels() -> list[tuple[int, str, dict]]:
    """Snapshot (row_id, kind, config) for every enabled two-way channel."""
    with session() as s:
        rows = s.scalars(
            select(OfficeChannel).where(
                OfficeChannel.enabled.is_(True),
                OfficeChannel.kind.in_(tuple(CONNECTORS)),
            )
        ).all()
        return [(r.id, r.kind, dict(r.config or {})) for r in rows]


def _persist_cursor(row_id: int, patch: dict) -> None:
    if not patch:
        return
    with session() as s:
        row = s.get(OfficeChannel, row_id)
        if row is None:
            return
        row.config = {**(row.config or {}), **patch}
        s.commit()


def _run_channel(row_id: int, kind: str, config: dict) -> int:
    """Poll one connector, answer everything it returns, persist its cursor. Returns # answered."""
    connector = build_connector(kind, config)
    answered = 0
    for inbound in connector.poll():
        # Show live progress (typing + a status line) while the brain works, if the channel can.
        if connector.supports_progress:
            prog = _Progress(connector, inbound.thread_id)
            connector.typing(inbound.thread_id)
            reply = handle(inbound, on_event=prog.on_event)
            prog.finish()  # drop the status line just before the real answer
        else:
            reply = handle(inbound)
        if not reply:
            continue
        connector.send(
            OutboundReply(thread_id=inbound.thread_id, text=reply, subject=inbound.subject)
        )
        answered += 1
    _persist_cursor(row_id, connector.state_patch())
    return answered


def run_once() -> int:
    """Run one polling pass across all enabled connectors. Returns total messages answered."""
    total = 0
    for row_id, kind, config in _enabled_channels():
        try:
            total += _run_channel(row_id, kind, config)
        except Exception as exc:  # one channel must never break the rest
            print(f"integration {kind} failed: {exc}")
    return total


def main() -> None:
    load_dotenv()
    count = run_once()
    print(f"answered {count} message(s)")


if __name__ == "__main__":
    main()
