"""Data access for the matter domain (dosya / müvekkil / duruşma).

Shared by the `/api/matters` HTTP endpoints AND the agent's `matter_tools`, so queries and
response shaping live in ONE place (no logic duplication). Every function takes a `Session`.
Mirrors the `store.office_*` helper style.
"""

from __future__ import annotations

from datetime import date as _date
from datetime import datetime, timedelta
from datetime import time as _time

from sqlalchemy import func, select
from sqlalchemy.orm import Session

from korkut.api.models import (
    Client,
    Conversation,
    Deadline,
    Document,
    Hearing,
    Matter,
    Task,
    User,
)

# Fields a caller may set on create/update (whitelist guards against arbitrary attribute writes).
_MATTER_FIELDS = ("title", "file_no", "court", "case_type", "counterparty", "status", "notes")


# ---------- redaction support (Layer 4: exact PII the office stores) ----------


def collect_known_entities(s: Session) -> list[tuple[str, str]]:
    """Exact PII values the office already stores — for the redactor to mask deterministically
    instead of guessing (see core.redactor Layer 4). Covers client identities (name, TCKN, phone,
    e-mail, address, birth date), matter counterparties, and lawyer names + bar-registry numbers.
    Returned as (value, kind) pairs; the
    redactor dedupes and drops blanks. Collected office-wide so any stored party's PII is masked
    wherever it appears in a cloud-bound prompt, not only the matter in context.
    """
    entities: list[tuple[str, str]] = []
    for c in s.scalars(select(Client)):
        entities.append((c.full_name, "PERSON"))
        entities.append((c.national_id or "", "TCKN"))
        entities.append((c.phone or "", "PHONE"))
        entities.append((c.email or "", "EMAIL"))
        entities.append((c.address or "", "ADDRESS"))
        if c.date_of_birth is not None:
            entities.append((c.date_of_birth.strftime("%d.%m.%Y"), "DOB"))
    for counterparty in s.scalars(
        select(Matter.counterparty).where(Matter.counterparty.is_not(None))
    ):
        entities.append((counterparty, "PERSON"))
    for u in s.scalars(select(User)):
        if u.full_name:
            entities.append((u.full_name, "PERSON"))
        if u.bar_registry_no:
            entities.append((u.bar_registry_no, "BAR_NO"))
    return entities


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


# ---------- shapers (DTOs; never expose internal tenant identifiers) ----------


def client_brief(c: Client) -> dict:
    return {
        "id": c.id,
        "full_name": c.full_name,
        "phone": c.phone,
        "email": c.email,
        "national_id": c.national_id,
        "address": c.address,
        "date_of_birth": c.date_of_birth.isoformat() if c.date_of_birth else None,
    }


def task_brief(t: Task) -> dict:
    return {
        "id": t.id,
        "title": t.title,
        "description": t.description,
        "assignee_id": t.assignee_id,
        "assignee_name": t.assignee.full_name if t.assignee else None,
        "matter_id": t.matter_id,
        "status": t.status,
        "due_date": t.due_date.isoformat() if t.due_date else None,
    }


def list_tasks(
    s: Session,
    status: str | None = None,
    assignee_id: int | None = None,
    matter_id: int | None = None,
) -> list[Task]:
    """Tasks ordered by due date (undated last), then newest first; filtered as given."""
    q = select(Task).order_by(Task.due_date.is_(None), Task.due_date, Task.created_at.desc())
    if status:
        q = q.where(Task.status == status)
    if assignee_id is not None:
        q = q.where(Task.assignee_id == assignee_id)
    if matter_id is not None:
        q = q.where(Task.matter_id == matter_id)
    return list(s.scalars(q))


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


def deadline_brief(d: Deadline) -> dict:
    return {
        "id": d.id,
        "matter_id": d.matter_id,
        "due_date": d.due_date.isoformat() if d.due_date else None,
        "title": d.title,
        "type": d.type,
        "status": d.status,
        "notes": d.notes,
    }


def matter_detail(m: Matter) -> dict:
    return {
        **matter_brief(m),
        "client": client_brief(m.client) if m.client else None,
        "notes": m.notes,
        "hearings": [hearing_brief(h) for h in m.hearings],
        "deadlines": [deadline_brief(d) for d in m.deadlines],
    }


# ---------- queries / mutations ----------


def list_matters(s: Session, status: str | None = None) -> list[Matter]:
    stmt = select(Matter)
    if status:
        stmt = stmt.where(Matter.status == status)
    stmt = stmt.order_by(Matter.updated_at.desc(), Matter.id.desc())
    return list(s.scalars(stmt))


def get_matter(s: Session, matter_id: int) -> Matter | None:
    return s.get(Matter, matter_id)


def matter_documents(s: Session, matter_id: int) -> list[dict]:
    """Documents (drafts) of conversations filed under this matter — the matter's work product."""
    rows = s.scalars(
        select(Document)
        .join(Conversation, Document.conversation_id == Conversation.id)
        .where(Conversation.matter_id == matter_id)
        .order_by(Document.updated_at.desc())
    )
    return [
        {
            "id": d.id,
            "conversation_id": d.conversation_id,
            "title": d.title,
            "status": d.status,
            "updated_at": d.updated_at.isoformat() if d.updated_at else None,
        }
        for d in rows
    ]


def matter_full(s: Session, matter_id: int) -> dict | None:
    """Full matter view (UI + agent): detail (hearings + deadlines) + linked documents."""
    m = get_matter(s, matter_id)
    if m is None:
        return None
    return {**matter_detail(m), "documents": matter_documents(s, matter_id)}


def list_clients(s: Session) -> list[Client]:
    return list(s.scalars(select(Client).order_by(Client.full_name)))


def get_or_create_client(s: Session, full_name: str) -> Client | None:
    """Find a client by exact name (case-insensitive), else create one. None if no name."""
    name = (full_name or "").strip()
    if not name:
        return None
    existing = s.scalar(select(Client).where(func.lower(Client.full_name) == name.lower()))
    if existing:
        return existing
    c = Client(full_name=name)
    s.add(c)
    s.flush()  # assign c.id without ending the transaction
    return c


def create_matter(
    s: Session,
    user_id: int | None,
    *,
    title: str,
    client_name: str | None = None,
    file_no: str | None = None,
    court: str | None = None,
    case_type: str | None = None,
    counterparty: str | None = None,
) -> Matter:
    client = get_or_create_client(s, client_name) if client_name else None
    m = Matter(
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


def update_matter(s: Session, matter_id: int, **fields: object) -> Matter | None:
    m = get_matter(s, matter_id)
    if m is None:
        return None
    for key, value in fields.items():
        if key in _MATTER_FIELDS and value is not None:
            setattr(m, key, value)
    s.commit()
    return m


def add_hearing(
    s: Session,
    matter_id: int,
    *,
    date: object,
    time: object = None,
    location: str | None = None,
    type: str | None = None,
    notes: str | None = None,
) -> Hearing | None:
    m = get_matter(s, matter_id)
    if m is None:
        return None
    h = Hearing(
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


def upcoming_hearings(s: Session, days: int = 14) -> list[Hearing]:
    """Upcoming hearings from today through today+days, soonest first (the lawyer's calendar)."""
    today = datetime.now().date()
    end = today + timedelta(days=days)
    stmt = (
        select(Hearing)
        .where(Hearing.date >= today, Hearing.date <= end)
        .order_by(Hearing.date, Hearing.time)
    )
    return list(s.scalars(stmt))


# ---------- deadlines (süre) ----------


def list_deadlines(s: Session, matter_id: int | None = None) -> list[Deadline]:
    stmt = select(Deadline)
    if matter_id is not None:
        stmt = stmt.where(Deadline.matter_id == matter_id)
    return list(s.scalars(stmt.order_by(Deadline.due_date)))


def add_deadline(
    s: Session,
    matter_id: int,
    *,
    due_date: object,
    title: str,
    type: str | None = None,
    notes: str | None = None,
) -> Deadline | None:
    m = get_matter(s, matter_id)
    if m is None:
        return None
    d = Deadline(
        matter_id=matter_id,
        due_date=parse_date(due_date),
        title=title.strip(),
        type=type,
        notes=notes,
    )
    s.add(d)
    s.commit()
    return d


def complete_deadline(s: Session, deadline_id: int) -> Deadline | None:
    d = s.get(Deadline, deadline_id)
    if d is None:
        return None
    d.status = "done"
    s.commit()
    return d


def upcoming_agenda(s: Session, days: int = 14) -> list[dict]:
    """Unified calendar: upcoming hearings + pending deadlines, soonest first.

    Each item is tagged with `kind` ("hearing"|"deadline") and a common `date` so the agent and UI
    can show one merged list of "yaklaşan duruşmalar/süreler".
    """
    today = datetime.now().date()
    end = today + timedelta(days=days)
    items: list[dict] = []
    for h in s.scalars(select(Hearing).where(Hearing.date >= today, Hearing.date <= end)):
        items.append({"kind": "hearing", "date": h.date.isoformat(), **hearing_brief(h)})
    for d in s.scalars(
        select(Deadline).where(
            Deadline.status == "pending",
            Deadline.due_date >= today,
            Deadline.due_date <= end,
        )
    ):
        items.append({"kind": "deadline", "date": d.due_date.isoformat(), **deadline_brief(d)})
    items.sort(key=lambda x: (x["date"], x.get("time") or ""))
    return items
