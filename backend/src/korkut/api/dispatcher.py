"""Scheduled-send worker — run periodically (cron) to flush due outbound messages.

    python -m korkut.api.dispatcher

Sends every queued `OutboundMessage` whose `send_at` has arrived (across all offices and
channels), then exits. Idempotent: only `queued` rows are touched and each becomes
`sent`/`failed`, so overlapping runs can't double-send. Wire it from deploy/cron (e.g. every
minute). The actual sending logic lives in `korkut.api.store.dispatch_due`.
"""

from __future__ import annotations

from korkut.api.store import dispatch_due
from korkut.core.env import load_dotenv


def main() -> None:
    load_dotenv()
    count = dispatch_due()
    print(f"dispatched {count} message(s)")


if __name__ == "__main__":
    main()
