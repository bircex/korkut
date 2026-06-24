"""Client for the Uyuşmazlık Mahkemesi (Court of Jurisdictional Disputes) portal.

The court resolves jurisdictional ("görev") and judgment ("hüküm") conflicts between the
judicial and administrative court orders. Its public portal at ``kararlar.uyusmazlik.gov.tr``
is a classic ASP.NET WebForms search page:

  * a GET of ``/`` seeds the ``__VIEWSTATE`` / ``__VIEWSTATEGENERATOR`` / ``__EVENTVALIDATION``
    tokens;
  * a POST that replays those tokens with ``txtSearch`` (the query), ``rblSearchScope`` (All /
    EsasNo / KararNo) and ``btnSearch=Ara`` runs the search and returns a server-rendered
    ``GridView1`` (20 rows/page): Esas No | Karar No | Karar Tarihi | İşlemler;
  * each row's İşlemler cell links straight to the decision PDF under ``/Uploads/<no>.pdf`` —
    there is no separate HTML decision page, so ``fetch_uyusmazlik`` downloads that PDF;
  * server-side paging fires ``__doPostBack('GridView1','Page$N')``; the total is printed in
    ``lblSearchStatus`` ("Arama sonuçları: N dosya bulundu.").

The host serves a certificate modern stacks reject, so this client disables verification (like
the KİK client) while reusing every other piece of ``BaseClient`` (bucket, 429 back-pressure,
cache, retries). Cookies must persist across the GET/POST handshake.

Shared by ``search_uyusmazlik`` and ``fetch_uyusmazlik``.
"""

from __future__ import annotations

import re
from html import unescape
from typing import Any, ClassVar
from urllib.parse import urljoin

import httpx

from .client import BaseClient, legacy_ssl_context, pdf_to_markdown

BASE_URL = "https://kararlar.uyusmazlik.gov.tr"
_ROOT = "/"

# Search scope → rblSearchScope value.
SCOPES: dict[str, str] = {
    "all": "All",
    "": "All",
    "esas": "EsasNo",
    "karar": "KararNo",
}

# Hidden WebForms state fields replayed from each response into the next POST.
_STATE_FIELDS = ("__VIEWSTATE", "__VIEWSTATEGENERATOR", "__EVENTVALIDATION")

_GRID_RE = re.compile(r'<table[^>]*id="GridView1".*?</table>', re.S | re.I)
_ROW_RE = re.compile(r"<tr[^>]*>(.*?)</tr>", re.S | re.I)
_CELL_RE = re.compile(r"<t[dh][^>]*>(.*?)</t[dh]>", re.S | re.I)
_PDF_HREF_RE = re.compile(r"""href=['"]([^'"]+\.pdf)['"]""", re.I)
_PAGE_LINK_RE = re.compile(r"__doPostBack\(&#39;([^&]+)&#39;,&#39;(Page\$\d+)&#39;\)")
_TOTAL_RE = re.compile(r"Arama sonuçları:\s*(\d+)\s*dosya", re.I)
_CASE_NO_RE = re.compile(r"\d+\s*/\s*\d+")
_TAG_RE = re.compile(r"<[^>]+>")
_WS_RE = re.compile(r"\s+")

# The GridView renders a fixed 20 rows per page.
_PAGE_SIZE = 20


def _clean(html: str) -> str:
    return unescape(_WS_RE.sub(" ", _TAG_RE.sub(" ", html)).strip())


class UyusmazlikClient(BaseClient):
    base_url = BASE_URL
    default_headers: ClassVar[dict[str, str]] = {
        **BaseClient.default_headers,
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Accept-Language": "tr-TR,tr;q=0.9",
        "Origin": BASE_URL,
        "Referer": BASE_URL + "/",
    }

    def __init__(self, *, timeout: float = 60.0, **kwargs: Any) -> None:
        super().__init__(timeout=timeout, **kwargs)
        # The host's certificate chain is rejected by modern OpenSSL; swap in a
        # tolerant transport while keeping the shared bucket/cache/retry loop.
        # Cookies must persist across the GET/POST WebForms handshake.
        self._client.close()
        self._client = httpx.Client(
            base_url=self.base_url,
            headers=self.default_headers,
            timeout=timeout,
            verify=legacy_ssl_context(),
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

    def _post(self, html: str, fields: dict[str, str]) -> httpx.Response:
        body = {"__EVENTTARGET": "", "__EVENTARGUMENT": "", **self._state(html), **fields}
        headers = {"Content-Type": "application/x-www-form-urlencoded"}
        return self.post_form(_ROOT, body, headers=headers)

    def search(
        self,
        query: str,
        *,
        scope: str = "all",
        limit: int = 10,
        page: int = 1,
        case_sensitive: bool = False,
    ) -> dict[str, Any]:
        scope_value = SCOPES.get(scope.strip().lower())
        if scope_value is None:
            raise ValueError(f"unknown scope {scope!r}; allowed: all, esas, karar")

        page = max(1, page)
        limit = max(1, limit)
        search_fields = {
            "txtSearch": query or "",
            "rblSearchScope": scope_value,
            "btnSearch": "Ara",
        }
        if case_sensitive:
            search_fields["chkCaseSensitive"] = "on"

        seed = self.get_text(_ROOT).text
        current = self._post(seed, search_fields).text

        # Walk server-side paging to the requested page, replaying fresh state.
        for target_page in range(2, page + 1):
            postback = self._page_postback(current, target_page)
            if postback is None:
                break
            event_target, event_argument = postback
            current = self._post(
                current,
                {
                    "__EVENTTARGET": event_target,
                    "__EVENTARGUMENT": event_argument,
                    "txtSearch": query or "",
                    "rblSearchScope": scope_value,
                },
            ).text

        total_match = _TOTAL_RE.search(current)
        total = int(total_match.group(1)) if total_match else None
        rows = self._parse_rows(current)[:limit]
        return {
            "total": total if total is not None else len(rows),
            "page": page,
            "count": len(rows),
            "results": rows,
        }

    @staticmethod
    def _page_postback(html: str, target_page: int) -> tuple[str, str] | None:
        grid = _GRID_RE.search(html)
        if not grid:
            return None
        for event_target, page_arg in _PAGE_LINK_RE.findall(grid.group(0)):
            if page_arg == f"Page${target_page}":
                return unescape(event_target), page_arg
        return None

    @staticmethod
    def _parse_rows(html: str) -> list[dict[str, Any]]:
        grid = _GRID_RE.search(html)
        if not grid:
            return []
        results: list[dict[str, Any]] = []
        for tr in _ROW_RE.findall(grid.group(0)):
            cells = _CELL_RE.findall(tr)
            if len(cells) < 4:
                continue
            esas_no = _clean(cells[0])
            # Skip the header and pager rows (data rows start with an "esas/karar" number).
            if not _CASE_NO_RE.fullmatch(esas_no.replace(" ", "")):
                continue
            pdf = _PDF_HREF_RE.search(cells[3])
            url = urljoin(BASE_URL + "/", unescape(pdf.group(1))) if pdf else None
            karar_no = _clean(cells[1])
            results.append(
                {
                    "id": karar_no or esas_no,
                    "esasNo": esas_no,
                    "kararNo": karar_no,
                    "date": _clean(cells[2]),
                    "url": url,
                }
            )
        return results

    def document_markdown(self, document_url: str) -> dict[str, Any]:
        raw, content_type = self.get_bytes(document_url)
        if "pdf" in content_type.lower() or document_url.lower().endswith(".pdf"):
            markdown = pdf_to_markdown(raw)
        else:
            from .client import html_to_markdown

            markdown = html_to_markdown(raw.decode("utf-8", errors="replace"))
        return {"sourceUrl": document_url, "markdown": markdown}
