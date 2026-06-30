#!/usr/bin/env python3
"""Set a single CalVer version (YYYY.M.PATCH) in backend/pyproject.toml and frontend/package.json.

One source of truth, written to both files so the release pipeline stays in sync.

Usage:
    python scripts/bump_version.py 2026.7.0
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CALVER = re.compile(r"\d{4}\.\d{1,2}\.\d+")


def _replace(path: Path, pattern: str, repl: str) -> None:
    text = path.read_text(encoding="utf-8")
    new, n = re.subn(pattern, repl, text, count=1, flags=re.MULTILINE)
    if n != 1:
        raise SystemExit(f"could not find a version line in {path}")
    path.write_text(new, encoding="utf-8")


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("usage: bump_version.py <YYYY.M.PATCH>")
    version = sys.argv[1].strip().lstrip("v")
    if not CALVER.fullmatch(version):
        raise SystemExit(f"not a CalVer YYYY.M.PATCH version: {version!r}")
    _replace(ROOT / "backend" / "pyproject.toml", r'^version = ".*"', f'version = "{version}"')
    _replace(ROOT / "frontend" / "package.json", r'^(\s*"version":\s*)".*"', rf'\g<1>"{version}"')
    print(f"Set version to {version} in pyproject.toml and package.json")


if __name__ == "__main__":
    main()
