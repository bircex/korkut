"""Read-only reporting: roll-ups over the office's own data.

Covers dosya / müvekkil / mali / görev / evrak. No new tables — every figure is derived from what
the other modules already record. Kept in one place so the `/api/reports` endpoints (and, later,
the agent) share the same aggregation logic.
"""

from __future__ import annotations

from datetime import datetime, timedelta

from sqlalchemy import func, select
from sqlalchemy.orm import Session

from korkut.api import finance_store as fin
from korkut.api.models import Client, Deadline, DocumentRecord, Hearing, Matter, Task

_UPCOMING_DAYS = 14  # window for "yaklaşan" hearings/deadlines


def _counts_by(s: Session, column) -> dict[str, int]:
    """Group-count rows by a status/kind column → {value: count} (blanks folded to '')."""
    rows = s.execute(select(column, func.count()).group_by(column))
    return {(value or ""): count for value, count in rows}


def overview(s: Session) -> dict:
    """A single dashboard snapshot: matters, clients, finance, tasks, agenda, evrak.

    Everything the office already stores, summarised for the reporting panel. Dates are compared
    against the server's today; no time zone juggling (the office runs on local time).
    """
    today = datetime.now().date()

    matters_by_status = _counts_by(s, Matter.status)
    clients_total = s.scalar(select(func.count()).select_from(Client)) or 0

    # Tasks: open / done split, plus how many open tasks are past their due date.
    tasks_by_status = _counts_by(s, Task.status)
    overdue_tasks = (
        s.scalar(
            select(func.count())
            .select_from(Task)
            .where(Task.status == "open", Task.due_date.is_not(None), Task.due_date < today)
        )
        or 0
    )

    # Agenda: what is coming up (and what deadlines have already slipped).
    hearings_upcoming = (
        s.scalar(
            select(func.count())
            .select_from(Hearing)
            .where(Hearing.date >= today, Hearing.date <= today + timedelta(days=_UPCOMING_DAYS))
        )
        or 0
    )
    deadlines_upcoming = (
        s.scalar(
            select(func.count())
            .select_from(Deadline)
            .where(
                Deadline.status == "pending",
                Deadline.due_date >= today,
                Deadline.due_date <= today + timedelta(days=_UPCOMING_DAYS),
            )
        )
        or 0
    )
    deadlines_overdue = (
        s.scalar(
            select(func.count())
            .select_from(Deadline)
            .where(Deadline.status == "pending", Deadline.due_date < today)
        )
        or 0
    )

    return {
        "matters": {
            "total": sum(matters_by_status.values()),
            "by_status": matters_by_status,
        },
        "clients": {"total": clients_total},
        "finance": fin.summary(s),
        "tasks": {
            "open": tasks_by_status.get("open", 0),
            "done": tasks_by_status.get("done", 0),
            "overdue": overdue_tasks,
        },
        "agenda": {
            "days": _UPCOMING_DAYS,
            "hearings_upcoming": hearings_upcoming,
            "deadlines_upcoming": deadlines_upcoming,
            "deadlines_overdue": deadlines_overdue,
        },
        "evrak": {
            "total": s.scalar(select(func.count()).select_from(DocumentRecord)) or 0,
            "by_direction": _counts_by(s, DocumentRecord.direction),
        },
    }
