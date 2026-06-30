"""Data access for the matter domain (dosya / müvekkil / duruşma).

Shared by the `/api/matters` HTTP endpoints AND the agent's `matter_tools`, so office scoping,
queries and response shaping live in ONE place (no logic duplication, no tenant leaks). Every
function takes a `Session` + `office_id` and enforces that scope. Mirrors the `store.office_*`
helper style.
"""

from __future__ import annotations

from datetime import date as _date
from datetime import datetime
from datetime import time as _time
from datetime import timedelta

from sqlalchemy import func, select
from sqlalchemy.orm import Session

from korkut.api.models import Client, Hearing, Matter

# Fields a caller may set on create/update (whitelist guards against arbitrary attribute writes).
_MATTER_FIELDS = ("title", "file_no", "court", "case_type", "counterparty", "status", "notes")


# ---------- parsing (tool args arrive as strings; endpoints may pass typed values) ----------


def parse_date(value: object) -> _date | None:
    """Accept a date, a datetime, or an ISO 'YYYY-MM-DD' string → date (or None)."""
    if value is None or value == "":
        return None
    if isinstance(value, _date) and not isinstance(value, datetime):
        return value
    if isinstance(value, datetime):
        return value.date()
    return _date.fromisoformat(str(value).strip()[:10])


def parse_time(value: object) -> _time | None:
    """Accept a time or an 'HH:MM'(':SS') string → time (or None)."""
    if value is None or value == "":
        return None
    if isinstance(value, _time):
        return value
    return _time.fromisoformat(str(value).strip())


# ---------- shapers (DTOs; never expose office_id) ----------


def client_brief(c: Client) -> dict:
    return {
        "id": c.id,
        "full_name": c.full_name,
        "phone": c.phone,
        "email": c.email,
        "national_id": c.national_id,
    }


def hearing_brief(h: Hearing) -> dict:
    return {
        "id": h.id,
        "matter_id": h.matter_id,
        "date": h.date.isoformat() if h.date else None,
        "time": h.time.isoformat(timespec="minutes") if h.time else None,
        "location": h.location,
        "type": h.type,
        "result": h.result,
        "reminder_status": h.reminder_status,
        "notes": h.notes,
    }


def matter_brief(m: Matter) -> dict:
    return {
        "id": m.id,
        "title": m.title,
        "client_name": m.client.full_name if m.client else None,
        "file_no": m.file_no,
        "court": m.court,
        "case_type": m.case_type,
        "counterparty": m.counterparty,
        "status": m.status,
        "opened_at": m.opened_at.isoformat() if m.opened_at else None,
        "updated_at": m.updated_at.isoformat() if m.updated_at else None,
    }


def matter_detail(m: Matter) -> dict:
    return {
        **matter_brief(m),
        "client": client_brief(m.client) if m.client else None,
        "notes": m.notes,
        "hearings": [hearing_brief(h) for h in m.hearings],
    }


# ---------- queries / mutations (all office-scoped) ----------


def list_matters(s: Session, office_id: int, status: str | None = None) -> list[Matter]:
    stmt = select(Matter).where(Matter.office_id == office_id)
    if status:
        stmt = stmt.where(Matter.status == status)
    stmt = stmt.order_by(Matter.updated_at.desc(), Matter.id.desc())
    return list(s.scalars(stmt))


def get_matter(s: Session, office_id: int, matter_id: int) -> Matter | None:
    m = s.get(Matter, matter_id)
    return m if m and m.office_id == office_id else None


def list_clients(s: Session, office_id: int) -> list[Client]:
    return list(
        s.scalars(
            select(Client).where(Client.office_id == office_id).order_by(Client.full_name)
        )
    )


def get_or_create_client(s: Session, office_id: int, full_name: str) -> Client | None:
    """Find an office client by exact name (case-insensitive), else create one. None if no name."""
    name = (full_name or "").strip()
    if not name:
        return None
    existing = s.scalar(
        select(Client).where(
            Client.office_id == office_id, func.lower(Client.full_name) == name.lower()
        )
    )
    if existing:
        return existing
    c = Client(office_id=office_id, full_name=name)
    s.add(c)
    s.flush()  # assign c.id without ending the transaction
    return c


def create_matter(
    s: Session,
    office_id: int,
    user_id: int | None,
    *,
    title: str,
    client_name: str | None = None,
    file_no: str | None = None,
    court: str | None = None,
    case_type: str | None = None,
    counterparty: str | None = None,
) -> Matter:
    client = get_or_create_client(s, office_id, client_name) if client_name else None
    m = Matter(
        office_id=office_id,
        created_by=user_id,
        title=title.strip(),
        client_id=client.id if client else None,
        file_no=file_no,
        court=court,
        case_type=case_type,
        counterparty=counterparty,
    )
    s.add(m)
    s.commit()
    return m


def update_matter(s: Session, office_id: int, matter_id: int, **fields: object) -> Matter | None:
    m = get_matter(s, office_id, matter_id)
    if m is None:
        return None
    for key, value in fields.items():
        if key in _MATTER_FIELDS and value is not None:
            setattr(m, key, value)
    s.commit()
    return m


def add_hearing(
    s: Session,
    office_id: int,
    matter_id: int,
    *,
    date: object,
    time: object = None,
    location: str | None = None,
    type: str | None = None,
    notes: str | None = None,
) -> Hearing | None:
    m = get_matter(s, office_id, matter_id)
    if m is None:
        return None
    h = Hearing(
        office_id=office_id,
        matter_id=matter_id,
        date=parse_date(date),
        time=parse_time(time),
        location=location,
        type=type,
        notes=notes,
    )
    s.add(h)
    s.commit()
    return h


def upcoming_hearings(s: Session, office_id: int, days: int = 14) -> list[Hearing]:
    """Office-wide hearings from today through today+days, soonest first (the lawyer's calendar)."""
    today = datetime.now().date()
    end = today + timedelta(days=days)
    stmt = (
        select(Hearing)
        .where(Hearing.office_id == office_id, Hearing.date >= today, Hearing.date <= end)
        .order_by(Hearing.date, Hearing.time)
    )
    return list(s.scalars(stmt))
