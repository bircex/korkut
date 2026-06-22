"""Search Yargıtay (Court of Cassation) decisions via the Bedesten API."""

from __future__ import annotations

from typing import Any

from tools._base.bedesten import BedestenClient


def run_tool(
    query: str,
    limit: int = 10,
    page: int = 1,
    chamber: str = "",
    date_start: str | None = None,
    date_end: str | None = None,
) -> dict[str, Any]:
    """Search Yargıtay decisions.

    query       : search phrase ("exact phrase", +required, -exclude, AND/OR/NOT)
    limit        : results per page (1-10)
    page         : 1-indexed page
    chamber      : optional chamber name, e.g. "1. Hukuk Dairesi"
    date_start   : optional YYYY-MM-DD (or full ISO 8601)
    date_end     : optional YYYY-MM-DD (or full ISO 8601)
    """
    with BedestenClient() as client:
        return client.search(
            ["YARGITAYKARARI"],
            query,
            limit=limit,
            page=page,
            birim=chamber,
            date_start=date_start,
            date_end=date_end,
        )
