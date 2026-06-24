"""Search for a keyword within a single Sigorta Tahkim journal issue.

Downloads the issue PDF (by number, from a search_sigorta_tahkim result), splits it into
individual arbitration decisions, and returns the decisions that mention the keyword, ranked by
match count with a short excerpt around the first hit. No API key (direct PDF download).
"""

from __future__ import annotations

from typing import Any

from korkut.tools._base.sigorta_tahkim import (
    SigortaTahkimClient,
    split_into_decisions,
    turkish_lower,
)

_EXCERPT_RADIUS = 200


def run_tool(issue_number: str, keyword: str, max_results: int = 10) -> dict[str, Any]:
    """Search for a keyword within one journal issue's decisions.

    issue_number : the journal issue number from a search_sigorta_tahkim result (`id`).
    keyword      : keyword or phrase to find (Turkish; case-insensitive, İ/I-aware).
    max_results  : maximum matching decisions to return.

    Returns {"issueNumber", "keyword", "totalDecisions", "matchingDecisions", "results"}
    where each result is {header, relevanceScore, excerpt, bodyLength}.
    """
    if issue_number in (None, ""):
        raise ValueError("issue_number is required")
    if not keyword:
        raise ValueError("keyword is required")

    with SigortaTahkimClient() as client:
        markdown, _ = client.download_issue_markdown(str(issue_number))

    decisions = split_into_decisions(markdown)
    keyword_lower = turkish_lower(keyword)

    matches: list[dict[str, Any]] = []
    for header, body in decisions:
        body_lower = turkish_lower(body)
        count = body_lower.count(keyword_lower)
        if count == 0:
            continue
        pos = body_lower.find(keyword_lower)
        start = max(0, pos - _EXCERPT_RADIUS)
        end = min(len(body), pos + len(keyword) + _EXCERPT_RADIUS)
        excerpt = body[start:end].strip()
        if start > 0:
            excerpt = "..." + excerpt
        if end < len(body):
            excerpt = excerpt + "..."
        matches.append(
            {
                "header": header,
                "relevanceScore": count,
                "excerpt": excerpt,
                "bodyLength": len(body),
            }
        )

    matches.sort(key=lambda m: m["relevanceScore"], reverse=True)
    matches = matches[: max(1, max_results)]
    return {
        "issueNumber": str(issue_number),
        "keyword": keyword,
        "totalDecisions": len(decisions),
        "matchingDecisions": len(matches),
        "results": matches,
    }
