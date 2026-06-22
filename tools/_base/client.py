"""Shared HTTP plumbing for Korkut's data-source tools.

Every source tool builds on `BaseClient`: a small synchronous httpx wrapper with
a token-bucket rate limiter, HTTP 429 back-pressure (honours `Retry-After`), and
an optional in-process response cache. This keeps each source client thin — it
only declares its endpoint and maps params/results.

All sources accessed here are public Turkish government portals; no auth/token.
"""

from __future__ import annotations

import io
import threading
import time
from dataclasses import dataclass, field
from typing import Any

import httpx


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
