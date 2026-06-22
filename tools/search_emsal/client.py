"""Search UYAP Emsal (local civil + civil appellate precedent) decisions via Bedesten."""

from __future__ import annotations

from typing import Any

from tools._base.bedesten import BedestenClient


def run_tool(
    query: str,
    limit: int = 10,
    page: int = 1,
    date_start: str | None = None,
    date_end: str | None = None,
) -> dict[str, Any]:
    """Search local-court and civil-appeals precedent (emsal) decisions.

    Covers itemTypeList = [YERELHUKUK, ISTINAFHUKUK]. See search_yargitay for shared
    parameter semantics.
    """
    with BedestenClient() as client:
        return client.search(
            ["YERELHUKUK", "ISTINAFHUKUK"],
            query,
            limit=limit,
            page=page,
            date_start=date_start,
            date_end=date_end,
        )
