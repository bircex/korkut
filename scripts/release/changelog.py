#!/usr/bin/env python3
"""CHANGELOG.md (Keep a Changelog) for releases.

  changelog.py cut VERSION [DATE]   move the Unreleased notes into a "[VERSION] - DATE" section,
                                    leave an empty Unreleased and update the compare links
  changelog.py notes VERSION        print that version's notes (for the GitHub release)
  changelog.py check                fail if Unreleased has no notes (nothing to release)
"""
import datetime
import re
import sys
from pathlib import Path

REPO = "https://github.com/bircex/korkut"
PATH = Path(__file__).resolve().parents[2] / "CHANGELOG.md"
HEADING = re.compile(r"^## \[(?P<name>[^\]]+)\](?: - (?P<date>\d{4}-\d{2}-\d{2}))?\s*$", re.M)
LINKS = re.compile(r"^\[[^\]]+\]: .*$\n?", re.M)


def sections(text):
    """(name, start, body_start, end) for each "## [...]" section."""
    found = list(HEADING.finditer(text))
    out = []
    for i, m in enumerate(found):
        end = found[i + 1].start() if i + 1 < len(found) else (LINKS.search(text, m.end()) or re.compile(r"\Z").search(text)).start()
        out.append((m.group("name"), m.start(), m.end(), end))
    return out


def body(text, name):
    for n, _, body_start, end in sections(text):
        if n == name:
            return text[body_start:end].strip()
    raise SystemExit(f"changelog: no [{name}] section")


def cut(version, date):
    text = PATH.read_text(encoding="utf-8")
    notes = body(text, "Unreleased")
    if not notes:
        raise SystemExit("changelog: Unreleased is empty; add the release notes there first")
    if f"## [{version}]" in text:
        raise SystemExit(f"changelog: [{version}] already exists")
    unreleased = next(s for s in sections(text) if s[0] == "Unreleased")
    text = (text[:unreleased[1]] + f"## [Unreleased]\n\n## [{version}] - {date}\n\n{notes}\n\n"
            + text[unreleased[3]:].lstrip("\n"))

    # Compare links: Unreleased against the new tag, the new version against the previous one.
    released = [n for n, *_ in sections(text) if n != "Unreleased"]
    links = [f"[Unreleased]: {REPO}/compare/v{version}...HEAD"]
    for i, name in enumerate(released):
        older = released[i + 1] if i + 1 < len(released) else None
        links.append(f"[{name}]: {REPO}/compare/v{older}...v{name}" if older else f"[{name}]: {REPO}/releases/tag/v{name}")
    text = LINKS.sub("", text).rstrip() + "\n\n" + "\n".join(links) + "\n"
    PATH.write_text(text, encoding="utf-8")


def main(argv):
    if len(argv) >= 2 and argv[0] == "cut":
        cut(argv[1], argv[2] if len(argv) > 2 else datetime.date.today().isoformat())
    elif len(argv) == 2 and argv[0] == "notes":
        print(body(PATH.read_text(encoding="utf-8"), argv[1]))
    elif argv == ["check"]:
        if not body(PATH.read_text(encoding="utf-8"), "Unreleased"):
            raise SystemExit("changelog: Unreleased is empty; add the release notes there first")
    else:
        raise SystemExit(__doc__)


if __name__ == "__main__":
    main(sys.argv[1:])
