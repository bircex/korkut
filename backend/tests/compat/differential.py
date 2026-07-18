"""KDM-003 — differential runner.

Loads the deterministic fixtures, runs each through a ``RuntimeAdapter``, and
returns the normalized result keyed by fixture id. Used two ways:

1. **Recorded parity** (live today): run the Python adapter and compare to the
   committed ``expected_python.json`` — this is the contract the .NET port must
   reproduce, and it guards against accidental Python-side drift.
2. **Cross-runtime parity** (KDM-034): run both adapters and compare, honouring
   the legacy-diff allowlist for known, owned, expiring differences.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from tests.compat import normalize
from tests.compat.adapters import PythonAdapter, RuntimeAdapter

HERE = Path(__file__).parent
FIXTURES_PATH = HERE / "fixtures.json"
EXPECTED_PYTHON_PATH = HERE / "expected_python.json"


def load_fixtures() -> list[dict[str, Any]]:
    data = json.loads(FIXTURES_PATH.read_text())
    return data["fixtures"]


def run_adapter(adapter: RuntimeAdapter, fixtures: list[dict[str, Any]]) -> dict[str, Any]:
    """Execute every fixture against one adapter; return normalized results."""
    results: dict[str, Any] = {}
    for fx in fixtures:
        cookie = _cookie_for(adapter, fx["principal"])
        status, headers, body = adapter.request(
            fx["method"], fx["path"], cookie=cookie, json_body=fx.get("body")
        )
        results[fx["id"]] = normalize.normalize_result(
            status=status,
            headers=headers,
            body=body,
            capture_headers=fx.get("capture_headers"),
        )
    return results


def _cookie_for(adapter: RuntimeAdapter, principal: str | None) -> str | None:
    # PythonAdapter resolves seeded principals to session cookies; other adapters
    # (dotnet over HTTP) must expose the same cookie_for hook when activated.
    resolver = getattr(adapter, "cookie_for", None)
    if resolver is not None:
        return resolver(principal)
    return None


def run_python(tmp_db: Path) -> dict[str, Any]:
    adapter = PythonAdapter(tmp_db)
    return run_adapter(adapter, load_fixtures())


def load_expected_python() -> dict[str, Any]:
    return json.loads(EXPECTED_PYTHON_PATH.read_text())


def write_expected_python(results: dict[str, Any]) -> None:
    EXPECTED_PYTHON_PATH.write_text(
        json.dumps(results, indent=2, ensure_ascii=False, sort_keys=True) + "\n"
    )


def diff_results(left: dict[str, Any], right: dict[str, Any]) -> dict[str, Any]:
    """Return per-fixture mismatches between two normalized result sets."""
    mismatches: dict[str, Any] = {}
    for fid in sorted(set(left) | set(right)):
        lv, rv = left.get(fid), right.get(fid)
        if lv != rv:
            mismatches[fid] = {"left": lv, "right": rv}
    return mismatches


if __name__ == "__main__":
    import os
    import tempfile

    with tempfile.TemporaryDirectory() as d:
        res = run_python(Path(d) / "diff.db")
    if os.environ.get("KORKUT_CONTRACT_UPDATE") == "1":
        write_expected_python(res)
        print(f"wrote {EXPECTED_PYTHON_PATH} ({len(res)} fixtures)")
    else:
        print(json.dumps(res, indent=2, ensure_ascii=False, sort_keys=True))
