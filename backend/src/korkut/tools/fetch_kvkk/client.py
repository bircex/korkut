"""Fetch a KVKK board decision's full text as Markdown.

KVKK decisions are standalone HTML pages on kvkk.gov.tr; the `decision_url` comes from a
search_kvkk result's `url`. The decision body lives in a ``<div class="blog-post-inner">``,
which is extracted and converted to Markdown. No API key (direct HTTP).
"""

from __future__ import annotations

import re
from html import unescape
from typing import Any

import httpx

from korkut.tools._base.client import BaseClient, chunk_markdown, extract_div_html, html_to_markdown

_TITLE_RE = re.compile(r'<h3[^>]*class="[^"]*blog-post-title[^"]*"[^>]*>(.*?)</h3>', re.S | re.I)
_DOC_TITLE_RE = re.compile(r"<title[^>]*>(.*?)</title>", re.S | re.I)
_TAG_RE = re.compile(r"<[^>]+>")
_DATE_RE = re.compile(r"Karar Tarih[ıi][^\n]*?(\d{1,2}[./]\d{1,2}[./]\d{4})", re.I)
_NUMBER_RE = re.compile(r"Karar No[^\n]*?(\d{4}/\d+)", re.I)


class KvkkDocClient(BaseClient):
    base_url = ""

    def __init__(self, *, timeout: float = 60.0, **kwargs: Any) -> None:
        super().__init__(timeout=timeout, **kwargs)
        self._client.close()
        self._client = httpx.Client(
            base_url=self.base_url,
            headers=self.default_headers,
            timeout=timeout,
            follow_redirects=True,
        )


def _text(html: str) -> str:
    return unescape(_TAG_RE.sub(" ", html)).strip()


def run_tool(decision_url: str, page: int = 1, page_size: int = 5000) -> dict[str, Any]:
    """Fetch a KVKK board decision as Markdown.

    decision_url : the full decision URL from a search_kvkk result (`url`).
    page         : 1-indexed page of the chunked Markdown (decisions can be long).
    page_size    : characters per page (default 5000).

    Returns {"sourceUrl", "title", "date", "decisionNumber", "markdown", "page",
    "total_pages"}.
    """
    if not decision_url or not decision_url.startswith("http"):
        raise ValueError("decision_url must be a full URL from a search_kvkk result")

    with KvkkDocClient() as client:
        raw, _ = client.get_bytes(decision_url)
    html = raw.decode("utf-8", errors="replace")

    title_match = _TITLE_RE.search(html) or _DOC_TITLE_RE.search(html)
    title = _text(title_match.group(1)) if title_match else None

    body_html = extract_div_html(html, "blog-post-inner")
    markdown = html_to_markdown(body_html)

    date = _DATE_RE.search(markdown)
    number = _NUMBER_RE.search(markdown)

    chunk = chunk_markdown(markdown, page=page, page_size=page_size)
    return {
        "sourceUrl": decision_url,
        "title": title,
        "date": date.group(1) if date else None,
        "decisionNumber": number.group(1) if number else None,
        "markdown": chunk["markdown"],
        "page": chunk["page"],
        "total_pages": chunk["total_pages"],
    }
