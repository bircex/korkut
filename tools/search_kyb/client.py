"""Search Kanun Yararına Bozma (extraordinary appeal) decisions via the Bedesten API."""

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
    """Search Kanun Yararına Bozma (KYB) decisions.

    KYB is the extraordinary appeal the Chief Public Prosecutor's Office files
    against finalised decisions in the interest of law. Covers itemTypeList = [KYB].
    See search_yargitay for shared parameter semantics. Use fetch_decision with a
    documentId for full text.
    """
    with BedestenClient() as client:
        return client.search(
            ["KYB"],
            query,
            limit=limit,
            page=page,
            date_start=date_start,
            date_end=date_end,
        )
