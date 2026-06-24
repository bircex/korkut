"""Search Turkish Constitutional Court (Anayasa Mahkemesi) decisions.

The court serves a single public "Kararlar Bilgi Bankası" portal as a React SPA
backed by a JSON API:

    https://normkararlarbilgibankasi.anayasa.gov.tr/kbb/

Despite the host name, that one portal now covers every decision corpus —
norm control (norm denetimi) and individual application (bireysel başvuru)
among them. The browser issues a single POST search and selects a corpus with a
top-level `kararTipi` discriminator:

    POST https://normkararlarbilgibankasi.anayasa.gov.tr/api/core/public/search
    {"query": "...", "page": 0, "size": 20, "sort": "yayinTarihi",
     "order": "desc", "kararTipi": "NormDenetimi"}

The response is {"total", "page", "data": [...]} with one object per decision.
`application_type` maps to the `kararTipi` value: "norm" -> "NormDenetimi",
"individual" -> "BireyselBasvuru".
"""

from __future__ import annotations

import base64
from typing import Any, ClassVar
from urllib.parse import urlencode

from korkut.tools._base.client import BaseClient

PORTAL_BASE = "https://normkararlarbilgibankasi.anayasa.gov.tr"
SEARCH_ENDPOINT = f"{PORTAL_BASE}/api/core/public/search"
DETAIL_PATH = "/kbb/pages/search"

# Maps the public `application_type` to the API `kararTipi` discriminator and the
# SPA detail-page segment used to build a shareable decision URL.
_KARAR_TIPI = {
    "norm": ("NormDenetimi", "NormDenetimi"),
    "individual": ("BireyselBasvuru", "BireyselBasvuru"),
}

# Seed the SPA hashes its decision ids with before base64url-encoding them.
_ID_SEED = "kbb"


class AnayasaClient(BaseClient):
    """JSON client for the Constitutional Court "Kararlar Bilgi Bankası" portal.

    `base_url` stays empty so the absolute search endpoint decides the host; the
    shared rate-limiter and cache in `BaseClient` still apply.
    """

    base_url = ""
    default_headers: ClassVar[dict[str, str]] = {
        **BaseClient.default_headers,
        "Accept": "application/json",
        "Content-Type": "application/json",
        "Referer": f"{PORTAL_BASE}/kbb/",
    }

    def search(
        self, query: str, karar_tipi: str, detail_segment: str, *, limit: int, page: int
    ) -> dict[str, Any]:
        api_page = max(page, 1) - 1
        payload = {
            "query": query,
            "page": api_page,
            "size": limit,
            "sort": "yayinTarihi",
            "order": "desc",
            "kararTipi": karar_tipi,
        }
        body = self.post_json(SEARCH_ENDPOINT, payload)
        rows = [_parse_row(item, detail_segment) for item in body.get("data") or []]
        total = int(body.get("total") or 0)
        return _envelope(rows, total, page)


def _detail_url(decision_id: str | None, detail_segment: str, karar_tipi: str) -> str | None:
    if not decision_id:
        return None
    raw = f"{_ID_SEED}:{decision_id}".encode()
    hashed = base64.b64encode(raw).decode().replace("+", "-").replace("/", "_").rstrip("=")
    query = urlencode({"id": hashed, "type": karar_tipi})
    return f"{PORTAL_BASE}{DETAIL_PATH}/{detail_segment}?{query}"


def _decision_date(item: dict[str, Any]) -> str | None:
    date = item.get("kararTarihi") or item.get("yayinTarihi")
    if not date:
        return None
    return str(date).split("T", 1)[0]


def _title(item: dict[str, Any], karar_tipi: str) -> str | None:
    if karar_tipi == "BireyselBasvuru":
        name = (item.get("basvuruAdi") or "").strip()
        basvuru_no = (item.get("basvuruNo") or "").strip()
        if name and basvuru_no:
            return f"{name} (B. No: {basvuru_no})"
        return name or basvuru_no or None
    esas = (item.get("esasNo") or "").strip()
    karar = (item.get("kararNo") or "").strip()
    if esas and karar:
        return f"E. {esas}, K. {karar}"
    return esas or karar or None


def _parse_row(item: dict[str, Any], detail_segment: str) -> dict[str, Any]:
    karar_tipi = item.get("kararTipi") or detail_segment
    decision_id = item.get("id")
    return {
        "id": str(decision_id) if decision_id is not None else None,
        "title": _title(item, karar_tipi),
        "type": karar_tipi,
        "date": _decision_date(item),
        "url": _detail_url(
            str(decision_id) if decision_id is not None else None,
            detail_segment,
            karar_tipi,
        ),
    }


def _envelope(rows: list[dict[str, Any]], total: int, page: int) -> dict[str, Any]:
    return {
        "total": total or len(rows),
        "page": page,
        "count": len(rows),
        "results": rows,
    }


def run_tool(
    query: str,
    application_type: str = "norm",
    limit: int = 10,
    page: int = 1,
) -> dict[str, Any]:
    """Search Constitutional Court decisions.

    query        : free-text search phrase (matched as a keyword).
    application_type : "norm" for norm control (norm denetimi) or "individual" for
                   individual applications (bireysel başvuru).
    limit        : maximum results to return for this page.
    page         : 1-indexed page number.

    Returns {"total", "page", "count", "results"} where each result is
    {id, title, type, date, url}.
    """
    mode = (application_type or "norm").strip().lower()
    if mode not in _KARAR_TIPI:
        raise ValueError("application_type must be 'norm' or 'individual'")
    karar_tipi, detail_segment = _KARAR_TIPI[mode]
    with AnayasaClient() as client:
        return client.search(query, karar_tipi, detail_segment, limit=limit, page=page)
