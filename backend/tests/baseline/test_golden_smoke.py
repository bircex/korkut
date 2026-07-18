"""KDM-002 — assert the Python runtime still matches the recorded golden baseline.

Runs the deterministic critical-flow request matrix (see ``golden_smoke.py``) and
compares status + response shape against the committed ``golden_smoke.json``.
Regenerate intentionally with:

    KORKUT_BASELINE_UPDATE=1 ../.venv/bin/python -m tests.baseline.golden_smoke

On top of the golden comparison, a few invariants are asserted explicitly so a
drift that happens to still "match a regenerated golden" can never silently
weaken tenant isolation or auth.
"""

from __future__ import annotations

import pytest

from tests.baseline import golden_smoke as gs


@pytest.fixture
def baseline(tmp_path):
    app_mod, ctx = gs.build_app(tmp_path / "baseline.db")
    return gs.collect(app_mod, ctx)


def test_golden_matches_recorded_baseline(baseline):
    expected = gs.load_golden()
    assert set(baseline) == set(expected), "golden case set drifted"
    mismatches = {
        cid: {"expected": expected[cid], "got": baseline[cid]}
        for cid in expected
        if baseline[cid] != expected[cid]
    }
    assert not mismatches, f"golden drift in cases: {sorted(mismatches)}"


def test_security_invariants_hold(baseline):
    # Unauthenticated protected reads are rejected.
    assert baseline["unauth_me"]["status"] == 401
    assert baseline["unauth_matters"]["status"] == 401
    # Public bootstrap stays public.
    assert baseline["public_config"]["status"] == 200
    # Cross-office IDOR is denied (must never be 200).
    assert baseline["idor_cross_office_matter"]["status"] in (403, 404)
    # Invalid body is a validation error, not a 500.
    assert baseline["error_matter_bad_body"]["status"] == 422
    # Missing resource is a clean 404.
    assert baseline["error_matter_not_found"]["status"] == 404
