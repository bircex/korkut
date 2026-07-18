"""KDM-003 — versioned OpenAPI contract snapshot.

Extracts the live FastAPI OpenAPI document and writes a **deterministic**,
secret-free snapshot (`contracts/openapi.json`). The paired test fails if the
live contract drifts from the snapshot, so an API-surface change cannot pass CI
without an intentional, reviewed snapshot update:

    KORKUT_CONTRACT_UPDATE=1 ../.venv/bin/python -m tests.compat.openapi_snapshot

The .NET port (KDM-006/KDM-034) must serve an OpenAPI document that reduces to
the same snapshot (modulo allowlisted legacy diffs).
"""

from __future__ import annotations

import importlib
import json
import os
from pathlib import Path
from typing import Any

CONTRACTS_DIR = Path(__file__).parent / "contracts"
OPENAPI_PATH = CONTRACTS_DIR / "openapi.json"


def live_openapi() -> dict[str, Any]:
    os.environ.setdefault("KORKUT_DB_URL", "sqlite+pysqlite:///:memory:")
    os.environ.setdefault("KORKUT_SECRET", "test-secret-stable-0123456789abcdef")
    import korkut.api.store as store

    importlib.reload(store)
    import korkut.api.app as app_mod

    importlib.reload(app_mod)
    return app_mod.app.openapi()


def normalize_openapi(spec: dict[str, Any]) -> dict[str, Any]:
    """Keep the contract-relevant, stable parts of the OpenAPI document.

    Drops the free-text ``info.version``/``description`` churn but keeps paths,
    operations, parameters, request/response schemas and component schemas — the
    parts a re-implementation must match.
    """
    return {
        "openapi": spec.get("openapi"),
        "paths": _sorted(spec.get("paths", {})),
        "components": {"schemas": _sorted(spec.get("components", {}).get("schemas", {}))},
    }


def _sorted(obj: Any) -> Any:
    if isinstance(obj, dict):
        return {k: _sorted(obj[k]) for k in sorted(obj)}
    if isinstance(obj, list):
        return [_sorted(v) for v in obj]
    return obj


def snapshot() -> dict[str, Any]:
    return normalize_openapi(live_openapi())


def load_snapshot() -> dict[str, Any]:
    return json.loads(OPENAPI_PATH.read_text())


def write_snapshot(data: dict[str, Any]) -> None:
    CONTRACTS_DIR.mkdir(exist_ok=True)
    OPENAPI_PATH.write_text(json.dumps(data, indent=2, ensure_ascii=False, sort_keys=True) + "\n")


if __name__ == "__main__":
    data = snapshot()
    if os.environ.get("KORKUT_CONTRACT_UPDATE") == "1":
        write_snapshot(data)
        print(
            f"wrote {OPENAPI_PATH} ({len(data['paths'])} paths, "
            f"{len(data['components']['schemas'])} schemas)"
        )
    else:
        print(json.dumps(data, indent=2, ensure_ascii=False, sort_keys=True))
