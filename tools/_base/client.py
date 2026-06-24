"""Shared HTTP plumbing for Korkut's data-source tools.

Every source tool builds on `BaseClient`: a small synchronous httpx wrapper with
a token-bucket rate limiter, HTTP 429 back-pressure (honours `Retry-After`), and
an optional in-process response cache. This keeps each source client thin — it
only declares its endpoint and maps params/results.

All sources accessed here are public Turkish government portals; no auth/token.
"""

from __future__ import annotations

import io
import math
import ssl
import threading
import time
from dataclasses import dataclass, field
from html.parser import HTMLParser
from typing import Any

import httpx


def legacy_ssl_context() -> ssl.SSLContext:
    """An SSL context tolerant of legacy Turkish government TLS stacks.

    Several portals (KİK, Uyuşmazlık, BDDK, Sigorta Tahkim) serve certificates or
    cipher suites that modern OpenSSL rejects. Clients for those hosts build their
    httpx transport with this context (verification disabled, legacy renegotiation
    and a relaxed security level enabled).
    """
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


class RateLimited(Exception):
    """Raised when the local bucket would block longer than `max_wait`."""

    def __init__(self, retry_after: float) -> None:
        self.retry_after = retry_after
        super().__init__(f"local rate-limit bucket would block {retry_after:.1f}s")


@dataclass
class TokenBucket:
    """Thread-safe token bucket with 429 back-pressure.

    Defaults are tuned for the measured Bedesten limit (~10 req / 30s per IP →
    ~1 token / 3s). A single bucket per host instance is shared by all calls in
    the same process.
    """

    capacity: float = 1.0
    refill_per_s: float = 1.0 / 3.5  # ~1 token every 3.5s (≈14% safety margin)
    _tokens: float = field(default=0.0, init=False)
    _last: float = field(default_factory=time.monotonic, init=False)
    _not_before: float = field(default=0.0, init=False)
    _lock: threading.Lock = field(default_factory=threading.Lock, init=False)

    def __post_init__(self) -> None:
        self._tokens = self.capacity

    def acquire(self, max_wait: float = 30.0) -> None:
        deadline = time.monotonic() + max_wait
        while True:
            with self._lock:
                now = time.monotonic()
                if now < self._not_before:
                    wait_s = self._not_before - now
                else:
                    self._tokens = min(
                        self.capacity, self._tokens + (now - self._last) * self.refill_per_s
                    )
                    self._last = now
                    if self._tokens >= 1.0:
                        self._tokens -= 1.0
                        return
                    wait_s = (1.0 - self._tokens) / self.refill_per_s
            if time.monotonic() + wait_s > deadline:
                raise RateLimited(retry_after=wait_s)
            time.sleep(wait_s)

    def penalize(self, retry_after: float) -> None:
        retry_after = max(1.0, min(retry_after, 60.0))
        with self._lock:
            self._not_before = max(self._not_before, time.monotonic() + retry_after + 0.5)
            self._tokens = 0.0
            self._last = time.monotonic()


class BaseClient:
    """Thin synchronous HTTP client shared by all source tools."""

    base_url: str = ""
    # Browser-like headers; public portals expect them. No auth.
    default_headers: dict[str, str] = {
        "Accept": "*/*",
        "Accept-Language": "tr-TR,tr;q=0.9,en;q=0.8",
        "User-Agent": (
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
            "(KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36"
        ),
    }

    def __init__(
        self,
        *,
        timeout: float = 60.0,
        bucket: TokenBucket | None = None,
        cache: bool = True,
    ) -> None:
        self._client = httpx.Client(
            base_url=self.base_url, headers=self.default_headers, timeout=timeout
        )
        self._bucket = bucket or TokenBucket()
        self._cache: dict[str, Any] | None = {} if cache else None

    def _cache_key(self, method: str, endpoint: str, payload: Any) -> str:
        return f"{method}:{endpoint}:{payload!r}"

    def _request(
        self,
        method: str,
        endpoint: str,
        *,
        json: Any = None,
        params: Any = None,
        data: Any = None,
        headers: dict[str, str] | None = None,
    ) -> httpx.Response:
        key = self._cache_key(method, endpoint, json if json is not None else (data if data is not None else params))
        if self._cache is not None and key in self._cache:
            return self._cache[key]
        for attempt in range(3):
            self._bucket.acquire()
            resp = self._client.request(method, endpoint, json=json, params=params, data=data, headers=headers)
            if resp.status_code == 429:
                retry_after = _parse_retry_after(resp.headers.get("Retry-After"))
                self._bucket.penalize(retry_after)
                if attempt < 2:
                    continue
            resp.raise_for_status()
            if self._cache is not None:
                self._cache[key] = resp
            return resp
        resp.raise_for_status()
        return resp

    def post_json(self, endpoint: str, payload: Any) -> Any:
        return self._request("POST", endpoint, json=payload).json()

    def post_form(self, endpoint: str, fields: Any, *, headers: dict[str, str] | None = None) -> httpx.Response:
        """POST application/x-www-form-urlencoded data; return the raw response."""
        return self._request("POST", endpoint, data=fields, headers=headers)

    def get_text(self, endpoint: str, params: Any = None) -> httpx.Response:
        """GET and return the raw response (used for session/CSRF bootstrap)."""
        return self._request("GET", endpoint, params=params)

    def get_json(self, endpoint: str, params: Any = None) -> Any:
        return self._request("GET", endpoint, params=params).json()

    def get_bytes(self, endpoint: str, params: Any = None) -> tuple[bytes, str]:
        resp = self._request("GET", endpoint, params=params)
        return resp.content, resp.headers.get("Content-Type", "")

    def close(self) -> None:
        self._client.close()

    def __enter__(self) -> "BaseClient":
        return self

    def __exit__(self, *exc: object) -> None:
        self.close()


def _parse_retry_after(raw: str | None) -> float:
    try:
        return float(raw) if raw else 30.0
    except (TypeError, ValueError):
        return 30.0


def html_to_markdown(html: str) -> str:
    """Convert an HTML legal document to clean Markdown."""
    from markdownify import markdownify

    return markdownify(html, heading_style="ATX").strip()


def pdf_to_markdown(data: bytes) -> str:
    """Extract text from a PDF document as Markdown-ish plain text."""
    from pypdf import PdfReader

    reader = PdfReader(io.BytesIO(data))
    return "\n\n".join((page.extract_text() or "").strip() for page in reader.pages).strip()


def chunk_markdown(markdown: str, page: int = 1, page_size: int = 5000) -> dict[str, Any]:
    """Slice a long Markdown document into a fixed-size page.

    Some sources (PDF journals, long HTML rulings) produce documents too large to
    return whole. This returns one ``page_size``-character chunk plus pagination
    metadata, mirroring the page model the search tools use:

        {"markdown": <chunk>, "page": <clamped>, "total_pages": <n>}

    `page` is 1-indexed and clamped into range.
    """
    text = markdown or ""
    total_pages = max(1, math.ceil(len(text) / page_size)) if text else 1
    page = max(1, min(page, total_pages))
    start = (page - 1) * page_size
    return {"markdown": text[start : start + page_size], "page": page, "total_pages": total_pages}


class _DivExtractor(HTMLParser):
    """Capture the inner HTML of the first ``<div>`` carrying ``target_class``.

    Re-serialises the tags/text/entities it sees while inside the matched div so
    the captured fragment can be handed straight to `html_to_markdown`. Nested
    divs are tracked by depth so capture ends at the matching close tag.
    """

    def __init__(self, target_class: str) -> None:
        super().__init__(convert_charrefs=False)
        self._target = target_class
        self._depth = 0
        self._capture_depth: int | None = None
        self.parts: list[str] = []

    def _attrs(self, attrs: list[tuple[str, str | None]]) -> str:
        return "".join(f' {k}="{v}"' if v is not None else f" {k}" for k, v in attrs)

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if self._capture_depth is None and tag == "div":
            classes = (dict(attrs).get("class") or "").split()
            if self._target in classes:
                self._capture_depth = self._depth
                self._depth += 1
                return
        if self._capture_depth is not None:
            self.parts.append(f"<{tag}{self._attrs(attrs)}>")
        self._depth += 1

    def handle_startendtag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if self._capture_depth is not None:
            self.parts.append(f"<{tag}{self._attrs(attrs)}/>")

    def handle_endtag(self, tag: str) -> None:
        self._depth -= 1
        if self._capture_depth is not None and self._depth == self._capture_depth:
            self._capture_depth = None
            return
        if self._capture_depth is not None:
            self.parts.append(f"</{tag}>")

    def handle_data(self, data: str) -> None:
        if self._capture_depth is not None:
            self.parts.append(data)

    def handle_entityref(self, name: str) -> None:
        if self._capture_depth is not None:
            self.parts.append(f"&{name};")

    def handle_charref(self, name: str) -> None:
        if self._capture_depth is not None:
            self.parts.append(f"&#{name};")


def extract_div_html(html: str, class_name: str) -> str:
    """Return the inner HTML of the first ``<div class="… class_name …">``.

    Falls back to the whole document when no matching div is found, so callers can
    always hand the result to `html_to_markdown`.
    """
    parser = _DivExtractor(class_name)
    parser.feed(html)
    captured = "".join(parser.parts).strip()
    return captured or html
