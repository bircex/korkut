"""Search Sayıştay (Turkish Court of Accounts) decisions via sayistay.gov.tr.

Sayıştay publishes three kinds of decisions, each behind its own ASP.NET Core MVC
endpoint that drives a server-side jQuery DataTables grid:

  * genel_kurul   — Genel Kurul (General Assembly): precedent-setting interpretive
                    rulings by the full assembly.
  * temyiz_kurulu — Temyiz Kurulu (Appeals Board): appeals against chamber decisions.
  * daire         — Daire (Chamber): first-instance audit findings and sanctions.

Each search page (``/KararlarGenelKurul``, ``/KararlarTemyiz``, ``/KararlarDaire``)
posts to a ``.../DataTablesList`` action. The request must carry:

  * the standard server-side DataTables payload — ``draw``, ``start``, ``length``,
    ``search[value]``, and one ``columns[i][...]`` group per declared column plus
    an ``order[0][...]`` group. The ASP.NET Core model binder rejects the request
    with HTTP 500 if these arrays are missing or malformed.
  * the page-specific search-form fields (the filter model, e.g.
    ``KararlarGenelKurulAra.KARARTAMAMI`` for the full-text field), which the page
    serialises into the same POST body.
  * the antiforgery token ``__RequestVerificationToken`` harvested from the GET
    page, together with the ``.AspNetCore.Antiforgery.*`` cookie set by that GET.

The response is DataTables JSON (``data``, ``recordsTotal``, ``recordsFiltered``);
the per-row schema differs by decision type, so each type is mapped to the shared
output contract separately. No API key is required.
"""

from __future__ import annotations

import re
from collections.abc import Callable
from typing import Any, ClassVar

from tools._base.client import BaseClient

DECISION_TYPES = ("genel_kurul", "temyiz_kurulu", "daire")

# Page used to bootstrap the antiforgery cookie + token, and the DataTables search
# action, for each decision type.
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

# DataTables columns declared by each search page, in declaration order. The
# server-side binder expects one columns[i] group per column; the `data` value
# must match the page exactly. Sourced from each page's DataTables init script.
_COLUMNS = {
    "genel_kurul": ["KARARNO", "KARARNO", "KARARTARIH", "KARAROZETI", ""],
    "temyiz_kurulu": [
        "TEMYIZTUTANAKTARIHI",
        "TEMYIZTUTANAKTARIHI",
        "ILAMDAIRESI",
        "TEMYIZKARAR",
        "",
    ],
    "daire": ["YARGILAMADAIRESI", "KARARTRH", "KARARNO", "YARGILAMADAIRESI", "WEBKARARMETNI", ""],
}
# Default sort column index used by each page's DataTables init script.
_ORDER_COLUMN = {"genel_kurul": 2, "temyiz_kurulu": 0, "daire": 1}

# Full-text search field for each decision type's filter model.
_TEXT_FIELD = {
    "genel_kurul": "KararlarGenelKurulAra.KARARTAMAMI",
    "temyiz_kurulu": "KararlarTemyizAra.TEMYIZKARAR",
    "daire": "KararlarDaireAra.WEBKARARMETNI",
}
# Remaining (empty) filter-model fields each page serialises into the POST body.
_EMPTY_FIELDS = {
    "genel_kurul": [
        "KararlarGenelKurulAra.KARARNO",
        "KararlarGenelKurulAra.KARAREK",
        "KararlarGenelKurulAra.KARARTARIHBaslangic",
        "KararlarGenelKurulAra.KARARTARIHBitis",
    ],
    "temyiz_kurulu": [
        "KararlarTemyizAra.DOSYANO",
        "KararlarTemyizAra.ILAMDAIRESI",
        "KararlarTemyizAra.ILAMNO",
        "KararlarTemyizAra.KAMUIDARESITURU",
        "KararlarTemyizAra.KARARTRHBaslangic",
        "KararlarTemyizAra.KARARTRHBitis",
        "KararlarTemyizAra.TEMYIZTUTANAKNO",
        "KararlarTemyizAra.WEBKARARKONUSU",
        "KararlarTemyizAra.YILI",
    ],
    "daire": [
        "KararlarDaireAra.HESAPYILI",
        "KararlarDaireAra.ILAMNO",
        "KararlarDaireAra.KAMUIDARESITURU",
        "KararlarDaireAra.KARARTRHBaslangic",
        "KararlarDaireAra.KARARTRHBitis",
        "KararlarDaireAra.WEBKARARKONUSU",
        "KararlarDaireAra.YARGILAMADAIRESI",
    ],
}

_CSRF_RE = re.compile(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', re.IGNORECASE)

# Some upstream WAF deployments answer with an HTTP 4xx and a Turkish block page
# instead of JSON. Detect and surface it clearly.
_WAF_MARKER = "Bilgi Güvenliği Politikaları Gereği Kısıtlanmıştır"

# DataTables hard-caps the page size the grid renders.
_MAX_LENGTH = 10


class SayistayWafBlocked(RuntimeError):
    """Raised when the upstream WAF blocks the DataTables request."""


class SayistaySearchError(RuntimeError):
    """Raised when the search endpoint returns a non-JSON or error response."""


class SayistayClient(BaseClient):
    base_url = "https://www.sayistay.gov.tr"
    default_headers: ClassVar[dict[str, str]] = {
        **BaseClient.default_headers,
        "Accept": "application/json, text/javascript, */*; q=0.01",
        "X-Requested-With": "XMLHttpRequest",
    }

    def _csrf_token(self, decision_type: str) -> str:
        """Fetch the search page to seed the antiforgery cookie and read the token.

        The GET response sets the ``.AspNetCore.Antiforgery.*`` cookie on the shared
        httpx client; the matching ``__RequestVerificationToken`` is returned here so
        the subsequent POST can pair them.
        """
        resp = self.get_text(_PAGE[decision_type])
        if _WAF_MARKER in resp.text:
            raise SayistayWafBlocked(
                "sayistay.gov.tr blocked the request at the upstream WAF while "
                "loading the search page. This is a server-side restriction that "
                "affects all clients; try again later."
            )
        match = _CSRF_RE.search(resp.text)
        if not match:
            raise SayistaySearchError(
                "Could not find the antiforgery token on the Sayıştay search page; "
                "the page layout may have changed."
            )
        return match.group(1)

    def _form_fields(
        self,
        decision_type: str,
        query: str,
        *,
        start: int,
        length: int,
        token: str,
    ) -> dict[str, str]:
        """Build the full server-side DataTables + filter-model POST body.

        Every key is unique (columns are indexed), so a flat dict round-trips to
        the wire form exactly as the DataTables grid sends it.
        """
        fields: dict[str, str] = {
            "draw": "1",
            "start": str(start),
            "length": str(length),
            "search[value]": "",
            "search[regex]": "false",
            "order[0][column]": str(_ORDER_COLUMN[decision_type]),
            "order[0][dir]": "desc",
        }
        for index, data_name in enumerate(_COLUMNS[decision_type]):
            fields[f"columns[{index}][data]"] = data_name
            fields[f"columns[{index}][name]"] = ""
            fields[f"columns[{index}][searchable]"] = "true"
            fields[f"columns[{index}][orderable]"] = "true"
            fields[f"columns[{index}][search][value]"] = ""
            fields[f"columns[{index}][search][regex]"] = "false"
        fields["__RequestVerificationToken"] = token
        fields[_TEXT_FIELD[decision_type]] = query or ""
        for name in _EMPTY_FIELDS[decision_type]:
            fields[name] = ""
        return fields

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
                f"decision_type must be one of {DECISION_TYPES}, got {decision_type!r}"
            )

        length = max(1, min(limit, _MAX_LENGTH))
        start = max(0, (max(1, page) - 1) * length)
        token = self._csrf_token(decision_type)

        fields = self._form_fields(decision_type, query, start=start, length=length, token=token)
        headers = {
            "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
            "Referer": f"{self.base_url}{_PAGE[decision_type]}",
        }
        resp = self.post_form(_SEARCH[decision_type], fields, headers=headers)
        if _WAF_MARKER in resp.text:
            raise SayistayWafBlocked(
                "sayistay.gov.tr blocked the search request at the upstream WAF. "
                "This is a server-side restriction that affects all clients; try "
                "again later."
            )
        try:
            body = resp.json() or {}
        except ValueError as exc:
            raise SayistaySearchError(
                "Sayıştay search endpoint returned a non-JSON response "
                f"(HTTP {resp.status_code}); the request shape may be stale."
            ) from exc

        rows = body.get("data") or []
        mapper = _MAPPERS[decision_type]
        results = [mapper(row, decision_type) for row in rows]
        total = body.get("recordsFiltered")
        if total is None:
            total = body.get("recordsTotal") or 0
        return {
            "total": total,
            "page": page,
            "count": len(results),
            "results": results,
        }


def _detail_url(decision_type: str, decision_id: Any) -> str:
    path = _PAGE[decision_type].lstrip("/")
    return f"https://www.sayistay.gov.tr/{path}/Detay/{decision_id}/"


def _map_genel_kurul(row: dict[str, Any], decision_type: str) -> dict[str, Any]:
    decision_id = row.get("Id")
    return {
        "id": decision_id,
        "title": (row.get("KARARNO") or "").strip(),
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
        "title": (row.get("KARARNO") or row.get("ILAMNO") or "").strip(),
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
    decision_type: str = "genel_kurul",
    limit: int = 10,
    page: int = 1,
) -> dict[str, Any]:
    """Search Sayıştay decisions.

    query         : free-text search phrase.
    decision_type : decision type — one of "genel_kurul", "temyiz_kurulu", "daire".
    limit         : results per page (1-10).
    page          : 1-indexed page number.

    Returns {"total", "page", "count", "results": [{id, title, decisionType,
    date, url}, ...]}.
    """
    with SayistayClient() as client:
        return client.search(decision_type, query, limit=limit, page=page)
