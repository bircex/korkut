"""Unit tests for search_yargitay (Bedesten API mocked with respx)."""

import httpx
import respx

from tools.search_yargitay.client import run_tool

SEARCH_URL = "https://bedesten.adalet.gov.tr/emsal-karar/searchDocuments"

SAMPLE = {
    "data": {
        "emsalKararList": [
            {
                "documentId": "doc-1",
                "birimAdi": "1. Hukuk Dairesi",
                "esasNo": "2023/123",
                "kararNo": "2023/456",
                "kararTarihiStr": "2023-05-10",
                "kararTuru": "KARAR",
            }
        ],
        "total": 1,
        "start": 0,
    },
    "metadata": {"FMTY": "SUCCESS"},
}


@respx.mock
def test_search_parses_results():
    route = respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    out = run_tool(query="kira tespiti", limit=5)
    assert route.called
    assert out["total"] == 1
    assert out["count"] == 1
    first = out["results"][0]
    assert first["documentId"] == "doc-1"
    assert first["court"] == "1. Hukuk Dairesi"
    assert first["caseNo"] == "2023/123"


@respx.mock
def test_search_sends_correct_court_type_and_dates():
    route = respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    run_tool(query="mülkiyet", date_start="2023-01-01", date_end="2023-12-31")
    sent = route.calls.last.request
    import json

    body = json.loads(sent.content)
    assert body["data"]["itemTypeList"] == ["YARGITAYKARARI"]
    assert body["data"]["kararTarihiStart"] == "2023-01-01T00:00:00.000Z"
    assert body["data"]["kararTarihiEnd"] == "2023-12-31T23:59:59.999Z"


@respx.mock
def test_empty_results():
    respx.post(SEARCH_URL).mock(
        return_value=httpx.Response(200, json={"data": None, "metadata": {}})
    )
    out = run_tool(query="zzz")
    assert out["total"] == 0
    assert out["results"] == []
