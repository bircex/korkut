"""KDM-002 — Python runtime golden smoke harness.

Freezes the *observable behavior* of the current Python API for the critical
office user flows, so the .NET port (KDM-005+) can be checked for parity against
a recorded, repeatable source of truth.

Design goals
------------
* **Repeatable**: deterministic seed (fixed offices/users/matters), in-process
  FastAPI TestClient over a throwaway SQLite DB and a fixed test secret. No
  network, no external services — mirrors the existing HTTP tests
  (``tests/test_http_tenant_isolation.py``).
* **Stable golden**: we record the HTTP *status* plus a normalized *shape* of
  each response (dict keys + value type tokens, list element shape), not the
  volatile values (ids, timestamps, secrets). Shapes are what a re-implementation
  must reproduce; exact ids would make the golden brittle and could leak data.
* **No secrets**: only a throwaway test secret is used; nothing from the real
  ``.env`` is read or recorded.

This module is imported by ``test_golden_smoke.py`` (pytest) and is also runnable
as a script to (re)generate the golden file:

    KORKUT_BASELINE_UPDATE=1 ../.venv/bin/python -m tests.baseline.golden_smoke
"""

from __future__ import annotations

import importlib
import json
import os
from pathlib import Path
from typing import Any
from unittest.mock import patch

from fastapi.testclient import TestClient

GOLDEN_PATH = Path(__file__).with_name("golden_smoke.json")


def _shape(value: Any) -> Any:
    """Reduce a JSON value to a stable structural signature.

    Scalars collapse to a type token; dicts keep their keys (values recursed);
    lists collapse to ``[<shape-of-first>, "...xN"]`` so length varies without
    changing the signature. This is what parity is asserted on.
    """
    if isinstance(value, bool):
        return "bool"
    if value is None:
        return "null"
    if isinstance(value, int):
        return "int"
    if isinstance(value, float):
        return "float"
    if isinstance(value, str):
        return "str"
    if isinstance(value, dict):
        return {k: _shape(v) for k, v in sorted(value.items())}
    if isinstance(value, list):
        if not value:
            return ["<empty>"]
        return [_shape(value[0]), f"...x{len(value)}"]
    return type(value).__name__


def build_app(tmp_db: Path):
    """Boot the app against a throwaway SQLite DB + fixed secret and seed data.

    Returns ``(app_module, ctx)`` where ctx holds cookies/ids for two offices.
    Mirrors the module-reload dance the existing HTTP tests use so the tenant
    middleware, auth secret and store all bind to the test DB.
    """
    os.environ["KORKUT_DB_URL"] = f"sqlite+pysqlite:///{tmp_db}"
    os.environ["KORKUT_SECRET"] = "test-secret-stable-0123456789abcdef"

    import korkut.api.auth as auth_mod
    import korkut.api.session as session_mod
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    auth_mod._secret_cache = None
    import korkut.api.app as app_mod

    importlib.reload(app_mod)

    from korkut.api.models import Matter, Office, User
    from korkut.core.tenancy import reset_current_office, set_current_office

    def seed(name: str, matters: int) -> dict:
        with store_mod.session() as s:
            office = Office(name=name)
            s.add(office)
            s.commit()
            oid = office.id
        tok = set_current_office(oid)
        try:
            with store_mod.session() as s:
                u = User(
                    username=f"{name.lower()}-owner",
                    email=f"{name.lower()}@example.test",
                    role="owner",
                )
                s.add(u)
                s.commit()
                uid, epoch = u.id, u.token_epoch
                for i in range(matters):
                    s.add(Matter(title=f"{name}-matter-{i}", created_by=uid))
                s.commit()
                first_mid = s.scalars(store_mod.select(Matter)).first().id
        finally:
            reset_current_office(tok)
        cookie = session_mod.mint(uid, oid, "owner", epoch)
        return {"office_id": oid, "user_id": uid, "matter_id": first_mid, "cookie": cookie}

    a = seed("A", matters=1)
    b = seed("B", matters=2)
    return app_mod, {"a": a, "b": b}


def _client(app_mod, cookie: str | None) -> TestClient:
    from korkut.api import auth

    c = TestClient(app_mod.app)
    if cookie is not None:
        c.cookies.set(auth.COOKIE_NAME, cookie)
    return c


def collect(app_mod, ctx: dict) -> dict:
    """Run the critical-flow request matrix and return the golden record."""
    a, b = ctx["a"], ctx["b"]

    # (case-id, method, path, cookie-or-None). Cases are ordered & deterministic.
    cases = [
        # Public bootstrap — no auth.
        ("public_config", "GET", "/api/config", None),
        # Unauthenticated access to protected reads must be rejected.
        ("unauth_me", "GET", "/api/me", None),
        ("unauth_matters", "GET", "/api/matters", None),
        # Authenticated office-A reads.
        ("auth_me", "GET", "/api/me", a["cookie"]),
        ("auth_matters_list", "GET", "/api/matters", a["cookie"]),
        ("auth_matter_get", "GET", f"/api/matters/{a['matter_id']}", a["cookie"]),
        ("auth_sessions", "GET", "/api/sessions", a["cookie"]),
        ("auth_clients", "GET", "/api/clients", a["cookie"]),
        ("auth_persona", "GET", "/api/persona", a["cookie"]),
        ("auth_style", "GET", "/api/style", a["cookie"]),
        ("auth_members", "GET", "/api/members", a["cookie"]),
        ("auth_calendar_upcoming", "GET", "/api/calendar/upcoming", a["cookie"]),
        # Cross-tenant IDOR: A asks for B's matter id -> must NOT be 200.
        ("idor_cross_office_matter", "GET", f"/api/matters/{b['matter_id']}", a["cookie"]),
        # Error behavior: missing resource, invalid body.
        ("error_matter_not_found", "GET", "/api/matters/999999", a["cookie"]),
        ("error_matter_bad_body", "POST", "/api/matters", a["cookie"]),
    ]

    record: dict[str, Any] = {}
    # Patch the brain so no LLM/provider is invoked by any incidental call.
    with patch("korkut.brain.agent.answer", return_value="x"):
        for cid, method, path, cookie in cases:
            client = _client(app_mod, cookie)
            if method == "GET":
                resp = client.get(path)
            elif method == "POST":
                # Intentionally-empty body to capture validation behavior.
                resp = client.post(path, json={})
            else:  # pragma: no cover - only GET/POST used here
                raise AssertionError(method)
            try:
                body_shape = _shape(resp.json())
            except ValueError:
                body_shape = f"<non-json:{len(resp.content)}b>"
            record[cid] = {
                "method": method,
                "path_template": path.replace(str(a["matter_id"]), "{A_matter}").replace(
                    str(b["matter_id"]), "{B_matter}"
                ),
                "status": resp.status_code,
                "shape": body_shape,
            }
    return record


def load_golden() -> dict:
    return json.loads(GOLDEN_PATH.read_text())


def write_golden(record: dict) -> None:
    GOLDEN_PATH.write_text(json.dumps(record, indent=2, ensure_ascii=False, sort_keys=True) + "\n")


def generate(tmp_db: Path) -> dict:
    app_mod, ctx = build_app(tmp_db)
    return collect(app_mod, ctx)


if __name__ == "__main__":
    import tempfile

    with tempfile.TemporaryDirectory() as d:
        rec = generate(Path(d) / "baseline.db")
    if os.environ.get("KORKUT_BASELINE_UPDATE") == "1":
        write_golden(rec)
        print(f"wrote {GOLDEN_PATH} ({len(rec)} cases)")
    else:
        print(json.dumps(rec, indent=2, ensure_ascii=False, sort_keys=True))
