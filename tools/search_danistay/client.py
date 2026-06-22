"""Search Danıştay (Council of State) decisions via the Bedesten API."""

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
    """Search Danıştay decisions. See search_yargitay for parameter semantics."""
    with BedestenClient() as client:
        return client.search(
            ["DANISTAYKARAR"],
            query,
            limit=limit,
            page=page,
            birim=chamber,
            date_start=date_start,
            date_end=date_end,
        )
