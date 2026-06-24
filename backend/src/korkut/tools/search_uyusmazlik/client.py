"""Search Uyuşmazlık Mahkemesi (Court of Jurisdictional Disputes) decisions."""

from __future__ import annotations

from typing import Any

from korkut.tools._base.uyusmazlik import UyusmazlikClient


def run_tool(
    query: str,
    scope: str = "all",
    limit: int = 10,
    page: int = 1,
    case_sensitive: bool = False,
) -> dict[str, Any]:
    """Search Court of Jurisdictional Disputes decisions.

    query          : search text (required).
    scope          : where to match — "all" (default, full content), "esas"
                     (case number), "karar" (decision number).
    limit          : results to return from the page (the portal serves 20/page).
    page           : 1-indexed page (server-side paging is walked to this page).
    case_sensitive : match case exactly when true.

    Returns {"total", "page", "count", "results"} where each result is
    {id, esasNo, kararNo, date, url}. `url` is the decision PDF — pass it to
    fetch_uyusmazlik for the full text.
    """
    if not query:
        raise ValueError("query is required")
    with UyusmazlikClient() as client:
        return client.search(
            query,
            scope=scope,
            limit=limit,
            page=page,
            case_sensitive=case_sensitive,
        )
