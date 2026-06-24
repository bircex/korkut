"""Search BDDK (Bankacılık Düzenleme ve Denetleme Kurumu / Banking Regulation Authority) decisions.

BDDK publishes its decisions as documents on ``bddk.org.tr`` with no searchable API. Following
the upstream yargi-mcp approach, this tool discovers them via the Tavily Search API restricted to
the BDDK document domain, and exposes each document's id (use fetch_bddk for the full text).

Requires a Tavily API key in ``TAVILY_API_KEY``; the registry hides this tool when it is unset.
"""

from __future__ import annotations

import re
from typing import Any

from tools._base.search_providers import TavilyClient

_DOC_ID_RES = (
    re.compile(r"/DokumanGetir/(\d+)"),
    re.compile(r"/Liste/(\d+)"),
    re.compile(r"ekId=(\d+)"),
)


def _document_id(url: str) -> str | None:
    for pattern in _DOC_ID_RES:
        match = pattern.search(url)
        if match:
            return match.group(1)
    return None


def run_tool(query: str, limit: int = 10, page: int = 1) -> dict[str, Any]:
    """Search BDDK decisions.

    query  : free-text keywords (Turkish).
    limit  : maximum results to return (Tavily has no real pagination; pages > 1 may be empty).
    page   : 1-indexed page number (kept for envelope consistency).

    Returns {"total", "page", "count", "results"} where each result is
    {id, title, content, url}. Pass `id` to fetch_bddk.
    """
    limit = max(1, min(limit, 20))
    page = max(1, page)
    tavily_query = f'{query} "Karar Sayısı"'.strip()

    with TavilyClient() as client:
        body = client.search(
            tavily_query,
            include_domains=["https://www.bddk.org.tr/Mevzuat/DokumanGetir"],
            max_results=limit,
        )

    results: list[dict[str, Any]] = []
    for item in body.get("results") or []:
        url = item.get("url") or ""
        doc_id = _document_id(url)
        if not doc_id:
            continue
        results.append(
            {
                "id": doc_id,
                "title": (item.get("title") or "").replace("[PDF] ", "").strip(),
                "content": (item.get("content") or "")[:500],
                "url": url,
            }
        )

    return {"total": len(results), "page": page, "count": len(results), "results": results}
