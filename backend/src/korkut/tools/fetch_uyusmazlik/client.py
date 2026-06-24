"""Fetch a Uyuşmazlık Mahkemesi decision's full text as Markdown.

Unlike Bedesten decisions (fetched by documentId via fetch_decision), Uyuşmazlık
decisions live at standalone HTML pages; the `document_url` comes from a
search_uyusmazlik result's `url` field.
"""

from __future__ import annotations

from typing import Any

from korkut.tools._base.uyusmazlik import BASE_URL, UyusmazlikClient


def run_tool(document_url: str) -> dict[str, Any]:
    """Fetch a Court of Jurisdictional Disputes decision as Markdown.

    document_url : the full decision URL from a search_uyusmazlik result (`url`).

    Returns {"sourceUrl", "markdown"}.
    """
    if not document_url:
        raise ValueError("document_url is required")
    if not document_url.startswith("http"):
        raise ValueError("document_url must be a full URL from a search_uyusmazlik result")
    if BASE_URL not in document_url:
        raise ValueError(
            f"document_url must be a kararlar.uyusmazlik.gov.tr URL, got {document_url!r}"
        )
    with UyusmazlikClient() as client:
        return client.document_markdown(document_url)
