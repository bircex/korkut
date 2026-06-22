"""Search Turkish legislation (mevzuat) via the Bedesten API.

The Ministry of Justice's UYAP Mevzuat service exposes a public, key-free
JSON endpoint at `bedesten.adalet.gov.tr/mevzuat/searchDocuments` that lists and
full-text-searches every kind of legislation: kanun, KHK, tüzük, yönetmelik,
Cumhurbaşkanlığı kararname/karar/genelge, tebliğ, and more. This client maps our
English params onto that endpoint and normalises each hit to stable English keys.
"""

from __future__ import annotations

from typing import Any

from tools._base.client import BaseClient

# Legislation type codes the API accepts in `mevzuatTurList`.
LEGISLATION_TYPES = (
    "KANUN",
    "KHK",
    "TUZUK",
    "YONETMELIK",
    "CB_KARARNAME",
    "CB_YONETMELIK",
    "CB_KARAR",
    "CB_GENELGE",
    "KKY",
    "UY",
    "TEBLIGLER",
    "MULGA",
)


class MevzuatClient(BaseClient):
    """Thin client over the Bedesten mevzuat search endpoint."""

    base_url = "https://bedesten.adalet.gov.tr"
    default_headers = {
        **BaseClient.default_headers,
        "AdaletApplicationName": "UyapMevzuat",
        "Content-Type": "application/json; charset=utf-8",
        "Origin": "https://mevzuat.adalet.gov.tr",
        "Referer": "https://mevzuat.adalet.gov.tr/",
    }
    SEARCH = "/mevzuat/searchDocuments"

    def search(
        self,
        phrase: str,
        *,
        limit: int = 10,
        page: int = 1,
        types: list[str] | None = None,
    ) -> dict[str, Any]:
        """Search legislation, returning the normalised result envelope.

        phrase : title keyword(s); searched against the legislation name.
        limit  : results per page (clamped to 1-50).
        page   : 1-indexed page number.
        types  : optional list of legislation type codes (see LEGISLATION_TYPES).
        """
        data: dict[str, Any] = {
            "pageSize": max(1, min(limit, 50)),
            "pageNumber": max(1, page),
            "sortFields": ["RESMI_GAZETE_TARIHI"],
            "sortDirection": "desc",
        }
        if phrase:
            data["mevzuatAdi"] = phrase
        if types:
            data["mevzuatTurList"] = types

        body = self.post_json(
            self.SEARCH,
            {"data": data, "applicationName": "UyapMevzuat", "paging": True},
        )
        meta = (body or {}).get("metadata") or {}
        payload = (body or {}).get("data") or {}
        if meta.get("FMTY") not in (None, "SUCCESS") and not payload:
            return {"total": 0, "page": page, "count": 0, "results": []}

        entries = payload.get("mevzuatList") or []
        results = [self._normalize(e) for e in entries]
        return {
            "total": payload.get("total", 0),
            "page": page,
            "count": len(results),
            "results": results,
        }

    @staticmethod
    def _normalize(entry: dict[str, Any]) -> dict[str, Any]:
        """Map a raw API record to stable English legislation keys."""
        mevzuat_id = entry.get("mevzuatId")
        tur = entry.get("mevzuatTur")
        if isinstance(tur, dict):
            tur = tur.get("name") or tur.get("description")
        return {
            "id": mevzuat_id,
            "title": entry.get("mevzuatAdi"),
            "type": tur,
            "number": entry.get("mevzuatNo"),
            "series": entry.get("mevzuatTertip"),
            "date": entry.get("resmiGazeteTarihi"),
            "gazetteNo": entry.get("resmiGazeteSayisi"),
            "url": f"https://mevzuat.adalet.gov.tr/mevzuat?mevzuatId={mevzuat_id}"
            if mevzuat_id
            else entry.get("url"),
        }


def run_tool(
    query: str,
    limit: int = 10,
    page: int = 1,
    mevzuat_turu: str = "",
) -> dict[str, Any]:
    """Search Turkish legislation by title.

    query        : title keyword(s) to match against legislation names.
    limit        : results per page (1-50).
    page         : 1-indexed page.
    mevzuat_turu : optional legislation type filter, e.g. "KANUN", "KHK",
                   "TUZUK", "YONETMELIK", "CB_KARARNAME". Comma-separated values
                   are accepted to filter by several types at once.
    """
    types = [t.strip().upper() for t in mevzuat_turu.split(",") if t.strip()]
    with MevzuatClient() as client:
        return client.search(query, limit=limit, page=page, types=types or None)
