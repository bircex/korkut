"""Unit tests for search_sayistay (sayistay.gov.tr mocked with respx; no network)."""

from urllib.parse import parse_qs

import httpx
import pytest
import respx

from tools.search_sayistay.client import run_tool

BASE = "https://www.sayistay.gov.tr"

PAGE_HTML = (
    '<form><input name="__RequestVerificationToken" type="hidden" '
    'value="csrf-token-123" /></form>'
)

GENEL_KURUL_JSON = {
    "draw": 1,
    "recordsTotal": 1,
    "recordsFiltered": 1,
    "data": [
        {
            "Id": 5001,
            "KARARNO": "5415/1",
            "KARARTARIH": "10.05.2023",
            "KARAROZETI": "Özet metni",
        }
    ],
}

TEMYIZ_JSON = {
    "draw": 1,
    "recordsTotal": 2,
    "recordsFiltered": 2,
    "data": [
        {
            "Id": 6002,
            "TEMYIZTUTANAKTARIHI": "01.03.2023",
            "ILAMDAIRESI": 3,
            "TEMYIZKARAR": "Temyiz karar metni",
        }
    ],
}

DAIRE_JSON = {
    "draw": 1,
    "recordsTotal": 7,
    "recordsFiltered": 7,
    "data": [
        {
            "Id": 7003,
            "YARGILAMADAIRESI": 1,
            "KARARTRH": "15.06.2023",
            "KARARNO": "123",
            "ILAMNO": "456",
        }
    ],
}


def _mock(decision_path: str, search_json: dict):
    respx.get(f"{BASE}{decision_path}").mock(
        return_value=httpx.Response(200, text=PAGE_HTML)
    )
    return respx.post(f"{BASE}{decision_path}/DataTablesList").mock(
        return_value=httpx.Response(200, json=search_json)
    )


@respx.mock
def test_genel_kurul_parses_and_is_default():
    route = _mock("/KararlarGenelKurul", GENEL_KURUL_JSON)
    out = run_tool(query="ihale", limit=5)
    assert route.called
    assert out["total"] == 1
    assert out["page"] == 1
    assert out["count"] == 1
    first = out["results"][0]
    assert first["id"] == 5001
    assert first["title"] == "5415/1"
    assert first["decisionType"] == "Genel Kurul"
    assert first["date"] == "10.05.2023"
    assert first["url"] == "https://www.sayistay.gov.tr/KararlarGenelKurul/Detay/5001/"


@respx.mock
def test_temyiz_kurulu_parses():
    _mock("/KararlarTemyiz", TEMYIZ_JSON)
    out = run_tool(query="harcırah", karar_turu="temyiz_kurulu")
    assert out["total"] == 2
    first = out["results"][0]
    assert first["id"] == 6002
    assert first["decisionType"] == "Temyiz Kurulu"
    assert first["title"] == "3. Daire"
    assert first["date"] == "01.03.2023"


@respx.mock
def test_daire_parses():
    _mock("/KararlarDaire", DAIRE_JSON)
    out = run_tool(query="personel", karar_turu="daire")
    assert out["total"] == 7
    first = out["results"][0]
    assert first["id"] == 7003
    assert first["decisionType"] == "Daire"
    assert first["title"] == "123"
    assert first["date"] == "15.06.2023"
    assert first["url"] == "https://www.sayistay.gov.tr/KararlarDaire/Detay/7003/"


@respx.mock
def test_csrf_token_and_pagination_sent():
    route = _mock("/KararlarGenelKurul", GENEL_KURUL_JSON)
    run_tool(query="vergi", limit=10, page=3)
    sent = route.calls.last.request
    body = parse_qs(sent.content.decode("utf-8"))
    assert body["__RequestVerificationToken"] == ["csrf-token-123"]
    assert body["length"] == ["10"]
    assert body["start"] == ["20"]
    assert body["KararlarGenelKurulAra.KARARTAMAMI"] == ["vergi"]


@respx.mock
def test_empty_results():
    _mock("/KararlarGenelKurul", {"draw": 1, "recordsTotal": 0, "recordsFiltered": 0, "data": []})
    out = run_tool(query="zzz")
    assert out["total"] == 0
    assert out["results"] == []


def test_invalid_decision_type_raises():
    with pytest.raises(ValueError):
        run_tool(query="x", karar_turu="bogus")
