"""Unit tests for search_bddk (Tavily discovery mocked with respx)."""

import json

import httpx
import pytest
import respx

from korkut.tools._base.search_providers import MissingSearchKey
from korkut.tools.search_bddk.client import run_tool

TAVILY_URL = "https://api.tavily.com/search"

SAMPLE = {
    "results": [
        {
            "title": "[PDF] BDDK Kurul Kararı",
            "url": "https://www.bddk.org.tr/Mevzuat/DokumanGetir/310",
            "content": "Karar Sayısı: 6862 ...",
        },
        {
            "title": "İlgisiz sayfa",
            "url": "https://www.bddk.org.tr/Hakkimizda",
            "content": "no id here",
        },
    ]
}


@respx.mock
def test_search_extracts_document_id(monkeypatch):
    monkeypatch.setenv("TAVILY_API_KEY", "test-key")
    respx.post(TAVILY_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    out = run_tool(query="sermaye yeterliliği", limit=5)
    # Only the result with an extractable document id is kept.
    assert out["count"] == 1
    row = out["results"][0]
    assert row["id"] == "310"
    assert row["title"] == "BDDK Kurul Kararı"


@respx.mock
def test_search_restricts_domain(monkeypatch):
    monkeypatch.setenv("TAVILY_API_KEY", "test-key")
    route = respx.post(TAVILY_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    run_tool(query="x")
    body = json.loads(route.calls.last.request.content)
    assert body["include_domains"] == ["https://www.bddk.org.tr/Mevzuat/DokumanGetir"]


def test_missing_key_raises(monkeypatch):
    monkeypatch.delenv("TAVILY_API_KEY", raising=False)
    with pytest.raises(MissingSearchKey):
        run_tool(query="x")
