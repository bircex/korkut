"""Fetch a Sigorta Tahkim Komisyonu journal issue as Markdown.

The `issue_number` comes from a search_sigorta_tahkim result (`id`). Journals are large, so the
converted text is paginated. No API key (direct PDF download).
"""

from __future__ import annotations

from typing import Any

from korkut.tools._base.client import chunk_markdown
from korkut.tools._base.sigorta_tahkim import SigortaTahkimClient


def run_tool(issue_number: str, page: int = 1, page_size: int = 5000) -> dict[str, Any]:
    """Fetch an Insurance Arbitration Commission journal issue as Markdown.

    issue_number : the journal issue number from a search_sigorta_tahkim result (`id`).
    page         : 1-indexed page of the chunked Markdown.
    page_size    : characters per page (default 5000).

    Returns {"issueNumber", "sourceUrl", "markdown", "page", "total_pages"}.
    """
    if issue_number in (None, ""):
        raise ValueError("issue_number is required")

    with SigortaTahkimClient() as client:
        markdown, pdf_url = client.download_issue_markdown(str(issue_number))

    chunk = chunk_markdown(markdown, page=page, page_size=page_size)
    return {
        "issueNumber": str(issue_number),
        "sourceUrl": pdf_url,
        "markdown": chunk["markdown"],
        "page": chunk["page"],
        "total_pages": chunk["total_pages"],
    }
