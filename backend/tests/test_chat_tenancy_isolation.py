"""The chat/agent TOOL surface stays office-scoped: office A's tools never see office B's data.

Load-bearing guarantee for chat: the agent's matter/client tools open ``store.session()`` under the
ambient tenant context, so isolation must hold across two offices sharing the SAME store.
"""

from __future__ import annotations

import importlib

import pytest

from korkut.api import store
from korkut.api.matter_tools import build_matter_tools
from korkut.api.models import Client, Matter, Office, User
from korkut.core.tenancy import reset_current_office, set_current_office


@pytest.fixture
def two_offices(monkeypatch, tmp_path):
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 't.db'}")
    importlib.reload(store)

    def seed(name):
        # office = tenant root (no context needed); users/clients/matters stamped by context
        with store.session() as s:
            office = Office(name=name)
            s.add(office)
            s.commit()
            oid = office.id
        tok = set_current_office(oid)
        try:
            with store.session() as s:
                u = User(username=f"{name}-user", password_hash="x", role="owner")
                s.add(u)
                s.add(Client(full_name=f"{name}-client"))
                s.commit()
                uid = u.id
                s.add(Matter(title=f"{name}-matter", created_by=uid))
                s.commit()
                mid = s.scalars(store.select(Matter)).first().id
                user = s.get(User, uid)
                s.expunge(user)
        finally:
            reset_current_office(tok)
        return oid, user, mid

    a = seed("A")
    b = seed("B")
    return a, b


def _titles_and_clients(user):
    _specs, handlers = build_matter_tools(user)
    matters = handlers["list_matters"]({})
    clients = handlers["list_clients"]({})
    return matters, clients


def test_tools_see_only_own_office(two_offices):
    (a_oid, a_user, _a_mid), (b_oid, b_user, _b_mid) = two_offices

    tok = set_current_office(a_oid)
    try:
        m, c = _titles_and_clients(a_user)
        dump = repr(m) + repr(c)
        assert "A-matter" in dump and "A-client" in dump
        assert "B-matter" not in dump and "B-client" not in dump
    finally:
        reset_current_office(tok)

    tok = set_current_office(b_oid)
    try:
        m, c = _titles_and_clients(b_user)
        dump = repr(m) + repr(c)
        assert "B-matter" in dump and "B-client" in dump
        assert "A-matter" not in dump and "A-client" not in dump
    finally:
        reset_current_office(tok)


def test_get_matter_across_office_is_not_found(two_offices):
    (a_oid, a_user, _a_mid), (_b_oid, _b_user, b_mid) = two_offices
    # office A's agent asks for office B's matter id -> the tool must not return it
    tok = set_current_office(a_oid)
    try:
        _specs, handlers = build_matter_tools(a_user)
        result = handlers["get_matter"]({"matter_id": b_mid})
        assert "B-matter" not in repr(result)  # never office B's data
    finally:
        reset_current_office(tok)
