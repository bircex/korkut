"""Search Turkish Constitutional Court (Anayasa Mahkemesi) decisions.

The court publishes two separate public "bilgi bankası" portals, both served as
plain HTML pages with no JSON API and no auth:

  • norm control (norm denetimi):
        https://normkararlarbilgibankasi.anayasa.gov.tr/Ara
  • individual application (bireysel başvuru):
        https://kararlarbilgibankasi.anayasa.gov.tr/Ara

Both accept a GET search and render a result list we scrape. This one client
covers both; `basvuru_turu` selects the portal and the matching result layout.
"""

from __future__ import annotations

import re
from typing import Any
from urllib.parse import urljoin

from tools._base.client import BaseClient

NORM_BASE = "https://normkararlarbilgibankasi.anayasa.gov.tr"
BIREYSEL_BASE = "https://kararlarbilgibankasi.anayasa.gov.tr"
SEARCH_PATH = "/Ara"

_TOTAL_RE = re.compile(r"(\d+)\s*Karar\s*Bulundu", re.IGNORECASE)
_REF_RE = re.compile(r"E\.\s*\d+/\d+\s*,?\s*K\.\s*\d+/\d+")
_DATE_RE = re.compile(r"\d{1,2}[./]\d{1,2}[./]\d{4}")


class AnayasaClient(BaseClient):
    """HTML client for both Constitutional Court portals.

    `base_url` stays empty so per-call absolute URLs decide the host; the shared
    rate-limiter and cache in `BaseClient` still apply.
    """

    base_url = ""
    default_headers = {
        **BaseClient.default_headers,
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
    }

    def search_norm(
        self, query: str, *, limit: int = 10, page: int = 1
    ) -> dict[str, Any]:
        params: list[tuple[str, str]] = [("KelimeAra[]", query)]
        if limit and limit != 10:
            params.append(("SatirSayisi", str(limit)))
        if page and page > 1:
            params.append(("page", str(page)))
        html = self.get_text(urljoin(NORM_BASE, SEARCH_PATH), params)
        rows, total = _parse_norm(html, NORM_BASE)
        return _envelope(rows[:limit], total, page)

    def search_bireysel(
        self, query: str, *, limit: int = 10, page: int = 1
    ) -> dict[str, Any]:
        params: list[tuple[str, str]] = [("KelimeAra[]", query)]
        if page and page > 1:
            params.append(("page", str(page)))
        html = self.get_text(urljoin(BIREYSEL_BASE, SEARCH_PATH), params)
        rows, total = _parse_bireysel(html, BIREYSEL_BASE)
        return _envelope(rows[:limit], total, page)

    def get_text(self, endpoint: str, params: Any = None) -> str:
        resp = self._request("GET", endpoint, params=params)
        return resp.text


def _soup(html: str) -> Any:
    from bs4 import BeautifulSoup

    return BeautifulSoup(html, "html.parser")


def _total(soup: Any) -> int:
    node = soup.find("div", class_="bulunankararsayisi") or soup.find(
        "div", class_="bulunankararsayisiMobil"
    )
    if not node:
        return 0
    match = _TOTAL_RE.search(node.get_text(" ", strip=True))
    return int(match.group(1)) if match else 0


def _clean(text: str) -> str:
    return re.sub(r"\s+", " ", text.replace("\xa0", " ")).strip()


def _parse_norm(html: str, base: str) -> tuple[list[dict[str, Any]], int]:
    soup = _soup(html)
    results: list[dict[str, Any]] = []
    for card in soup.find_all("div", class_="birkarar"):
        link = card.find("a", href=True)
        href = link["href"] if link else None
        url = urljoin(base, href) if href else None

        title_node = card.find("div", class_="bkararbaslik")
        title_raw = _clean(title_node.get_text(" ", strip=True)) if title_node else ""
        ref_match = _REF_RE.search(title_raw)
        title = ref_match.group(0) if ref_match else title_raw

        info_node = card.find("div", class_="kararbilgileri")
        date = ""
        if info_node:
            info = _clean(info_node.get_text(" ", strip=True))
            date_match = _DATE_RE.search(info)
            date = date_match.group(0) if date_match else ""

        results.append(
            {
                "id": _id_from_url(href) or title,
                "title": title or None,
                "type": "norm",
                "date": date or None,
                "url": url,
            }
        )
    return results, _total(soup)


def _parse_bireysel(html: str, base: str) -> tuple[list[dict[str, Any]], int]:
    soup = _soup(html)
    cards = soup.find_all("div", class_="KararBulteniBirKarar")
    if not cards:
        cards = soup.find_all("div", class_="birkarar")
    results: list[dict[str, Any]] = []
    for card in cards:
        title_node = card.find("h4")
        title = _clean(title_node.get_text(" ", strip=True)) if title_node else ""

        ref_node = card.find("div", class_="AltiCizili") or card
        link = ref_node.find("a", href=True)
        href = link["href"] if link else None
        url = urljoin(base, href) if href else None
        ref = _clean(link.get_text(strip=True)) if link else ""

        block = _clean(ref_node.get_text(" ", strip=True))
        date_match = _DATE_RE.findall(block)
        date = date_match[-1] if date_match else ""

        results.append(
            {
                "id": _id_from_url(href) or ref or title,
                "title": title or ref or None,
                "type": "bireysel",
                "date": date or None,
                "url": url,
            }
        )
    return results, _total(soup)


def _id_from_url(href: str | None) -> str | None:
    if not href:
        return None
    return href.rstrip("/").rsplit("/", 1)[-1] or None


def _envelope(rows: list[dict[str, Any]], total: int, page: int) -> dict[str, Any]:
    return {
        "total": total or len(rows),
        "page": page,
        "count": len(rows),
        "results": rows,
    }


def run_tool(
    query: str,
    basvuru_turu: str = "norm",
    limit: int = 10,
    page: int = 1,
) -> dict[str, Any]:
    """Search Constitutional Court decisions.

    query        : free-text search phrase (matched as a keyword).
    basvuru_turu : "norm" for norm control (norm denetimi) or "bireysel" for
                   individual applications (bireysel başvuru).
    limit        : maximum results to return for this page.
    page         : 1-indexed page number.

    Returns {"total", "page", "count", "results"} where each result is
    {id, title, type, date, url}.
    """
    turu = (basvuru_turu or "norm").strip().lower()
    if turu not in {"norm", "bireysel"}:
        raise ValueError("basvuru_turu must be 'norm' or 'bireysel'")
    with AnayasaClient() as client:
        if turu == "norm":
            return client.search_norm(query, limit=limit, page=page)
        return client.search_bireysel(query, limit=limit, page=page)
