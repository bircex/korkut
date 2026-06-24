"""Unit tests for search_anayasa (Constitutional Court JSON API mocked with respx)."""

import json

import httpx
import respx

from korkut.tools.search_anayasa.client import run_tool

SEARCH_URL = "https://normkararlarbilgibankasi.anayasa.gov.tr/api/core/public/search"

NORM_BODY = {
    "total": 2,
    "page": 1,
    "page_size": 5,
    "data": [
        {
            "id": "95d2e3dc-1a3f-3834-b965-ea1c0936364f",
            "kararTipi": "NormDenetimi",
            "esasNo": "2023/12",
            "kararNo": "2023/45",
            "kararTarihi": "2023-05-10",
            "yayinTarihi": "2023-06-02T10:00:00",
        },
        {
            "id": "47c852cd-489f-2d64-f16f-228906173b64",
            "kararTipi": "NormDenetimi",
            "esasNo": "2022/9",
            "kararNo": "2022/30",
            "kararTarihi": "2022-02-01",
            "yayinTarihi": "2022-03-04T10:00:00",
        },
    ],
}

INDIVIDUAL_BODY = {
    "total": 1,
    "page": 1,
    "page_size": 5,
    "data": [
        {
            "id": "0e7f0dcd-6aad-455a-bca1-50833a29f6bb",
            "kararTipi": "BireyselBasvuru",
            "basvuruNo": "2021/20295",
            "basvuruAdi": "Ahmet Yılmaz",
            "kararTarihi": "2022-06-15",
            "yayinTarihi": "2022-07-01T10:00:00",
        }
    ],
}


@respx.mock
def test_norm_parses_results():
    route = respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json=NORM_BODY))
    out = run_tool(query="ifade özgürlüğü", application_type="norm", limit=5)
    assert route.called
    assert out["total"] == 2
    assert out["count"] == 2
    assert out["page"] == 1
    first = out["results"][0]
    assert first["id"] == "95d2e3dc-1a3f-3834-b965-ea1c0936364f"
    assert first["type"] == "NormDenetimi"
    assert first["title"] == "E. 2023/12, K. 2023/45"
    assert first["date"] == "2023-05-10"
    assert first["url"].startswith(
        "https://normkararlarbilgibankasi.anayasa.gov.tr/kbb/pages/search/NormDenetimi?id="
    )
    assert "type=NormDenetimi" in first["url"]


@respx.mock
def test_norm_sends_keyword_and_pagination():
    route = respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json=NORM_BODY))
    run_tool(query="mülkiyet", application_type="norm", limit=1, page=2)
    sent = json.loads(route.calls.last.request.content)
    assert sent["query"] == "mülkiyet"
    assert sent["kararTipi"] == "NormDenetimi"
    assert sent["size"] == 1
    assert sent["page"] == 1  # 1-indexed page 2 -> API page 1


@respx.mock
def test_individual_parses_results():
    route = respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, json=INDIVIDUAL_BODY))
    out = run_tool(query="adil yargılanma", application_type="individual", limit=5)
    sent = json.loads(route.calls.last.request.content)
    assert sent["kararTipi"] == "BireyselBasvuru"
    assert out["total"] == 1
    assert out["count"] == 1
    first = out["results"][0]
    assert first["id"] == "0e7f0dcd-6aad-455a-bca1-50833a29f6bb"
    assert first["type"] == "BireyselBasvuru"
    assert first["title"] == "Ahmet Yılmaz (B. No: 2021/20295)"
    assert first["date"] == "2022-06-15"


@respx.mock
def test_empty_results():
    respx.post(SEARCH_URL).mock(
        return_value=httpx.Response(200, json={"total": 0, "page": 1, "data": []})
    )
    out = run_tool(query="zzz", application_type="norm")
    assert out["total"] == 0
    assert out["results"] == []


def test_invalid_application_type():
    import pytest

    with pytest.raises(ValueError):
        run_tool(query="x", application_type="hatali")
