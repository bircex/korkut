"""Unit tests for search_gib (GİB özelge API mocked with respx)."""

import json

import httpx
import respx

from tools.search_gib.client import run_tool

SEARCH_URL = "https://gib.gov.tr/api/gibportal/mevzuat/ozelge/list"

SAMPLE = {
    "resultContainer": {
        "content": [
            {
                "id": 12345,
                "ozelgeNo": "E-40247694-130-15524",
                "ozelgeTarih": "2023-05-10T00:00:00.000Z",
                "title": "KDV istisnası uygulaması hakkında",
                "kanunNo": "3065",
                "kanunTitle": "KATMA DEĞER VERGİSİ KANUNU",
                "siteLink": "https://www.gib.gov.tr/node/12345",
            }
        ],
        "totalElements": 1,
        "totalPages": 1,
    }
}


@respx.mock
def test_search_parses_results():
    route = respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    out = run_tool(query="kdv istisnası", limit=5)
    assert route.called
    assert out["total"] == 1
    assert out["page"] == 1
    assert out["count"] == 1
    first = out["results"][0]
    assert first["id"] == "12345"
    assert first["title"] == "KDV istisnası uygulaması hakkında"
    assert first["type"] == "ozelge"
    assert first["date"] == "2023-05-10T00:00:00.000Z"
    assert first["url"] == "https://www.gib.gov.tr/node/12345"


@respx.mock
def test_search_sends_filters_and_pagination():
    route = respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    run_tool(query="stopaj", limit=20, page=3)
    request = route.calls.last.request

    body = json.loads(request.content)
    assert body["status"] == 2
    assert body["deleted"] is False
    assert body["ktype"] == 99
    assert body["title"] == "stopaj"
    assert body["kanunNo"] == "stopaj"
    assert body["description"] == "stopaj"

    assert dict(request.url.params)["page"] == "2"  # 1-indexed -> 0-indexed
    assert dict(request.url.params)["size"] == "20"
    assert dict(request.url.params)["sortFieldName"] == "ozelgeTarih"


@respx.mock
def test_empty_results():
    respx.post(SEARCH_URL).mock(
        return_value=httpx.Response(200, json={"resultContainer": {"content": []}})
    )
    out = run_tool(query="zzz")
    assert out["total"] == 0
    assert out["count"] == 0
    assert out["results"] == []


@respx.mock
def test_missing_container():
    respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json={}))
    out = run_tool(query="zzz")
    assert out["total"] == 0
    assert out["results"] == []
