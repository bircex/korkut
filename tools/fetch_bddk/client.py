"""Fetch a BDDK decision document's full text as Markdown.

The `document_id` comes from a search_bddk result. BDDK serves documents under a few different
URL shapes (PDF or HTML); this tries each until one responds, converts by content type, and
paginates the result. No API key (direct HTTP).
"""

from __future__ import annotations

from typing import Any

import httpx

from tools._base.client import (
    BaseClient,
    chunk_markdown,
    html_to_markdown,
    legacy_ssl_context,
    pdf_to_markdown,
)

_URL_TEMPLATES = (
    "https://www.bddk.org.tr/Mevzuat/DokumanGetir/{id}",
    "https://www.bddk.org.tr/Mevzuat/Liste/{id}",
    "https://www.bddk.org.tr/KurumHakkinda/EkGetir/13?ekId={id}",
    "https://www.bddk.org.tr/KurumHakkinda/EkGetir/5?ekId={id}",
)


class BddkDocClient(BaseClient):
    base_url = ""

    def __init__(self, *, timeout: float = 60.0, **kwargs: Any) -> None:
        super().__init__(timeout=timeout, **kwargs)
        self._client.close()
        self._client = httpx.Client(
            base_url=self.base_url,
            headers=self.default_headers,
            timeout=timeout,
            verify=legacy_ssl_context(),
            follow_redirects=True,
        )


def run_tool(document_id: str, page: int = 1, page_size: int = 5000) -> dict[str, Any]:
    """Fetch a BDDK decision document as Markdown.

    document_id : the id from a search_bddk result (`id`).
    page        : 1-indexed page of the chunked Markdown.
    page_size   : characters per page (default 5000).

    Returns {"documentId", "sourceUrl", "markdown", "page", "total_pages"}.
    """
    if not document_id:
        raise ValueError("document_id is required")

    with BddkDocClient() as client:
        raw: bytes | None = None
        content_type = ""
        source_url = ""
        for template in _URL_TEMPLATES:
            url = template.format(id=document_id)
            try:
                raw, content_type = client.get_bytes(url)
            except httpx.HTTPError:
                continue
            source_url = url
            break

    if raw is None:
        raise ValueError(f"could not fetch BDDK document {document_id} from any known URL shape")

    if "pdf" in content_type.lower():
        markdown = pdf_to_markdown(raw)
    else:
        markdown = html_to_markdown(raw.decode("utf-8", errors="replace"))

    chunk = chunk_markdown(markdown, page=page, page_size=page_size)
    return {
        "documentId": document_id,
        "sourceUrl": source_url,
        "markdown": chunk["markdown"],
        "page": chunk["page"],
        "total_pages": chunk["total_pages"],
    }
