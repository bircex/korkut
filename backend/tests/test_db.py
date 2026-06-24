"""Tests for core.db — models, relationships, and the upcoming_hearings helper (SQLite)."""

from datetime import date, time

import pytest

from korkut.core.db import (
    PETITION_APPROVED,
    PETITION_DRAFT,
    Case,
    Client,
    Hearing,
    Petition,
    init_db,
    make_engine,
    upcoming_hearings,
)

sqlalchemy = pytest.importorskip("sqlalchemy")
from sqlalchemy.orm import Session  # noqa: E402


def _session() -> Session:
    engine = make_engine()
    init_db(engine)
    return Session(engine)


def test_create_and_relationships():
    s = _session()
    client = Client(full_name="Ahmet Yılmaz", national_id="10000000146")
    case = Case(file_no="2024/123", client=client, court="Antalya 3. Aile Mahkemesi")
    case.hearings.append(Hearing(date=date(2026, 7, 15), time=time(10, 0), location="Salon 2"))
    case.petitions.append(Petition(type="cevap", status=PETITION_DRAFT))
    s.add(client)
    s.commit()

    got = s.get(Case, case.id)
    assert got.client.full_name == "Ahmet Yılmaz"
    assert len(got.hearings) == 1
    assert got.petitions[0].status == PETITION_DRAFT


def test_upcoming_hearings_window():
    s = _session()
    c = Client(full_name="Test")
    case = Case(file_no="1/1", client=c)
    case.hearings.append(Hearing(date=date(2026, 7, 2)))  # in window
    case.hearings.append(Hearing(date=date(2026, 7, 20)))  # out of window
    s.add(c)
    s.commit()

    rows = upcoming_hearings(s, today=date(2026, 7, 1), days=7)
    assert [h.date for h in rows] == [date(2026, 7, 2)]


def test_petition_approval_transition():
    s = _session()
    c = Client(full_name="Test")
    case = Case(file_no="1/1", client=c)
    pet = Petition(type="dava", status=PETITION_DRAFT, case=case)
    s.add_all([c, case, pet])
    s.commit()

    pet.status = PETITION_APPROVED
    s.commit()
    assert s.get(Petition, pet.id).status == PETITION_APPROVED
