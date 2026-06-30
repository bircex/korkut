"""Integration runner — poll every enabled office connector and answer inbound messages.

    python -m korkut.integrations.runner

One pass: for each enabled `OfficeChannel` whose kind is a two-way connector, build the connector,
poll for new messages, run each through the `gateway` (resolve → answer), and send the reply back.
Then persist the connector's read cursor onto the row so the next pass resumes cleanly.

This is the inbound twin of `korkut.api.dispatcher` (which flushes *outbound* scheduled sends).
Run it on the same schedule — the compose `integrations` service loops it every few seconds; a
non-Docker host can cron it (see deploy/cron/korkut.crontab). Each connector is isolated in a
try/except so one office's bad token can't stall the others.
"""

from __future__ import annotations

from sqlalchemy import select

from korkut.api.models import OfficeChannel
from korkut.api.store import session
from korkut.core.env import load_dotenv
from korkut.integrations import build_connector
from korkut.integrations.base import OutboundReply
from korkut.integrations.gateway import handle
from korkut.integrations.registry import CONNECTORS


def _enabled_channels() -> list[tuple[int, int, str, dict]]:
    """Snapshot (row_id, office_id, kind, config) for every enabled two-way channel."""
    with session() as s:
        rows = s.scalars(
            select(OfficeChannel).where(
                OfficeChannel.enabled.is_(True),
                OfficeChannel.kind.in_(tuple(CONNECTORS)),
            )
        ).all()
        return [(r.id, r.office_id, r.kind, dict(r.config or {})) for r in rows]


def _persist_cursor(row_id: int, patch: dict) -> None:
    if not patch:
        return
    with session() as s:
        row = s.get(OfficeChannel, row_id)
        if row is None:
            return
        row.config = {**(row.config or {}), **patch}
        s.commit()


def _run_channel(row_id: int, office_id: int, kind: str, config: dict) -> int:
    """Poll one connector, answer everything it returns, persist its cursor. Returns # answered."""
    connector = build_connector(kind, office_id, config)
    answered = 0
    for inbound in connector.poll():
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
    for row_id, office_id, kind, config in _enabled_channels():
        try:
            total += _run_channel(row_id, office_id, kind, config)
        except Exception as exc:  # one office must never break the rest
            print(f"integration {kind}/office {office_id} failed: {exc}")
    return total


def main() -> None:
    load_dotenv()
    count = run_once()
    print(f"answered {count} message(s)")


if __name__ == "__main__":
    main()
