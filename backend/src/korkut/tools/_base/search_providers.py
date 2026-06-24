"""Web-search provider used to discover decisions on sources without a real API.

A few Turkish authorities (KVKK, BDDK, Sigorta Tahkim) publish decisions on plain
web pages or PDF journals with no searchable backend. We discover them by querying
the Tavily Search API restricted to the authority's domain, then fetch the documents
directly. A single provider keeps the whole toolset down to one key.

`TavilyClient` builds on `BaseClient` (rate limiter, 429 back-pressure, cache) and
reads its key from ``TAVILY_API_KEY`` in the environment.

A tool that uses it declares ``requires_key: TAVILY_API_KEY`` in its ``tool.yaml``;
the registry hides the tool when the key is absent, so this client assumes the key is
present and raises a clear error if it is not.
"""

from __future__ import annotations

import os
from typing import Any

from .client import BaseClient


class MissingSearchKey(RuntimeError):
    """Raised when a search-provider API key is not set in the environment."""


class TavilyClient(BaseClient):
    """Minimal client for the Tavily Search API (``api.tavily.com``)."""

    base_url = "https://api.tavily.com"
    SEARCH = "/search"
    ENV_KEY = "TAVILY_API_KEY"

    def __init__(self, **kwargs: Any) -> None:
        super().__init__(**kwargs)
        key = os.getenv(self.ENV_KEY)
        if not key:
            raise MissingSearchKey(f"{self.ENV_KEY} is not set")
        self._client.headers["Content-Type"] = "application/json"
        self._client.headers["Authorization"] = f"Bearer {key}"

    def search(
        self,
        query: str,
        *,
        include_domains: list[str] | None = None,
        max_results: int = 10,
        search_depth: str = "advanced",
        country: str = "turkey",
    ) -> dict[str, Any]:
        body: dict[str, Any] = {
            "query": query,
            "country": country,
            "max_results": max_results,
            "search_depth": search_depth,
        }
        if include_domains:
            body["include_domains"] = include_domains
        return self.post_json(self.SEARCH, body)
