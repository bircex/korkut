"""Unit tests for matters_store pure helpers — date/time parsing and DTO shapers (no DB needed)."""

from datetime import date, datetime, time

import pytest

from korkut.api import matters_store as ms
from korkut.api.models import CalendarEvent, Client, Matter


def test_parse_date():
    assert ms.parse_date("2026-07-12") == date(2026, 7, 12)
    assert ms.parse_date(date(2026, 7, 12)) == date(2026, 7, 12)
    assert ms.parse_date(datetime(2026, 7, 12, 10, 30)) == date(2026, 7, 12)
    assert ms.parse_date(None) is None
    assert ms.parse_date("") is None
    with pytest.raises(ValueError):
        ms.parse_date("12 temmuz")


def test_parse_time():
    assert ms.parse_time("10:30") == time(10, 30)
    assert ms.parse_time("10:30:00") == time(10, 30, 0)
    assert ms.parse_time(time(9, 0)) == time(9, 0)
    assert ms.parse_time(None) is None
    assert ms.parse_time("") is None


def test_shapers_never_leak_office_id():
    c = Client(full_name="Ahmet Yılmaz", phone="555", email="a@x.tr", national_id="123")
    assert ms.client_brief(c) == {
        "id": None,
        "full_name": "Ahmet Yılmaz",
        "phone": "555",
        "email": "a@x.tr",
        "national_id": "123",
        "address": None,
        "date_of_birth": None,
    }
    assert "office_id" not in ms.client_brief(c)

    h = CalendarEvent(
        category="hearing",
        matter_id=2,
        title="Duruşma",
        start_at=datetime(2026, 7, 12, 10, 30),
        all_day=False,
        type="ön inceleme",
    )
    hb = ms.hearing_brief(h)
    assert hb["date"] == "2026-07-12" and hb["time"] == "10:30" and hb["type"] == "ön inceleme"
    assert "office_id" not in hb

    m = Matter(title="Dosya", status="open", file_no="2026/1")
    mb = ms.matter_brief(m)
    assert mb["title"] == "Dosya" and mb["status"] == "open" and mb["file_no"] == "2026/1"
    assert mb["client_name"] is None  # no client attached
    assert "office_id" not in mb


def test_matter_detail_includes_hearings_and_client():
    m = Matter(title="Dosya", status="open")
    m.client = Client(full_name="Ayşe")
    m.events = [
        CalendarEvent(
            category="hearing",
            matter_id=1,
            title="Duruşma",
            start_at=datetime(2026, 1, 1),
            all_day=True,
        )
    ]
    detail = ms.matter_detail(m)
    assert detail["client"]["full_name"] == "Ayşe"
    assert len(detail["hearings"]) == 1
