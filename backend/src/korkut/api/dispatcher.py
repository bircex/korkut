"""Scheduled-send worker — run periodically (cron) to flush due outbound work.

    python -m korkut.api.dispatcher

Flushes two due queues, then exits: every queued `OutboundMessage` whose `send_at` has arrived,
and every calendar-event reminder whose reminder moment has passed. Idempotent: each row is moved
to a terminal state (sent/failed/skipped) as it is handled, so overlapping runs can't double-send.
Wire it from deploy/cron (e.g. every minute). The sending logic lives in `korkut.api.store`.
"""

from __future__ import annotations

from korkut.api.store import dispatch_due, dispatch_due_reminders
from korkut.core.env import load_dotenv


def main() -> None:
    load_dotenv()
    count = dispatch_due()
    reminders = dispatch_due_reminders()
    print(f"dispatched {count} message(s), {reminders} reminder(s)")


if __name__ == "__main__":
    main()
