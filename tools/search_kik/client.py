"""Search KİK (Kamu İhale Kurulu / Public Procurement Authority) board decisions.

Source: the legacy citizen portal at ``https://ekap.kik.gov.tr``. No API key is
required.

Why the legacy portal and not the v2 JSON API:
The modern v2 endpoint (``ekapv2.kik.gov.tr/b_ihalearaclari/api/...``) rejects
every unauthenticated request with HTTP 401 and the body
``"CryptoJs engellendi: zorunlu header eksik (iv/apiSecretKey/ts)"`` — it expects
per-request CryptoJS headers (an AES-signed timestamp/IV pair) that only its
Angular front end can mint. That signing scheme cannot be reproduced key-free.

The older WebForms search page ``/EKAP/Vatandas/KurulKararSorgu.aspx`` serves the
same board decisions with no auth at all. It is a classic ASP.NET WebForms grid:

  * a GET seeds the ``__VIEWSTATE`` / ``__EVENTVALIDATION`` / ``__PIT`` tokens and
    the session cookie on the shared httpx client;
  * a POST that replays those tokens together with the filter fields and
    ``__EVENTTARGET=...$btnAra`` runs the search and returns a server-rendered
    ``GridView`` (``grdKurulKararSorguSonuc``), 10 rows per page;
  * each row carries a ``btnOnizle`` (preview) postback; firing it reveals the
    decision's stable permalink ``KararGoster.aspx?KararId=<hash>``.

The decision family is chosen with the ``kurulKararTip`` radio group
(``rbUyusmazlik`` / ``rbDuzenleyici`` / ``rbMahkeme``), selected here via the
``decision_type`` parameter.

The KİK host serves TLS through a legacy stack that modern OpenSSL rejects unless
``OP_LEGACY_SERVER_CONNECT`` is enabled and the security level is relaxed, so this
client builds its own transport in ``__init__`` while reusing every other piece of
``BaseClient`` (token bucket, 429 back-pressure, cache, retry loop).
"""

from __future__ import annotations

import re
import ssl
from html import unescape
from typing import Any

import httpx

from tools._base.client import BaseClient

BASE_URL = "https://ekap.kik.gov.tr"
_SEARCH_PATH = "/EKAP/Vatandas/KurulKararSorgu.aspx"
_DOCUMENT_URL = f"{BASE_URL}/EKAP/Vatandas/KararGoster.aspx?KararId="

# WebForms control-name prefix shared by every field on the search page.
_PREFIX = "ctl00$ContentPlaceHolder1$"
_SEARCH_BUTTON = f"{_PREFIX}btnAra"

# decision_type -> kurulKararTip radio value + human-facing label.
_DECISION_TYPES: dict[str, tuple[str, str]] = {
    "uyusmazlik": ("rbUyusmazlik", "uyusmazlik"),
    "duzenleyici": ("rbDuzenleyici", "duzenleyici"),
    "mahkeme": ("rbMahkeme", "mahkeme"),
}

# Hidden WebForms state fields replayed from the GET into every POST.
_STATE_FIELDS = (
    "__VIEWSTATE",
    "__VIEWSTATEGENERATOR",
    "__VIEWSTATEENCRYPTED",
    "__EVENTVALIDATION",
    "__PIT",
    "__PITC",
    "__LASTFOCUS",
    "__SCROLLPOSITIONX",
    "__SCROLLPOSITIONY",
)

# The portal renders a fixed 10 rows per grid page.
_PAGE_SIZE = 10

_GRID_RE = re.compile(
    r'<table[^>]*id="grdKurulKararSorguSonuc".*?</table>', re.S | re.I
)
_ROW_RE = re.compile(r"<tr[^>]*>(.*?)</tr>", re.S | re.I)
_CELL_RE = re.compile(r"<td[^>]*>(.*?)</td>", re.S | re.I)
_PAGER_RE = re.compile(r"<tr[^>]*>(?:(?!</tr>).)*ctl14(?:(?!</tr>).)*</tr>", re.S | re.I)
_PAGE_LINK_RE = re.compile(
    r"__doPostBack\(&#39;([^&]+ctl14\$ctl\d+)&#39;,&#39;&#39;\)\">(\d+)<"
)
_PREVIEW_RE = re.compile(r"__doPostBack\(&#39;([^&]+btnOnizle)&#39;")
_KARAR_ID_RE = re.compile(r"KararGoster\.aspx\?KararId=([0-9a-fA-F]+)")
_TAG_RE = re.compile(r"<[^>]+>")
_WS_RE = re.compile(r"\s+")
_DATE_RE = re.compile(r"(\d{2})\.(\d{2})\.(\d{4})")


class KikSearchError(RuntimeError):
    """Raised when the KİK portal returns an error page or unexpected layout."""


def _legacy_ssl_context() -> ssl.SSLContext:
    """Build an SSL context tolerant of the KİK host's legacy TLS stack."""
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    if hasattr(ssl, "OP_LEGACY_SERVER_CONNECT"):
        ctx.options |= ssl.OP_LEGACY_SERVER_CONNECT
    try:
        ctx.set_ciphers("DEFAULT:@SECLEVEL=1")
    except ssl.SSLError:
        pass
    return ctx


def _clean(html: str) -> str:
    return unescape(_WS_RE.sub(" ", _TAG_RE.sub(" ", html)).strip())


def _iso_date(raw: str) -> str:
    match = _DATE_RE.match(raw)
    return f"{match.group(3)}-{match.group(2)}-{match.group(1)}" if match else raw


class KikClient(BaseClient):
    """Client for the kik.gov.tr legacy board-decision search portal."""

    base_url = BASE_URL
    default_headers = {
        **BaseClient.default_headers,
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Accept-Language": "tr-TR,tr;q=0.9",
    }

    def __init__(self, *, timeout: float = 60.0, **kwargs: Any) -> None:
        super().__init__(timeout=timeout, **kwargs)
        # Replace BaseClient's default transport with one that tolerates the KİK
        # host's legacy TLS; everything else (bucket, cache, retries, cookies)
        # stays. cookies must persist across the GET/POST WebForms handshake.
        self._client.close()
        self._client = httpx.Client(
            base_url=self.base_url,
            headers=self.default_headers,
            timeout=timeout,
            verify=_legacy_ssl_context(),
            follow_redirects=True,
        )

    @staticmethod
    def _state(html: str) -> dict[str, str]:
        state: dict[str, str] = {}
        for name in _STATE_FIELDS:
            match = re.search(rf'name="{name}"[^>]*value="([^"]*)"', html)
            if match:
                state[name] = unescape(match.group(1))
        return state

    @staticmethod
    def _filter_fields(
        radio: str,
        query: str,
        karar_no: str,
        basvuran: str,
        idare_adi: str,
        subject: str,
        date_start: str,
        date_end: str,
        year: str,
    ) -> dict[str, str]:
        return {
            f"{_PREFIX}kurulKararTip": radio,
            f"{_PREFIX}txtKararNo": karar_no,
            f"{_PREFIX}etKararTarihBaslangic$EkapTakvimTextBox_etKararTarihBaslangic": date_start,
            f"{_PREFIX}etKararTarihBitis$EkapTakvimTextBox_etKararTarihBitis": date_end,
            f"{_PREFIX}txtResmiGazeteSayisi": "",
            f"{_PREFIX}etResmiGazeteTarihi$EkapTakvimTextBox_etResmiGazeteTarihi": "",
            f"{_PREFIX}txtBasvuruKonusuIhale": subject,
            f"{_PREFIX}txtSikayetci": basvuran,
            f"{_PREFIX}txtIhaleyiYapanIdare": idare_adi,
            # ddlYil validates against its option set; "0" is the "all years" option.
            f"{_PREFIX}ddlYil": year or "0",
            f"{_PREFIX}txtKararMetni": query,
        }

    def _post(self, fields: dict[str, str], html: str, event_target: str) -> httpx.Response:
        body = dict(fields)
        body.update(self._state(html))
        body["__EVENTTARGET"] = event_target
        body["__EVENTARGUMENT"] = ""
        headers = {
            "Content-Type": "application/x-www-form-urlencoded",
            "Referer": f"{self.base_url}{_SEARCH_PATH}",
            "Origin": self.base_url,
        }
        resp = self.post_form(_SEARCH_PATH, body, headers=headers)
        if "Sayfada hata oluştu" in resp.text:
            raise KikSearchError(
                "The KİK search portal returned an error page; the form layout "
                "may have changed or the request was rejected."
            )
        return resp

    @staticmethod
    def _parse_rows(html: str) -> list[dict[str, str]]:
        grid = _GRID_RE.search(html)
        if not grid:
            return []
        rows: list[dict[str, str]] = []
        for tr in _ROW_RE.findall(grid.group(0)):
            cells = _CELL_RE.findall(tr)
            if len(cells) < 6:
                continue
            preview = _PREVIEW_RE.search(cells[0])
            rows.append(
                {
                    "karar_no": _clean(cells[1]),
                    "date": _clean(cells[2]),
                    "idare": _clean(cells[3]),
                    "sikayetci": _clean(cells[4]),
                    "subject": _clean(cells[5]),
                    "preview": unescape(preview.group(1)) if preview else "",
                }
            )
        return rows

    @staticmethod
    def _last_visible_page(html: str) -> int:
        grid = _GRID_RE.search(html)
        if not grid:
            return 1
        pager = _PAGER_RE.search(grid.group(0))
        if not pager:
            return 1
        numbers = [int(num) for _, num in _PAGE_LINK_RE.findall(pager.group(0))]
        current = re.findall(r"<span[^>]*>(\d+)</span>", pager.group(0))
        numbers.extend(int(value) for value in current)
        return max(numbers) if numbers else 1

    @staticmethod
    def _page_target(html: str, target_page: int) -> str | None:
        grid = _GRID_RE.search(html)
        if not grid:
            return None
        pager = _PAGER_RE.search(grid.group(0))
        if not pager:
            return None
        for target, num in _PAGE_LINK_RE.findall(pager.group(0)):
            if int(num) == target_page:
                return unescape(target)
        return None

    def _resolve_url(self, fields: dict[str, str], html: str, preview_target: str) -> str:
        """Fire a row's preview postback to recover its KararGoster permalink."""
        if not preview_target:
            return ""
        try:
            resp = self._post(fields, html, preview_target)
        except (KikSearchError, httpx.HTTPError):
            return ""
        match = _KARAR_ID_RE.search(resp.text)
        return f"{_DOCUMENT_URL}{match.group(1)}" if match else ""

    def search(
        self,
        decision_type: str,
        query: str,
        *,
        limit: int = 10,
        page: int = 1,
        karar_no: str = "",
        basvuran: str = "",
        idare_adi: str = "",
        subject: str = "",
        date_start: str = "",
        date_end: str = "",
        year: str = "",
        resolve_urls: bool = True,
    ) -> dict[str, Any]:
        if decision_type not in _DECISION_TYPES:
            valid = ", ".join(_DECISION_TYPES)
            raise ValueError(f"decision_type must be one of: {valid}")

        page = max(1, page)
        limit = max(1, limit)
        radio, label = _DECISION_TYPES[decision_type]
        fields = self._filter_fields(
            radio,
            query,
            karar_no,
            basvuran,
            idare_adi,
            subject,
            date_start,
            date_end,
            year,
        )

        seed = self.get_text(_SEARCH_PATH)
        current = self._post(fields, seed.text, _SEARCH_BUTTON)

        # The grid pages server-side at a fixed 10 rows; walk the pager to the
        # requested page, replaying each response's fresh WebForms state.
        for target_page in range(2, page + 1):
            target = self._page_target(current.text, target_page)
            if target is None:
                break
            current = self._post(fields, current.text, target)

        last_page = self._last_visible_page(current.text)
        total = last_page * _PAGE_SIZE
        rows = self._parse_rows(current.text)[:limit]
        results = [
            self._to_result(row, label, current.text, fields, resolve_urls)
            for row in rows
        ]
        return {
            "total": total,
            "page": page,
            "count": len(results),
            "results": results,
        }

    def _to_result(
        self,
        row: dict[str, str],
        label: str,
        html: str,
        fields: dict[str, str],
        resolve_urls: bool,
    ) -> dict[str, Any]:
        url = self._resolve_url(fields, html, row["preview"]) if resolve_urls else ""
        title = row["subject"] or row["idare"] or row["karar_no"]
        return {
            "id": row["karar_no"],
            "title": title,
            "decisionType": label,
            "date": _iso_date(row["date"]),
            "url": url,
        }


def run_tool(
    query: str,
    decision_type: str = "uyusmazlik",
    limit: int = 10,
    page: int = 1,
    karar_no: str = "",
    basvuran: str = "",
    idare_adi: str = "",
    subject: str = "",
    date_start: str = "",
    date_end: str = "",
    year: str = "",
) -> dict[str, Any]:
    """Search KİK board decisions.

    query        : free-text decision-content search (KararMetni)
    decision_type : decision family — "uyusmazlik" (default), "duzenleyici", "mahkeme"
    limit        : results per page (>= 1)
    page         : 1-indexed page
    karar_no     : optional decision number, e.g. "2025/UH.II-1801"
    basvuran     : optional applicant/complainant name
    idare_adi    : optional administration (procuring entity) name
    subject      : optional tender-subject phrase (BasvuruKonusuIhale)
    date_start   : optional start date (DD.MM.YYYY)
    date_end     : optional end date (DD.MM.YYYY)
    year         : optional decision year (e.g. "2024")

    Returns {"total", "page", "count", "results": [{id, title, decisionType, date, url}]}.
    """
    with KikClient() as client:
        return client.search(
            decision_type,
            query,
            limit=limit,
            page=page,
            karar_no=karar_no,
            basvuran=basvuran,
            idare_adi=idare_adi,
            subject=subject,
            date_start=date_start,
            date_end=date_end,
            year=year,
        )
