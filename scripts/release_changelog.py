#!/usr/bin/env python3
"""Promote the CHANGELOG.md [Unreleased] section to a dated release, leaving a fresh Unreleased.

Used by the release workflow. Date is passed in (CI provides it) so the script stays deterministic.

Usage:
    python scripts/release_changelog.py 2026.7.0 2026-07-01
"""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: release_changelog.py <version> <YYYY-MM-DD>")
    version = sys.argv[1].strip().lstrip("v")
    date = sys.argv[2].strip()
    path = ROOT / "CHANGELOG.md"
    text = path.read_text(encoding="utf-8")
    marker = "## [Unreleased]"
    if marker not in text:
        raise SystemExit("no [Unreleased] section in CHANGELOG.md")
    text = text.replace(marker, f"## [Unreleased]\n\n## [{version}] - {date}", 1)
    path.write_text(text, encoding="utf-8")
    print(f"CHANGELOG: promoted Unreleased to {version} ({date})")


if __name__ == "__main__":
    main()
