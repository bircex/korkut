#!/usr/bin/env python3
"""Compute the next CalVer version (YYYY.M.PATCH) from existing git tags.

For a given YYYY.M period (defaults to the current UTC year.month), this scans the
`v<period>.*` tags and prints the next version: the highest existing patch + 1, or
`<period>.0` when the month has no release yet. Used by the release workflow so the
version number is produced automatically instead of typed by hand.

Usage:
    python scripts/next_calver.py            # current UTC month
    python scripts/next_calver.py 2026.7     # explicit period
"""

from __future__ import annotations

import re
import subprocess
import sys
from datetime import datetime, timezone

PERIOD = re.compile(r"\d{4}\.\d{1,2}")


def _period() -> str:
    if len(sys.argv) > 1:
        p = sys.argv[1].strip().lstrip("v")
        if not PERIOD.fullmatch(p):
            raise SystemExit(f"not a YYYY.M period: {p!r}")
        return p
    now = datetime.now(timezone.utc)
    return f"{now.year}.{now.month}"


def _tags() -> list[str]:
    out = subprocess.run(
        ["git", "tag", "--list"], capture_output=True, text=True, check=True
    ).stdout
    return out.split()


def main() -> None:
    period = _period()
    prefix = f"v{period}."
    patches = [
        int(t[len(prefix) :])
        for t in _tags()
        if t.startswith(prefix) and t[len(prefix) :].isdigit()
    ]
    patch = max(patches) + 1 if patches else 0
    print(f"{period}.{patch}")


if __name__ == "__main__":
    main()
