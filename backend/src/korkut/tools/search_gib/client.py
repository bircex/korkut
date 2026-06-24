"""Search GİB (Gelir İdaresi Başkanlığı / Revenue Administration) tax rulings.

GİB publishes official tax-ruling letters ("özelge") that answer taxpayer
questions on VAT, income tax, corporate tax, stamp duty and related matters.
These rulings, along with the tax legislation they reference, are searchable
through the public gib.gov.tr JSON API. No API key is required.

The API exposes a single POST list endpoint: pagination/sort options travel as
query-string parameters while the search filters travel in the JSON body. The
backend ORs the free-text term across the ruling title, its law number and its
body text, so the same term is sent for all three fields.
"""

from __future__ import annotations

from typing import Any

from korkut.tools._base.client import BaseClient

_LIST_ENDPOINT = "/gibportal/mevzuat/ozelge/list"

# Backend-mandated constants. `ktype=99` selects özelge (tax rulings); the list
# endpoint returns nothing useful without status/deleted/ktype present.
_STATUS = 2
_DELETED = False
_KTYPE = 99
_SORT_FIELD = "ozelgeTarih"
_SORT_TYPE = "DESC"


class GibClient(BaseClient):
    """Client for the public GİB özelge search API."""

    base_url = "https://gib.gov.tr/api"

    def search(
        self,
        query: str,
        *,
        limit: int = 10,
        page: int = 1,
        date_start: str | None = None,
        date_end: str | None = None,
    ) -> dict[str, Any]:
        body: dict[str, Any] = {
            "status": _STATUS,
            "deleted": _DELETED,
            "ktype": _KTYPE,
        }
        term = (query or "").strip()
        if term:
            body["title"] = term
            body["kanunNo"] = term
            body["description"] = term

        start_iso = _to_iso(date_start, end_of_day=False)
        end_iso = _to_iso(date_end, end_of_day=True)
        if start_iso:
            body["ozelgeStartDate"] = start_iso
        if end_iso:
            body["ozelgeEndDate"] = end_iso

        params = {
            "page": max(0, page - 1),  # API pages are 0-indexed
            "size": limit,
            "sortFieldName": _SORT_FIELD,
            "sortType": _SORT_TYPE,
        }

        payload = self._request("POST", _LIST_ENDPOINT, json=body, params=params).json()
        return _parse(payload, page)


def run_tool(query: str, limit: int = 10, page: int = 1) -> dict[str, Any]:
    """Search GİB tax rulings (özelge).

    query  : free-text search across ruling title, law number and body text
    limit  : results per page (1-50)
    page   : 1-indexed page number

    Returns the standard tool envelope:
        {"total": int, "page": int, "count": int, "results": [...]}
    where each result is {id, title, type, date, url}.
    """
    with GibClient() as client:
        return client.search(query, limit=limit, page=page)


def _parse(payload: Any, page: int) -> dict[str, Any]:
    container = (payload or {}).get("resultContainer") or {}
    items = container.get("content") or []

    results: list[dict[str, Any]] = []
    for raw in items:
        if not isinstance(raw, dict):
            continue
        raw_id = raw.get("id")
        if raw_id is None:
            continue
        results.append(
            {
                "id": str(raw_id),
                "title": raw.get("title") or "",
                "type": "ozelge",
                "date": raw.get("ozelgeTarih") or "",
                "url": raw.get("siteLink") or "",
            }
        )

    return {
        "total": _as_int(container.get("totalElements")),
        "page": page,
        "count": len(results),
        "results": results,
    }


def _to_iso(value: str | None, *, end_of_day: bool) -> str | None:
    """Normalize a date to full ISO 8601; the backend rejects date-only strings."""
    if not value:
        return None
    text = value.strip()
    if not text:
        return None
    if "T" in text:
        return text
    suffix = "T23:59:59.999Z" if end_of_day else "T00:00:00.000Z"
    return f"{text}{suffix}"


def _as_int(value: Any) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return 0
