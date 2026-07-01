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
    CalendarEvent,
    Client,
    Conversation,
    Document,
    DocumentRecord,
    Matter,
    Task,
    User,
)

# Event categories that behave like a legal deadline (süre) vs. an attended event.
_DEADLINE = "deadline"
_HEARING = "hearing"

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


def parse_datetime(value: object) -> datetime | None:
    """Accept a datetime, a date, or an ISO string → datetime (or None).

    A bare 'YYYY-MM-DD' becomes midnight; a full ISO timestamp keeps its time.
    """
    if value is None or value == "":
        return None
    if isinstance(value, datetime):
        return value
    if isinstance(value, _date):
        return datetime.combine(value, _time())
    return datetime.fromisoformat(str(value).strip())


def _combine(d: _date, t: _time | None) -> datetime:
    """A date + optional time → datetime (midnight when the time is absent)."""
    return datetime.combine(d, t or _time())


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


def document_record_brief(d: DocumentRecord) -> dict:
    return {
        "id": d.id,
        "matter_id": d.matter_id,
        "direction": d.direction,
        "title": d.title,
        "reference_no": d.reference_no,
        "doc_date": d.doc_date.isoformat() if d.doc_date else None,
        "notes": d.notes,
    }


def list_document_records(
    s: Session, matter_id: int | None = None, direction: str | None = None
) -> list[DocumentRecord]:
    """Evrak entries, newest doc-date first (undated last); filtered by matter/direction."""
    q = select(DocumentRecord).order_by(
        DocumentRecord.doc_date.is_(None),
        DocumentRecord.doc_date.desc(),
        DocumentRecord.created_at.desc(),
    )
    if matter_id is not None:
        q = q.where(DocumentRecord.matter_id == matter_id)
    if direction:
        q = q.where(DocumentRecord.direction == direction)
    return list(s.scalars(q))


def hearing_brief(h: CalendarEvent) -> dict:
    """Shape a hearing-category event into the legacy duruşma DTO (date + optional time)."""
    return {
        "id": h.id,
        "matter_id": h.matter_id,
        "date": h.start_at.date().isoformat() if h.start_at else None,
        "time": (
            None if h.all_day or h.start_at is None else h.start_at.time().isoformat("minutes")
        ),
        "location": h.location,
        "type": h.type,
        "result": h.result,
        "reminder_status": h.reminder_status,
        "notes": h.notes,
    }


def event_brief(e: CalendarEvent) -> dict:
    """Full calendar DTO for the takvim UI — every field a calendar entry can carry."""
    return {
        "id": e.id,
        "category": e.category,
        "type": e.type,
        "title": e.title,
        "start_at": e.start_at.isoformat() if e.start_at else None,
        "end_at": e.end_at.isoformat() if e.end_at else None,
        "all_day": e.all_day,
        "matter_id": e.matter_id,
        "matter_title": e.matter.title if e.matter else None,
        "client_id": e.client_id,
        "client_name": e.client.full_name if e.client else None,
        "assignee_id": e.assignee_id,
        "assignee_name": e.assignee.full_name if e.assignee else None,
        "location": e.location,
        "status": e.status,
        "result": e.result,
        "notes": e.notes,
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


def deadline_brief(d: CalendarEvent) -> dict:
    """Shape a deadline-category event into the legacy süre DTO (due_date)."""
    return {
        "id": d.id,
        "matter_id": d.matter_id,
        "due_date": d.start_at.date().isoformat() if d.start_at else None,
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
        "hearings": [hearing_brief(e) for e in m.events if e.category == _HEARING],
        "deadlines": [deadline_brief(e) for e in m.events if e.category == _DEADLINE],
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
) -> CalendarEvent | None:
    m = get_matter(s, matter_id)
    if m is None:
        return None
    day = parse_date(date)
    clock = parse_time(time)
    h = CalendarEvent(
        category=_HEARING,
        matter_id=matter_id,
        title=(type or "Duruşma"),
        start_at=_combine(day, clock) if day else None,
        all_day=clock is None,
        location=location,
        type=type,
        notes=notes,
    )
    s.add(h)
    s.commit()
    return h


def upcoming_hearings(s: Session, days: int = 14) -> list[CalendarEvent]:
    """Upcoming hearings from today through today+days, soonest first (the lawyer's calendar)."""
    stmt = (
        select(CalendarEvent)
        .where(CalendarEvent.category == _HEARING, *_window(days))
        .order_by(CalendarEvent.start_at)
    )
    return list(s.scalars(stmt))


# ---------- deadlines (süre) ----------


def list_deadlines(s: Session, matter_id: int | None = None) -> list[CalendarEvent]:
    stmt = select(CalendarEvent).where(CalendarEvent.category == _DEADLINE)
    if matter_id is not None:
        stmt = stmt.where(CalendarEvent.matter_id == matter_id)
    return list(s.scalars(stmt.order_by(CalendarEvent.start_at)))


def add_deadline(
    s: Session,
    matter_id: int,
    *,
    due_date: object,
    title: str,
    type: str | None = None,
    notes: str | None = None,
) -> CalendarEvent | None:
    m = get_matter(s, matter_id)
    if m is None:
        return None
    day = parse_date(due_date)
    d = CalendarEvent(
        category=_DEADLINE,
        matter_id=matter_id,
        title=title.strip(),
        start_at=_combine(day, None) if day else None,
        all_day=True,
        status="pending",
        type=type,
        notes=notes,
    )
    s.add(d)
    s.commit()
    return d


def complete_deadline(s: Session, deadline_id: int) -> CalendarEvent | None:
    d = s.get(CalendarEvent, deadline_id)
    if d is None or d.category != _DEADLINE:
        return None
    d.status = "done"
    s.commit()
    return d


def upcoming_agenda(s: Session, days: int = 14) -> list[dict]:
    """Unified calendar: upcoming hearings + pending deadlines, soonest first.

    Each item is tagged with `kind` ("hearing"|"deadline") and a common `date` so the agent and UI
    can show one merged list of "yaklaşan duruşmalar/süreler".
    """
    items: list[dict] = []
    for h in s.scalars(
        select(CalendarEvent).where(CalendarEvent.category == _HEARING, *_window(days))
    ):
        b = hearing_brief(h)
        items.append({"kind": "hearing", "date": b["date"], **b})
    for d in s.scalars(
        select(CalendarEvent).where(
            CalendarEvent.category == _DEADLINE, CalendarEvent.status == "pending", *_window(days)
        )
    ):
        b = deadline_brief(d)
        items.append({"kind": "deadline", "date": b["due_date"], **b})
    items.sort(key=lambda x: (x["date"] or "", x.get("time") or ""))
    return items


# ---------- calendar events (takvim) — the unified, all-category surface ----------


def _window(days: int) -> tuple:
    """SQL bounds covering today through the end of today+days (inclusive), for start_at."""
    today = datetime.now().date()
    lo = datetime.combine(today, _time())
    hi = datetime.combine(today + timedelta(days=days + 1), _time())
    return (CalendarEvent.start_at >= lo, CalendarEvent.start_at < hi)


def list_events(
    s: Session,
    *,
    start: object = None,
    end: object = None,
    category: str | None = None,
    matter_id: int | None = None,
    client_id: int | None = None,
    assignee_id: int | None = None,
) -> list[CalendarEvent]:
    """Calendar events in an optional [start, end] datetime range, filtered as given."""
    stmt = select(CalendarEvent)
    lo, hi = parse_datetime(start), parse_datetime(end)
    if lo is not None:
        stmt = stmt.where(CalendarEvent.start_at >= lo)
    if hi is not None:
        stmt = stmt.where(CalendarEvent.start_at <= hi)
    if category:
        stmt = stmt.where(CalendarEvent.category == category)
    if matter_id is not None:
        stmt = stmt.where(CalendarEvent.matter_id == matter_id)
    if client_id is not None:
        stmt = stmt.where(CalendarEvent.client_id == client_id)
    if assignee_id is not None:
        stmt = stmt.where(CalendarEvent.assignee_id == assignee_id)
    return list(s.scalars(stmt.order_by(CalendarEvent.start_at)))


_EVENT_CATEGORIES = ("hearing", "deadline", "appointment", "reminder")


def create_event(
    s: Session,
    *,
    created_by: int | None,
    category: str,
    title: str,
    start_at: object,
    end_at: object = None,
    all_day: bool = False,
    type: str | None = None,
    matter_id: int | None = None,
    client_id: int | None = None,
    assignee_id: int | None = None,
    location: str | None = None,
    notes: str | None = None,
) -> CalendarEvent:
    """Create any calendar event. Deadlines default to pending; others to planned."""
    cat = category if category in _EVENT_CATEGORIES else "appointment"
    e = CalendarEvent(
        category=cat,
        title=title.strip(),
        start_at=parse_datetime(start_at),
        end_at=parse_datetime(end_at),
        all_day=all_day,
        status="pending" if cat == _DEADLINE else "planned",
        type=(type or "").strip() or None,
        matter_id=matter_id,
        client_id=client_id,
        assignee_id=assignee_id,
        location=(location or "").strip() or None,
        notes=(notes or "").strip() or None,
        created_by=created_by,
    )
    s.add(e)
    s.commit()
    return e


# Fields a caller may patch on an event (whitelist; None means "clear", except title).
_EVENT_FIELDS = (
    "category",
    "type",
    "title",
    "all_day",
    "matter_id",
    "client_id",
    "assignee_id",
    "location",
    "status",
    "result",
    "notes",
)


def update_event(s: Session, event_id: int, **fields: object) -> CalendarEvent | None:
    e = s.get(CalendarEvent, event_id)
    if e is None:
        return None
    if "start_at" in fields:
        parsed = parse_datetime(fields["start_at"])
        if parsed is not None:
            e.start_at = parsed
    if "end_at" in fields:
        e.end_at = parse_datetime(fields["end_at"])
    for key, value in fields.items():
        if key in _EVENT_FIELDS:
            setattr(e, key, value)
    s.commit()
    return e


def delete_event(s: Session, event_id: int) -> bool:
    e = s.get(CalendarEvent, event_id)
    if e is None:
        return False
    s.delete(e)
    s.commit()
    return True
