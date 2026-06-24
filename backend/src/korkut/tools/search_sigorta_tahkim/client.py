"""Search Sigorta Tahkim Komisyonu (Insurance Arbitration Commission) decisions.

The commission publishes quarterly PDF journals on ``sigortatahkim.org`` with no searchable API.
Following the upstream yargi-mcp approach, this tool discovers relevant journal issues via the
Tavily Search API restricted to the commission domain. Each result carries a journal issue number
(`id`); pass it to fetch_sigorta_tahkim to read the issue, or to search_within_sigorta_tahkim to
find a keyword inside that issue's decisions.

Requires a Tavily API key in ``TAVILY_API_KEY``; the registry hides this tool when it is unset.
"""

from __future__ import annotations

import re
from typing import Any

from korkut.tools._base.search_providers import TavilyClient

_ISSUE_RES = (
    re.compile(r"karardrgs(\d+)\.pdf", re.I),
    re.compile(r"revizekd(\d+)\.pdf", re.I),
    re.compile(r"karardergisisayi(\d+)\.pdf", re.I),
    re.compile(r"say[ıi]\s*[-:]?\s*(\d+)", re.I),
)


def _issue_number(url: str) -> str | None:
    for pattern in _ISSUE_RES:
        match = pattern.search(url)
        if match:
            return match.group(1)
    return None


def run_tool(query: str, limit: int = 10, page: int = 1) -> dict[str, Any]:
    """Search Insurance Arbitration Commission journal issues.

    query  : free-text keywords (Turkish).
    limit  : maximum results (Tavily has no real pagination; pages > 1 may be empty).
    page   : 1-indexed page number (kept for envelope consistency).

    Returns {"total", "page", "count", "results"} where each result is
    {id, title, content, url}. `id` is the journal issue number — pass it to
    fetch_sigorta_tahkim or search_within_sigorta_tahkim.
    """
    limit = max(1, min(limit, 20))
    page = max(1, page)

    with TavilyClient() as client:
        body = client.search(
            query,
            include_domains=["sigortatahkim.org"],
            max_results=limit,
        )

    results: list[dict[str, Any]] = []
    for item in body.get("results") or []:
        url = item.get("url") or ""
        issue = _issue_number(url)
        results.append(
            {
                "id": issue or url,
                "title": (item.get("title") or "").strip(),
                "content": (item.get("content") or "")[:500],
                "url": url,
            }
        )

    return {"total": len(results), "page": page, "count": len(results), "results": results}
