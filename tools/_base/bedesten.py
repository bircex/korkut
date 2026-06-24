"""Client for the Bedesten API (bedesten.adalet.gov.tr).

The Ministry of Justice's unified decision service. One endpoint covers several
court types via `itemTypeList`: Yargıtay (Court of Cassation), Danıştay (Council
of State), local civil courts, civil courts of appeals, and extraordinary
appeals (KYB). Public; no auth. Shared by `search_yargitay`, `search_danistay`,
`search_emsal`, and `fetch_decision`.
"""

from __future__ import annotations

import base64
from typing import Any, ClassVar

from .client import BaseClient, html_to_markdown, pdf_to_markdown

COURT_TYPES = ("YARGITAYKARARI", "DANISTAYKARAR", "YERELHUKUK", "ISTINAFHUKUK", "KYB")


def _iso(date: str | None, *, end: bool) -> str | None:
    """Accept YYYY-MM-DD and expand to the ISO 8601 form the API requires."""
    if not date or date.endswith("Z"):
        return date
    return f"{date}T23:59:59.999Z" if end else f"{date}T00:00:00.000Z"


class BedestenClient(BaseClient):
    base_url = "https://bedesten.adalet.gov.tr"
    default_headers: ClassVar[dict[str, str]] = {
        **BaseClient.default_headers,
        "AdaletApplicationName": "UyapMevzuat",
        "Content-Type": "application/json; charset=utf-8",
        "Origin": "https://mevzuat.adalet.gov.tr",
        "Referer": "https://mevzuat.adalet.gov.tr/",
    }
    SEARCH = "/emsal-karar/searchDocuments"
    DOCUMENT = "/emsal-karar/getDocumentContent"

    def search(
        self,
        item_types: list[str],
        phrase: str,
        *,
        limit: int = 10,
        page: int = 1,
        birim: str = "",
        date_start: str | None = None,
        date_end: str | None = None,
    ) -> dict[str, Any]:
        data: dict[str, Any] = {
            "pageSize": max(1, min(limit, 10)),
            "pageNumber": page,
            "itemTypeList": item_types,
            "phrase": phrase,
            "sortFields": ["KARAR_TARIHI"],
            "sortDirection": "desc",
        }
        if birim:
            data["birimAdi"] = birim
        if date_start:
            data["kararTarihiStart"] = _iso(date_start, end=False)
        if date_end:
            data["kararTarihiEnd"] = _iso(date_end, end=True)

        body = self.post_json(
            self.SEARCH, {"data": data, "applicationName": "UyapMevzuat", "paging": True}
        )
        payload = body.get("data") or {}
        entries = payload.get("emsalKararList") or []
        results = [
            {
                "documentId": e.get("documentId"),
                "court": e.get("birimAdi"),
                "caseNo": e.get("esasNo"),
                "decisionNo": e.get("kararNo"),
                "decisionDate": e.get("kararTarihiStr") or e.get("kararTarihi"),
                "decisionType": e.get("kararTuru"),
            }
            for e in entries
        ]
        return {
            "total": payload.get("total", 0),
            "page": page,
            "count": len(results),
            "results": results,
        }

    def document_markdown(self, document_id: str) -> dict[str, Any]:
        body = self.post_json(
            self.DOCUMENT, {"data": {"documentId": document_id}, "applicationName": "UyapMevzuat"}
        )
        data = (body or {}).get("data") or {}
        content_b64, mime = data.get("content"), data.get("mimeType", "")
        if not content_b64:
            raise ValueError(f"No content for document {document_id}")
        raw = base64.b64decode(content_b64)
        if mime == "text/html":
            markdown = html_to_markdown(raw.decode("utf-8", errors="replace"))
        elif mime == "application/pdf":
            markdown = pdf_to_markdown(raw)
        else:
            markdown = raw.decode("utf-8", errors="replace")
        return {
            "documentId": document_id,
            "mimeType": mime,
            "sourceUrl": f"https://mevzuat.adalet.gov.tr/ictihat/{document_id}",
            "markdown": markdown,
        }
