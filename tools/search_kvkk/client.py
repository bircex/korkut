"""Search KVKK (Kişisel Verileri Koruma Kurumu / Personal Data Protection Authority) decisions.

KVKK publishes its board decision summaries ("karar özeti") as ordinary web pages on
``kvkk.gov.tr`` with no searchable API. This tool discovers them via the Tavily Search API
restricted to the KVKK domain — the same single provider used for BDDK and Sigorta Tahkim, so the
whole toolset needs only one key — then exposes the decision URL (use fetch_kvkk for full text).

Requires a Tavily API key in ``TAVILY_API_KEY``; the registry hides this tool when it is unset.
"""

from __future__ import annotations

import re
from typing import Any
from urllib.parse import urlparse

from tools._base.search_providers import TavilyClient

_DATE_RE = re.compile(r"(\d{1,2}/\d{1,2}/\d{4})")
_NUMBER_RE = re.compile(r"(\d{4}/\d+)")


def _decision_id(url: str) -> str | None:
    """Pull the decision id out of a KVKK URL, e.g. /Icerik/7288/2021-1303 -> 7288/2021-1303."""
    parts = urlparse(url).path.strip("/").split("/")
    if len(parts) >= 3 and parts[0].lower() == "icerik":
        return "/".join(parts[1:])
    return None


def run_tool(query: str, limit: int = 10, page: int = 1) -> dict[str, Any]:
    """Search KVKK board decision summaries.

    query  : free-text keywords (Turkish).
    limit  : maximum results (Tavily has no real pagination; pages > 1 may be empty).
    page   : 1-indexed page number (kept for envelope consistency).

    Returns {"total", "page", "count", "results"} where each result is
    {id, title, description, date, decisionNumber, url}. Pass `url` to fetch_kvkk.
    """
    limit = max(1, min(limit, 20))
    page = max(1, page)
    tavily_query = f'{query} "karar özeti"'.strip()

    with TavilyClient() as client:
        body = client.search(
            tavily_query,
            include_domains=["kvkk.gov.tr"],
            max_results=limit,
        )

    results: list[dict[str, Any]] = []
    for item in body.get("results") or []:
        title = item.get("title") or ""
        url = item.get("url") or ""
        date = _DATE_RE.search(title)
        number = _NUMBER_RE.search(title)
        results.append(
            {
                "id": _decision_id(url),
                "title": title,
                "description": (item.get("content") or "")[:500],
                "date": date.group(1) if date else None,
                "decisionNumber": number.group(1) if number else None,
                "url": url,
            }
        )

    return {"total": len(results), "page": page, "count": len(results), "results": results}
