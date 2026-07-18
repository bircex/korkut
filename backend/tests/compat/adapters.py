"""KDM-003 — runtime adapters for the differential harness.

A *fixture* (a request + the context needed to authenticate it) is fed to a
``RuntimeAdapter`` which executes it and returns a raw result. The same fixture
must run against:

* ``PythonAdapter`` — the current FastAPI app in-process (live today), and
* ``DotNetAdapter`` — the ASP.NET Core port over HTTP (activated at KDM-034 by
  setting ``KORKUT_DOTNET_BASE_URL``; until then it is explicitly unavailable,
  never a silent pass).

Both return ``(status, headers, json_or_none)`` so ``normalize.normalize_result``
can canonicalize them identically.
"""

from __future__ import annotations

import importlib
import os
from pathlib import Path
from typing import Any, Protocol
from unittest.mock import patch


class RuntimeAdapter(Protocol):
    name: str

    def available(self) -> bool: ...

    def request(
        self, method: str, path: str, *, cookie: str | None, json_body: Any | None
    ) -> tuple[int, dict[str, str], Any]: ...


class PythonAdapter:
    """Drives the current Python FastAPI app in-process over a throwaway SQLite DB.

    Seeds a deterministic two-office dataset (reusing the golden-smoke seed) so
    fixtures can reference stable logical handles (``{A_matter}`` etc.) that the
    harness resolves to concrete ids before the request.
    """

    name = "python"

    def __init__(self, tmp_db: Path):
        # Reuse the golden-smoke bootstrap so the two harnesses agree on seed data.
        from tests.baseline import golden_smoke as gs

        self._gs = gs
        self.app_mod, self.ctx = gs.build_app(tmp_db)

    def available(self) -> bool:
        return True

    def resolve(self, path: str) -> str:
        a, b = self.ctx["a"], self.ctx["b"]
        return path.replace("{A_matter}", str(a["matter_id"])).replace(
            "{B_matter}", str(b["matter_id"])
        )

    def cookie_for(self, principal: str | None) -> str | None:
        if principal in (None, "anonymous"):
            return None
        return self.ctx[principal]["cookie"]

    def request(
        self, method: str, path: str, *, cookie: str | None, json_body: Any | None
    ) -> tuple[int, dict[str, str], Any]:
        from tests.baseline import golden_smoke as gs

        client = gs._client(self.app_mod, cookie)
        with patch("korkut.brain.agent.answer", return_value="x"):
            resp = client.request(method, self.resolve(path), json=json_body)
        try:
            body = resp.json()
        except ValueError:
            body = None
        return resp.status_code, dict(resp.headers), body


class DotNetAdapter:
    """HTTP client for the ASP.NET Core port. Inert until KDM-034 wires a base URL.

    ``available()`` is False unless ``KORKUT_DOTNET_BASE_URL`` is set, so the
    cross-runtime comparison is skipped *with an explicit reason* (the .NET
    runtime does not exist yet — it is built in KDM-005+). It never fabricates a
    pass.
    """

    name = "dotnet"

    def __init__(self) -> None:
        self.base_url = os.environ.get("KORKUT_DOTNET_BASE_URL", "").rstrip("/")

    def available(self) -> bool:
        return bool(self.base_url)

    def request(
        self, method: str, path: str, *, cookie: str | None, json_body: Any | None
    ) -> tuple[int, dict[str, str], Any]:  # pragma: no cover - activated at KDM-034
        import httpx

        headers = {}
        if cookie is not None:
            from korkut.api import auth

            headers["Cookie"] = f"{auth.COOKIE_NAME}={cookie}"
        resp = httpx.request(method, f"{self.base_url}{path}", headers=headers, json=json_body)
        try:
            body = resp.json()
        except ValueError:
            body = None
        return resp.status_code, dict(resp.headers), body


def build_python_adapter(tmp_db: Path) -> PythonAdapter:
    return PythonAdapter(tmp_db)


def build_dotnet_adapter() -> DotNetAdapter:
    return DotNetAdapter()


# Re-export so importlib users don't accidentally cache a stale module ref.
__all__ = [
    "DotNetAdapter",
    "PythonAdapter",
    "RuntimeAdapter",
    "build_dotnet_adapter",
    "build_python_adapter",
    "importlib",
]
