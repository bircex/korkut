"""Unit tests for search_kvkk (Tavily discovery mocked with respx)."""

import httpx
import pytest
import respx

from tools._base.search_providers import MissingSearchKey
from tools.search_kvkk.client import run_tool

TAVILY_URL = "https://api.tavily.com/search"

SAMPLE = {
    "results": [
        {
            "title": "Karar Özeti 23/12/2021 2021/1303",
            "url": "https://www.kvkk.gov.tr/Icerik/7288/2021-1303",
            "content": "Açık rıza hakkında karar özeti metni.",
        }
    ]
}


@respx.mock
def test_search_parses_tavily_results(monkeypatch):
    monkeypatch.setenv("TAVILY_API_KEY", "test-key")
    route = respx.post(TAVILY_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    out = run_tool(query="açık rıza", limit=5)
    assert route.called
    row = out["results"][0]
    assert row["id"] == "7288/2021-1303"
    assert row["date"] == "23/12/2021"
    assert row["decisionNumber"] == "2021/1303"
    assert row["url"].endswith("/Icerik/7288/2021-1303")


@respx.mock
def test_search_restricts_to_kvkk_domain(monkeypatch):
    monkeypatch.setenv("TAVILY_API_KEY", "test-key")
    route = respx.post(TAVILY_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    run_tool(query="açık rıza")
    import json

    body = json.loads(route.calls.last.request.content)
    assert body["include_domains"] == ["kvkk.gov.tr"]
    assert "karar özeti" in body["query"]


def test_missing_key_raises(monkeypatch):
    monkeypatch.delenv("TAVILY_API_KEY", raising=False)
    with pytest.raises(MissingSearchKey):
        run_tool(query="x")
