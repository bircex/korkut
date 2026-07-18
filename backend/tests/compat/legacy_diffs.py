"""KDM-003 — legacy differential-diff allowlist loader and validator.

Known, accepted Python<->.NET differences are recorded in ``legacy_diffs.json``,
each with an owner and an expiry. This module loads and validates them and tells
the differential test which fixture ids to exempt — but only while the entry is
well-formed and unexpired. An expired entry is a hard error, forcing the owner to
either resolve the difference or consciously renew the exemption.
"""

from __future__ import annotations

import datetime as _dt
import json
from pathlib import Path
from typing import Any

PATH = Path(__file__).with_name("legacy_diffs.json")
REQUIRED_FIELDS = ("fixture_id", "owner", "reason", "expires", "tracking_issue")


class LegacyDiffError(ValueError):
    """Raised when an allowlist entry is malformed or expired."""


def load(path: Path | None = None) -> list[dict[str, Any]]:
    data = json.loads((path or PATH).read_text())
    diffs = data.get("diffs", [])
    if not isinstance(diffs, list):
        raise LegacyDiffError("'diffs' must be a list")
    return diffs


def validate(diffs: list[dict[str, Any]], *, today: _dt.date) -> None:
    """Raise on any malformed or expired entry. No-op for an empty list."""
    for i, entry in enumerate(diffs):
        missing = [f for f in REQUIRED_FIELDS if not entry.get(f)]
        if missing:
            raise LegacyDiffError(f"diff #{i} missing fields: {missing}")
        try:
            expires = _dt.date.fromisoformat(entry["expires"])
        except (TypeError, ValueError) as exc:
            raise LegacyDiffError(f"diff #{i} '{entry['fixture_id']}' bad expires date") from exc
        if expires < today:
            raise LegacyDiffError(
                f"diff #{i} '{entry['fixture_id']}' expired on {entry['expires']} "
                f"(owner {entry['owner']}, {entry['tracking_issue']}) — resolve or renew"
            )


def exempt_fixture_ids(diffs: list[dict[str, Any]]) -> set[str]:
    return {entry["fixture_id"] for entry in diffs}
