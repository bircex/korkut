"""Fetch the full text of a piece of Turkish legislation (mevzuat) as Markdown.

The Ministry of Justice's UYAP Mevzuat service exposes a public, key-free JSON
endpoint at `bedesten.adalet.gov.tr/mevzuat/getDocumentContent` that returns a
document's body (base64-encoded HTML or PDF). The same endpoint serves either a
whole piece of legislation (`documentType: "MEVZUAT"`) or a single article
(`documentType: "MADDE"`), selected by the `id` field. This client maps our
English params onto that endpoint and normalises the result to stable keys.

The `legislation_id` comes from a `search_mevzuat` result (its `id` field); a
`article_id` (article id) can be obtained from the legislation's article tree.
"""

from __future__ import annotations

import base64
from typing import Any, ClassVar

from tools._base.client import BaseClient, html_to_markdown, pdf_to_markdown


class LegislationClient(BaseClient):
    """Thin client over the Bedesten mevzuat document-content endpoint."""

    base_url = "https://bedesten.adalet.gov.tr"
    default_headers: ClassVar[dict[str, str]] = {
        **BaseClient.default_headers,
        "AdaletApplicationName": "UyapMevzuat",
        "Content-Type": "application/json; charset=utf-8",
        "Origin": "https://mevzuat.adalet.gov.tr",
        "Referer": "https://mevzuat.adalet.gov.tr/",
    }
    DOCUMENT = "/mevzuat/getDocumentContent"

    def document_markdown(self, legislation_id: str, article_id: str = "") -> dict[str, Any]:
        """Fetch a whole piece of legislation, or a single article if `article_id`.

        Returns {"id", "title", "markdown", "sourceUrl"}. When `article_id` is
        given the article is fetched (documentType MADDE) and its id is the
        canonical document id; otherwise the full text is fetched (MEVZUAT).
        """
        if article_id:
            document_id, document_type = article_id, "MADDE"
        else:
            document_id, document_type = legislation_id, "MEVZUAT"

        body = self.post_json(
            self.DOCUMENT,
            {
                "data": {"documentType": document_type, "id": document_id},
                "applicationName": "UyapMevzuat",
            },
        )
        meta = (body or {}).get("metadata") or {}
        data = (body or {}).get("data") or {}
        if meta.get("FMTY") not in (None, "SUCCESS") and not data.get("content"):
            raise ValueError(meta.get("FMTE") or f"No content for document {document_id}")

        content_b64 = data.get("content")
        if not content_b64:
            raise ValueError(f"No content for document {document_id}")

        mime = data.get("mimeType") or data.get("mimetype") or "text/html"
        raw = base64.b64decode(content_b64)
        if "pdf" in mime.lower():
            markdown = pdf_to_markdown(raw)
        elif "html" in mime.lower():
            markdown = html_to_markdown(raw.decode("utf-8", errors="replace"))
        else:
            markdown = raw.decode("utf-8", errors="replace")

        title = data.get("mevzuatAdi") or data.get("maddeBaslik") or data.get("baslik") or ""
        result: dict[str, Any] = {
            "id": document_id,
            "markdown": markdown,
            "sourceUrl": f"https://mevzuat.adalet.gov.tr/mevzuat?mevzuatId={legislation_id}",
        }
        if title:
            result["title"] = title
        return result


def run_tool(legislation_id: str, article_id: str = "") -> dict[str, Any]:
    """Fetch the full text of Turkish legislation (or one article) as Markdown.

    legislation_id : the legislation id returned by `search_mevzuat` (its `id`).
    article_id   : optional article id; when given, only that article is fetched.
    """
    if not legislation_id:
        raise ValueError("legislation_id is required")
    with LegislationClient() as client:
        return client.document_markdown(legislation_id, article_id)
