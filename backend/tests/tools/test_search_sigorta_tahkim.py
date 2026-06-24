"""Unit tests for search_sigorta_tahkim (Tavily discovery mocked with respx)."""

import json

import httpx
import pytest
import respx

from korkut.tools._base.search_providers import MissingSearchKey
from korkut.tools.search_sigorta_tahkim.client import run_tool

TAVILY_URL = "https://api.tavily.com/search"

SAMPLE = {
    "results": [
        {
            "title": "Hakem Karar Dergisi 64",
            "url": "https://www.sigortatahkim.org/content/CmsFiles/karardrgs64.pdf",
            "content": "Değer kaybı uyuşmazlıkları ...",
        }
    ]
}


@respx.mock
def test_search_extracts_issue_number(monkeypatch):
    monkeypatch.setenv("TAVILY_API_KEY", "test-key")
    respx.post(TAVILY_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    out = run_tool(query="değer kaybı", limit=5)
    assert out["results"][0]["id"] == "64"


@respx.mock
def test_search_restricts_domain(monkeypatch):
    monkeypatch.setenv("TAVILY_API_KEY", "test-key")
    route = respx.post(TAVILY_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    run_tool(query="x")
    body = json.loads(route.calls.last.request.content)
    assert body["include_domains"] == ["sigortatahkim.org"]


def test_missing_key_raises(monkeypatch):
    monkeypatch.delenv("TAVILY_API_KEY", raising=False)
    with pytest.raises(MissingSearchKey):
        run_tool(query="x")
