"""Shared test setup.

Every test runs inside a tenant context (office 1) — matching the single office the fixtures
bootstrap — so tenant models stamp ``office_id`` on insert. Multi-tenant tests (``test_tenancy_*``)
set their own context explicitly and are unaffected.
"""

from __future__ import annotations

import pytest

from korkut.core.tenancy import reset_current_office, set_current_office


@pytest.fixture(autouse=True)
def _tenant_context():
    token = set_current_office(1)
    try:
        yield
    finally:
        reset_current_office(token)
