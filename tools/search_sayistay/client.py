"""Search Sayıştay (Turkish Court of Accounts) decisions via sayistay.gov.tr.

Sayıştay publishes three kinds of decisions, each behind its own ASP.NET WebForms
DataTables endpoint:

  * genel_kurul   — Genel Kurul (General Assembly): precedent-setting interpretive
                    rulings by the full assembly.
  * temyiz_kurulu — Temyiz Kurulu (Appeals Board): appeals against chamber decisions.
  * daire         — Daire (Chamber): first-instance audit findings and sanctions.

Each endpoint expects a form-urlencoded DataTables POST carrying a CSRF token
(``__RequestVerificationToken``) that is harvested from the corresponding search
page together with the session cookie. The response is DataTables JSON with
``data``, ``recordsTotal`` and ``recordsFiltered`` fields; the per-row schema
differs by decision type, so each type is mapped to the shared output contract
separately.

No API key is required. All endpoints are public government portals.
"""

from __future__ import annotations

import re
from typing import Any, Callable

from tools._base.client import BaseClient

DECISION_TYPES = ("genel_kurul", "temyiz_kurulu", "daire")

# Page used to bootstrap the session cookie + CSRF token, and the DataTables
# search endpoint, for each decision type.
_PAGE = {
    "genel_kurul": "/KararlarGenelKurul",
    "temyiz_kurulu": "/KararlarTemyiz",
    "daire": "/KararlarDaire",
}
_SEARCH = {
    "genel_kurul": "/KararlarGenelKurul/DataTablesList",
    "temyiz_kurulu": "/KararlarTemyiz/DataTablesList",
    "daire": "/KararlarDaire/DataTablesList",
}
# Human-facing label exposed in each result's `decisionType`.
_LABEL = {
    "genel_kurul": "Genel Kurul",
    "temyiz_kurulu": "Temyiz Kurulu",
    "daire": "Daire",
}

_CSRF_RE = re.compile(
    r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', re.IGNORECASE
)

# Some upstream WAF deployments answer the DataTables POST with an HTTP 418 and a
# Turkish block page instead of JSON. Detect and surface it clearly.
_WAF_MARKER = "Bilgi Güvenliği Politikaları Gereği Kısıtlanmıştır"


class SayistayWafBlocked(RuntimeError):
    """Raised when the upstream WAF blocks the DataTables request."""


class SayistayClient(BaseClient):
    base_url = "https://www.sayistay.gov.tr"
    default_headers = {
        **BaseClient.default_headers,
        "Accept": "application/json, text/javascript, */*; q=0.01",
        "X-Requested-With": "XMLHttpRequest",
    }

    def _csrf_token(self, decision_type: str) -> str:
        """Fetch the search page to seed the session cookie and read the CSRF token."""
        resp = self.get_text(_PAGE[decision_type])
        match = _CSRF_RE.search(resp.text)
        return match.group(1) if match else ""

    def search(
        self,
        decision_type: str,
        query: str,
        *,
        limit: int = 10,
        page: int = 1,
    ) -> dict[str, Any]:
        if decision_type not in DECISION_TYPES:
            raise ValueError(
                f"karar_turu must be one of {DECISION_TYPES}, got {decision_type!r}"
            )

        length = max(1, min(limit, 10))
        start = max(0, (max(1, page) - 1) * length)
        token = self._csrf_token(decision_type)

        fields: dict[str, str] = {
            "draw": "1",
            "start": str(start),
            "length": str(length),
            "search[value]": query or "",
            "search[regex]": "false",
            "__RequestVerificationToken": token,
        }
        fields.update(_query_fields(decision_type, query))

        headers = {
            "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
            "Referer": f"{self.base_url}{_PAGE[decision_type]}",
        }
        resp = self.post_form(_SEARCH[decision_type], fields, headers=headers)
        if resp.status_code == 418 or _WAF_MARKER in resp.text:
            raise SayistayWafBlocked(
                "sayistay.gov.tr blocked the request at the upstream WAF "
                f"(HTTP {resp.status_code}). This is a server-side restriction "
                "that affects all clients; try again later."
            )

        body = resp.json() or {}
        rows = body.get("data") or []
        mapper = _MAPPERS[decision_type]
        results = [mapper(row, decision_type) for row in rows]
        total = body.get("recordsFiltered") or body.get("recordsTotal") or 0
        return {
            "total": total,
            "page": page,
            "count": len(results),
            "results": results,
        }


def _query_fields(decision_type: str, query: str) -> dict[str, str]:
    """Map a free-text query onto the type-specific full-text form field.

    Each decision type exposes its own ASP.NET search model with distinct field
    names. Only the full-text field is wired here; the remaining filters default
    to empty/all on the server. Field names are best-effort and should be
    verified against the live API.
    """
    if not query:
        return {}
    if decision_type == "genel_kurul":
        return {"KararlarGenelKurulAra.KARARTAMAMI": query}
    if decision_type == "temyiz_kurulu":
        return {"KararlarTemyizAra.TEMYIZKARAR": query}
    return {"KararlarDaireAra.WEBKARARMETNI": query}


def _detail_url(decision_type: str, decision_id: Any) -> str:
    path = _PAGE[decision_type].lstrip("/")
    return f"https://www.sayistay.gov.tr/{path}/Detay/{decision_id}/"


def _map_genel_kurul(row: dict[str, Any], decision_type: str) -> dict[str, Any]:
    decision_id = row.get("Id")
    return {
        "id": decision_id,
        "title": row.get("KARARNO") or "",
        "decisionType": _LABEL[decision_type],
        "date": row.get("KARARTARIH") or "",
        "url": _detail_url(decision_type, decision_id),
    }


def _map_temyiz_kurulu(row: dict[str, Any], decision_type: str) -> dict[str, Any]:
    decision_id = row.get("Id")
    daire = row.get("ILAMDAIRESI")
    title = f"{daire}. Daire" if daire not in (None, "") else (row.get("TEMYIZKARAR") or "")
    return {
        "id": decision_id,
        "title": title,
        "decisionType": _LABEL[decision_type],
        "date": row.get("TEMYIZTUTANAKTARIHI") or "",
        "url": _detail_url(decision_type, decision_id),
    }


def _map_daire(row: dict[str, Any], decision_type: str) -> dict[str, Any]:
    decision_id = row.get("Id")
    return {
        "id": decision_id,
        "title": row.get("KARARNO") or row.get("ILAMNO") or "",
        "decisionType": _LABEL[decision_type],
        "date": row.get("KARARTRH") or "",
        "url": _detail_url(decision_type, decision_id),
    }


_MAPPERS: dict[str, Callable[[dict[str, Any], str], dict[str, Any]]] = {
    "genel_kurul": _map_genel_kurul,
    "temyiz_kurulu": _map_temyiz_kurulu,
    "daire": _map_daire,
}


def run_tool(
    query: str,
    karar_turu: str = "genel_kurul",
    limit: int = 10,
    page: int = 1,
) -> dict[str, Any]:
    """Search Sayıştay decisions.

    query       : free-text search phrase.
    karar_turu  : decision type — one of "genel_kurul", "temyiz_kurulu", "daire".
    limit        : results per page (1-10).
    page         : 1-indexed page number.

    Returns {"total", "page", "count", "results": [{id, title, decisionType,
    date, url}, ...]}.
    """
    with SayistayClient() as client:
        return client.search(karar_turu, query, limit=limit, page=page)
