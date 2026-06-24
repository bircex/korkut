"""Search Rekabet Kurumu (Turkish Competition Authority) decisions.

The Competition Authority publishes its decisions on the public portal
``https://www.rekabet.gov.tr/tr/Kararlar``. There is no JSON API and no key:
the search results page is server-rendered HTML, so this tool fetches that page
and parses the decision list out of it. Each result links to a decision landing
page identified by a ``kararId`` GUID.

Decision-type filtering is exposed through the ``decision_type`` argument, mapped to
the GUID the portal expects on the ``KararTuruID`` query parameter.
"""

from __future__ import annotations

import math
import re
from html.parser import HTMLParser
from typing import Any, ClassVar
from urllib.parse import parse_qs, urljoin, urlparse

from tools._base.client import BaseClient

# User-friendly decision-type names mapped to the portal's KararTuruID GUIDs.
# Empty string ("" / "all") means "all types" (no filter applied).
DECISION_TYPE_GUIDS: dict[str, str] = {
    "": "",
    "all": "",
    "tumu": "",
    "birlesme_devralma": "2fff0979-9f9d-42d7-8c2e-a30705889542",
    "diger": "dda8feaf-c919-405c-9da1-823f22b45ad9",
    "menfi_tespit_muafiyet": "95ccd210-5304-49c5-b9e0-8ee53c50d4e8",
    "ozellestirme": "e1f14505-842b-4af5-95d1-312d6de1a541",
    "rekabet_ihlali": "720614bf-efd1-4dca-9785-b98eb65f2677",
}

RESULTS_PER_PAGE = 10


class RekabetClient(BaseClient):
    """HTML-scraping client for the Competition Authority decisions portal."""

    base_url = "https://www.rekabet.gov.tr"
    default_headers: ClassVar[dict[str, str]] = {
        **BaseClient.default_headers,
        "Accept": ("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8"),
    }
    SEARCH = "/tr/Kararlar"
    DECISION_PATH = "/Karar"

    def get_html(self, endpoint: str, params: Any = None) -> str:
        """Fetch an endpoint and return its decoded HTML body."""
        data, _ = self.get_bytes(endpoint, params=params)
        return data.decode("utf-8", errors="replace")

    def search(
        self,
        query: str,
        *,
        decision_type: str = "",
        limit: int = 10,
        page: int = 1,
    ) -> dict[str, Any]:
        guid = DECISION_TYPE_GUIDS.get(decision_type.strip().lower())
        if guid is None:
            allowed = ", ".join(k for k in DECISION_TYPE_GUIDS if k)
            raise ValueError(f"unknown decision_type {decision_type!r}; allowed: {allowed}")

        params: list[tuple[str, str]] = [
            ("sayfaAdi", query or ""),
            ("YayinlanmaTarihi", ""),
            ("PdfText", ""),
            ("KararTuruID", guid),
            ("KararSayisi", ""),
            ("KararTarihi", ""),
        ]
        if page and page > 1:
            params.append(("page", str(page)))

        html_body = self.get_html(self.SEARCH, params=params)
        total, decisions = _parse_search_html(html_body, self.base_url)

        limit = max(1, min(limit, RESULTS_PER_PAGE))
        decisions = decisions[:limit]
        return {
            "total": total,
            "page": page,
            "count": len(decisions),
            "results": decisions,
        }


def run_tool(
    query: str,
    decision_type: str = "",
    limit: int = 10,
    page: int = 1,
) -> dict[str, Any]:
    """Search Competition Authority decisions.

    query       : free-text title search (maps to the portal's "sayfaAdi" field)
    decision_type  : optional decision type filter — one of "birlesme_devralma",
                  "diger", "menfi_tespit_muafiyet", "ozellestirme",
                  "rekabet_ihlali" (empty = all types)
    limit       : results per page (1-10)
    page        : 1-indexed page number
    """
    with RekabetClient() as client:
        return client.search(query, decision_type=decision_type, limit=limit, page=page)


class _SearchHTMLParser(HTMLParser):
    """Extract the decision list and total count from a results page.

    The portal renders each decision as a ``<table class="equalDivide">`` inside
    ``<div id="kararList">``. Every table holds three rows:
      row 0 : publication date | decision number | related-cases link
      row 1 : decision date | decision type
      row 2 : title cell with the main decision link (``/Karar?kararId=...``)
    The total record count is printed as "Toplam : N" inside ``<div class="yazi01">``.
    """

    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self._depth = 0
        self._in_karar_list = False
        self._karar_list_depth: int | None = None
        self._in_pagination = False
        self._pagination_depth: int | None = None
        self.pagination_text: list[str] = []

        self._row = -1
        self._cell = -1
        self._in_table = False
        self._current: dict[str, Any] | None = None
        self._cell_text: list[str] = []
        self._capture_link = False
        self._link_text: list[str] = []
        self.tables: list[dict[str, Any]] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        self._depth += 1
        attr = {k: (v or "") for k, v in attrs}

        if tag == "div":
            classes = attr.get("class", "").split()
            if attr.get("id") == "kararList":
                self._in_karar_list = True
                self._karar_list_depth = self._depth
            if "yazi01" in classes:
                self._in_pagination = True
                self._pagination_depth = self._depth

        if (
            tag == "table"
            and self._in_karar_list
            and "equalDivide" in attr.get("class", "").split()
        ):
            self._in_table = True
            self._row = -1
            self._current = {"rows": []}

        if self._in_table:
            if tag == "tr":
                self._row += 1
                self._cell = -1
                self._current["rows"].append([])  # type: ignore[index]
            elif tag == "td":
                self._cell += 1
                self._cell_text = []
            elif tag == "a":
                self._capture_link = True
                self._link_text = []
                self._current.setdefault("links", []).append(  # type: ignore[union-attr]
                    {"href": attr.get("href", ""), "row": self._row, "text": ""}
                )

    def handle_endtag(self, tag: str) -> None:
        if self._in_table:
            if tag == "a" and self._capture_link:
                self._capture_link = False
                if self._current and self._current.get("links"):
                    self._current["links"][-1]["text"] = "".join(self._link_text).strip()
            elif tag == "td":
                if self._current is not None and self._current["rows"]:
                    self._current["rows"][-1].append("".join(self._cell_text).strip())
            elif tag == "table":
                self._in_table = False
                if self._current is not None:
                    self.tables.append(self._current)
                self._current = None

        if tag == "div":
            if self._pagination_depth == self._depth:
                self._in_pagination = False
                self._pagination_depth = None
            if self._karar_list_depth == self._depth:
                self._in_karar_list = False
                self._karar_list_depth = None

        self._depth -= 1

    def handle_data(self, data: str) -> None:
        if self._in_table:
            if self._capture_link:
                self._link_text.append(data)
            self._cell_text.append(data)
        if self._in_pagination:
            self.pagination_text.append(data)


def _karar_id_from_href(href: str) -> str:
    qs = parse_qs(urlparse(href).query)
    values = qs.get("kararId")
    return values[0] if values else ""


def _parse_search_html(html_body: str, base_url: str) -> tuple[int, list[dict[str, Any]]]:
    parser = _SearchHTMLParser()
    parser.feed(html_body)

    total = 0
    match = re.search(r"Toplam\s*:\s*(\d+)", " ".join(parser.pagination_text))
    if match:
        total = int(match.group(1))

    results: list[dict[str, Any]] = []
    for table in parser.tables:
        rows = table.get("rows", [])
        links = table.get("links", [])

        publication_date = rows[0][0] if len(rows) > 0 and len(rows[0]) > 0 else ""
        decision_number = rows[0][1] if len(rows) > 0 and len(rows[0]) > 1 else ""
        decision_date = rows[1][0] if len(rows) > 1 and len(rows[1]) > 0 else ""
        decision_type = rows[1][1] if len(rows) > 1 and len(rows[1]) > 1 else ""

        decision_prefix = f"{RekabetClient.DECISION_PATH}?kararId="
        main_link = next(
            (link for link in links if link.get("href", "").startswith(decision_prefix)),
            None,
        )
        any_link = next((link for link in links if "kararId=" in link.get("href", "")), None)
        chosen = main_link or any_link
        if not chosen:
            continue

        karar_id = _karar_id_from_href(chosen["href"])
        if not karar_id:
            continue

        title = (main_link or chosen).get("text", "") or decision_number
        url = urljoin(base_url, f"{RekabetClient.DECISION_PATH}?kararId={karar_id}")

        results.append(
            {
                "id": karar_id,
                "title": title,
                "type": decision_type,
                "date": decision_date or publication_date,
                "url": url,
            }
        )

    if total == 0 and results:
        total = len(results)
    return total, results


def total_pages(total: int) -> int:
    """Pages the portal would render for ``total`` records (10 per page)."""
    if total <= 0:
        return 0
    return max(1, math.ceil(total / RESULTS_PER_PAGE))
