"""KDM-003 — compatibility contract & differential parity tests.

Runs live today against the Python runtime; the cross-runtime comparison
activates when a .NET adapter is configured (KDM-034).
"""

from __future__ import annotations

import datetime as dt
import json

import pytest

from tests.compat import differential, legacy_diffs, openapi_snapshot
from tests.compat.adapters import DotNetAdapter

# --- Contract drift guard: API surface cannot change without an approved update ---


def test_openapi_matches_snapshot():
    live = openapi_snapshot.snapshot()
    committed = openapi_snapshot.load_snapshot()
    assert live == committed, (
        "OpenAPI contract drifted from tests/compat/contracts/openapi.json. "
        "If intended, regenerate with KORKUT_CONTRACT_UPDATE=1 "
        "python -m tests.compat.openapi_snapshot and get the contract change reviewed."
    )


# --- Recorded parity: Python output must match the committed expected record ---


def test_python_differential_matches_expected(tmp_path):
    results = differential.run_python(tmp_path / "diff.db")
    expected = differential.load_expected_python()
    mismatches = differential.diff_results(expected, results)
    assert not mismatches, f"Python differential drift: {sorted(mismatches)}"


def test_fixtures_are_secret_and_pii_free():
    # Scan the concrete fixture VALUES (not the descriptive _comment) for
    # secret/PII value *patterns*: provider keys, JWTs, bearer creds, real email
    # addresses, and 11-digit Turkish national ids (TCKN).
    import re

    fixtures = differential.load_fixtures()
    blob = json.dumps(fixtures, ensure_ascii=False)
    patterns = {
        "provider key": re.compile(r"sk-[A-Za-z0-9]{16,}|AIza[A-Za-z0-9_-]{20,}"),
        "jwt": re.compile(r"[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}"),
        "bearer": re.compile(r"[Bb]earer\s+[A-Za-z0-9._-]+"),
        "email": re.compile(r"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"),
        "tckn": re.compile(r"\b\d{11}\b"),
    }
    hits = {name: rx.findall(blob) for name, rx in patterns.items() if rx.search(blob)}
    assert not hits, f"fixture values contain secret/PII-shaped data: {hits}"


# --- Legacy-diff allowlist: well-formed, unexpired, owned ---


def test_legacy_diff_allowlist_is_valid():
    diffs = legacy_diffs.load()
    legacy_diffs.validate(diffs, today=dt.date.today())  # raises on malformed/expired


def test_legacy_diff_validator_rejects_bad_entries():
    today = dt.date(2026, 1, 1)
    good = [
        {
            "fixture_id": "config_public",
            "owner": "@recepkizilarslan",
            "reason": "x",
            "expires": "2026-12-31",
            "tracking_issue": "https://github.com/recepkizilarslan/korkut-backend/issues/96",
        }
    ]
    legacy_diffs.validate(good, today=today)  # ok

    with pytest.raises(legacy_diffs.LegacyDiffError):
        legacy_diffs.validate([{**good[0], "owner": ""}], today=today)  # missing field
    with pytest.raises(legacy_diffs.LegacyDiffError):
        legacy_diffs.validate([{**good[0], "expires": "2025-01-01"}], today=today)  # expired


# --- Cross-runtime parity: activates at KDM-034 when the .NET adapter exists ---


def test_cross_runtime_parity(tmp_path):
    dotnet = DotNetAdapter()
    if not dotnet.available():
        pytest.skip(
            "no .NET adapter (KORKUT_DOTNET_BASE_URL unset). The .NET runtime is "
            "built in KDM-005+ and this cross-runtime diff is wired in KDM-034."
        )
    python = differential.run_python(tmp_path / "diff.db")  # pragma: no cover
    dn = differential.run_adapter(dotnet, differential.load_fixtures())  # pragma: no cover
    exempt = legacy_diffs.exempt_fixture_ids(legacy_diffs.load())  # pragma: no cover
    mismatches = {  # pragma: no cover
        k: v for k, v in differential.diff_results(python, dn).items() if k not in exempt
    }
    assert not mismatches, f"cross-runtime parity diffs: {json.dumps(mismatches, indent=2)}"
