"""Minimal .env loader (no dependency) so the CLI and bridge pick up keys from ./.env."""

from __future__ import annotations

import os

from korkut.paths import ENV_FILE

_ENV = ENV_FILE


def load_dotenv() -> None:
    """Load KEY=VALUE lines from ./.env into os.environ (does not override existing)."""
    if not _ENV.exists():
        return
    for line in _ENV.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        key, value = key.strip(), value.strip().strip('"').strip("'")
        if key and key not in os.environ:
            os.environ[key] = value
