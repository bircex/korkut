"""Single source of truth for runtime asset locations.

The Korkut package lives under ``backend/src/korkut`` but its runtime assets — `config/`,
`templates/`, the SQLite `data/` dir and the local `.env` — sit at the repository root so they
can be edited without touching the package. This module resolves the project root once and
exports the asset paths, so no other module has to guess with fragile ``parent.parent`` walks.

Resolution order:
1. ``KORKUT_ROOT`` environment variable (explicit override; useful for installed wheels/Docker).
2. Walk up from this file until a directory holding both ``config/`` and ``templates/`` is found.
3. Fall back to the repo root relative to this file (backend/src/korkut/paths.py → up 3).
"""

from __future__ import annotations

import os
from pathlib import Path


def _find_root() -> Path:
    env = os.getenv("KORKUT_ROOT")
    if env:
        return Path(env).resolve()
    here = Path(__file__).resolve()
    for parent in here.parents:
        if (parent / "config").is_dir() and (parent / "templates").is_dir():
            return parent
    return here.parents[3]


PROJECT_ROOT = _find_root()
CONFIG_DIR = PROJECT_ROOT / "config"
TEMPLATES_DIR = PROJECT_ROOT / "templates"
DATA_DIR = PROJECT_ROOT / "data"
ENV_FILE = PROJECT_ROOT / ".env"
