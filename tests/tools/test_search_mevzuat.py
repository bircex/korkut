"""Unit tests for search_mevzuat (Bedesten mevzuat API mocked with respx)."""

import json

import httpx
import respx

from tools.search_mevzuat.client import run_tool

SEARCH_URL = "https://bedesten.adalet.gov.tr/mevzuat/searchDocuments"

SAMPLE = {
    "data": {
        "mevzuatList": [
            {
                "mevzuatId": "abc123",
                "mevzuatNo": "6098",
                "mevzuatAdi": "TÜRK BORÇLAR KANUNU",
                "mevzuatTur": "KANUN",
                "mevzuatTertip": "5",
                "resmiGazeteTarihi": "2011-02-04",
                "resmiGazeteSayisi": "27836",
            }
        ],
        "total": 3,
        "start": 0,
    },
    "metadata": {"FMTY": "SUCCESS"},
}


@respx.mock
def test_search_parses_results():
    route = respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    out = run_tool(query="türk borçlar kanunu", limit=5)
    assert route.called
    assert out["total"] == 3
    assert out["count"] == 1
    first = out["results"][0]
    assert first["id"] == "abc123"
    assert first["title"] == "TÜRK BORÇLAR KANUNU"
    assert first["type"] == "KANUN"
    assert first["number"] == "6098"
    assert first["date"] == "2011-02-04"
    assert first["url"] == "https://mevzuat.adalet.gov.tr/mevzuat?mevzuatId=abc123"


@respx.mock
def test_search_sends_query_and_type_filter():
    route = respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    run_tool(query="kira", legislation_type="kanun, khk")
    sent = route.calls.last.request
    body = json.loads(sent.content)
    assert body["data"]["mevzuatAdi"] == "kira"
    assert body["data"]["mevzuatTurList"] == ["KANUN", "KHK"]
    assert body["data"]["pageSize"] == 10
    assert body["paging"] is True


@respx.mock
def test_no_type_filter_omits_field():
    route = respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    run_tool(query="vergi")
    body = json.loads(route.calls.last.request.content)
    assert "mevzuatTurList" not in body["data"]


@respx.mock
def test_empty_results():
    respx.post(SEARCH_URL).mock(
        return_value=httpx.Response(200, json={"data": None, "metadata": {"FMTY": "SUCCESS"}})
    )
    out = run_tool(query="zzz")
    assert out["total"] == 0
    assert out["results"] == []
