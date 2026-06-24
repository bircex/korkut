"""Unit tests for search_kyb (Bedesten API mocked with respx)."""

import json

import httpx
import respx

from korkut.tools.search_kyb.client import run_tool

SEARCH_URL = "https://bedesten.adalet.gov.tr/emsal-karar/searchDocuments"

SAMPLE = {
    "data": {
        "emsalKararList": [
            {
                "documentId": "kyb-1",
                "birimAdi": "8. Ceza Dairesi",
                "esasNo": "2023/2484",
                "kararNo": "2024/9115",
                "kararTarihiStr": "2024-11-28",
                "kararTuru": "KARAR",
            }
        ],
        "total": 1,
    },
    "metadata": {"FMTY": "SUCCESS"},
}


@respx.mock
def test_search_parses_results():
    route = respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    out = run_tool(query="zincirleme suç", limit=3)
    assert route.called
    assert out["total"] == 1
    assert out["results"][0]["documentId"] == "kyb-1"


@respx.mock
def test_search_sends_kyb_item_type():
    route = respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    run_tool(query="x", date_start="2024-01-01", date_end="2024-12-31")
    body = json.loads(route.calls.last.request.content)
    assert body["data"]["itemTypeList"] == ["KYB"]
    assert body["data"]["kararTarihiStart"] == "2024-01-01T00:00:00.000Z"
    assert body["data"]["kararTarihiEnd"] == "2024-12-31T23:59:59.999Z"
