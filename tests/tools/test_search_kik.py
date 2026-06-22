"""Unit tests for search_kik (KİK v2 API mocked with respx; no network)."""

import json

import httpx
import respx

from tools.search_kik.client import run_tool

BASE = "https://ekapv2.kik.gov.tr/b_ihalearaclari/api/KurulKararlari"
UYUSMAZLIK_URL = f"{BASE}/GetKurulKararlari"
DUZENLEYICI_URL = f"{BASE}/GetKurulKararlariDk"
MAHKEME_URL = f"{BASE}/GetKurulKararlariMk"


def _response(wrapper: str, result: str, records: list[dict]) -> dict:
    return {
        wrapper: {
            result: {
                "hataKodu": "0",
                "hataMesaji": "",
                "KurulKararTutanakDetayListesi": [{"kurulKararTutanakDetayi": records}],
            }
        }
    }


RECORD = {
    "kararNo": "2025/UH.II-1801",
    "kararTarihi": "2025-01-15T00:00:00",
    "basvuran": "ACME Ltd.",
    "idareAdi": "Örnek Belediyesi",
    "basvuruKonusu": "aşırı düşük teklif değerlendirmesi",
    "gundemMaddesiId": "177280",
}

SAMPLE = _response(
    "SorgulaKurulKararlariResponse", "SorgulaKurulKararlariResult", [RECORD]
)


@respx.mock
def test_search_parses_results():
    route = respx.post(UYUSMAZLIK_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    out = run_tool(query="aşırı düşük", limit=5)
    assert route.called
    assert out["total"] == 1
    assert out["count"] == 1
    assert out["page"] == 1
    first = out["results"][0]
    assert first["id"] == "177280"
    assert first["title"] == "aşırı düşük teklif değerlendirmesi"
    assert first["decisionType"] == "uyusmazlik"
    assert first["date"] == "2025-01-15T00:00:00"
    assert first["url"].endswith("KararId=177280")


@respx.mock
def test_default_karar_turu_hits_uyusmazlik_endpoint_with_query():
    route = respx.post(UYUSMAZLIK_URL).mock(return_value=httpx.Response(200, json=SAMPLE))
    run_tool(query="ihale", karar_no="2025/UH.II-1801")
    body = json.loads(route.calls.last.request.content)
    pairs = body["sorgulaKurulKararlari"]["keyValuePairs"]["keyValueOfstringanyType"]
    by_key = {p["key"]: p["value"] for p in pairs}
    assert by_key["KararMetni"] == "ihale"
    assert by_key["KararNo"] == "2025/UH.II-1801"


@respx.mock
def test_duzenleyici_routes_to_dk_endpoint():
    sample = _response(
        "SorgulaKurulKararlariDkResponse", "SorgulaKurulKararlariDkResult", [RECORD]
    )
    route = respx.post(DUZENLEYICI_URL).mock(return_value=httpx.Response(200, json=sample))
    out = run_tool(query="x", decision_type="duzenleyici")
    assert route.called
    assert out["results"][0]["decisionType"] == "duzenleyici"


@respx.mock
def test_mahkeme_routes_to_mk_endpoint():
    sample = _response(
        "SorgulaKurulKararlariMkResponse", "SorgulaKurulKararlariMkResult", [RECORD]
    )
    route = respx.post(MAHKEME_URL).mock(return_value=httpx.Response(200, json=sample))
    out = run_tool(query="x", decision_type="mahkeme")
    assert route.called
    assert out["results"][0]["decisionType"] == "mahkeme"


@respx.mock
def test_client_side_pagination():
    records = [dict(RECORD, gundemMaddesiId=str(i)) for i in range(25)]
    sample = _response(
        "SorgulaKurulKararlariResponse", "SorgulaKurulKararlariResult", records
    )
    respx.post(UYUSMAZLIK_URL).mock(return_value=httpx.Response(200, json=sample))
    out = run_tool(query="x", limit=10, page=2)
    assert out["total"] == 25
    assert out["count"] == 10
    assert out["page"] == 2
    assert out["results"][0]["id"] == "10"


@respx.mock
def test_empty_results():
    sample = _response(
        "SorgulaKurulKararlariResponse", "SorgulaKurulKararlariResult", []
    )
    respx.post(UYUSMAZLIK_URL).mock(return_value=httpx.Response(200, json=sample))
    out = run_tool(query="zzz")
    assert out["total"] == 0
    assert out["results"] == []


@respx.mock
def test_api_error_raises():
    sample = {
        "SorgulaKurulKararlariResponse": {
            "SorgulaKurulKararlariResult": {
                "hataKodu": "5",
                "hataMesaji": "geçersiz sorgu",
                "KurulKararTutanakDetayListesi": [],
            }
        }
    }
    respx.post(UYUSMAZLIK_URL).mock(return_value=httpx.Response(200, json=sample))
    try:
        run_tool(query="x")
        raise AssertionError("expected RuntimeError")
    except RuntimeError as exc:
        assert "5" in str(exc)


def test_invalid_karar_turu_raises():
    try:
        run_tool(query="x", decision_type="bogus")
        raise AssertionError("expected ValueError")
    except ValueError as exc:
        assert "decision_type" in str(exc)
