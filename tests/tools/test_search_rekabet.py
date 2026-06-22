"""Unit tests for search_rekabet (rekabet.gov.tr HTML scrape mocked with respx)."""

import httpx
import respx

from tools.search_rekabet.client import DECISION_TYPE_GUIDS, run_tool

SEARCH_URL = "https://www.rekabet.gov.tr/tr/Kararlar"

SAMPLE_HTML = """
<html><body>
  <div class="yazi01">Toplam : 42 kayıt bulundu. Sayfa 1 / 5</div>
  <div id="kararList">
    <table class="equalDivide">
      <tr>
        <td>01.02.2024</td>
        <td>24-01/15-9</td>
        <td><a href="/IlgiliKararlar?kararId=guid-1">İlgili Kararlar</a></td>
      </tr>
      <tr>
        <td>10.01.2024</td>
        <td>Rekabet İhlali</td>
      </tr>
      <tr>
        <td colspan="5">
          <a href="/Karar?kararId=guid-1">Örnek Telekomünikasyon Kararı</a>
        </td>
      </tr>
    </table>
    <table class="equalDivide">
      <tr>
        <td>05.02.2024</td>
        <td>24-02/20-12</td>
        <td><a href="/IlgiliKararlar?kararId=guid-2">İlgili Kararlar</a></td>
      </tr>
      <tr>
        <td>20.01.2024</td>
        <td>Birleşme ve Devralma</td>
      </tr>
      <tr>
        <td colspan="5">
          <a href="/Karar?kararId=guid-2">İkinci Birleşme Kararı</a>
        </td>
      </tr>
    </table>
  </div>
</body></html>
"""

EMPTY_HTML = """
<html><body>
  <div class="yazi01">Toplam : 0 kayıt bulundu.</div>
  <div id="kararList"></div>
</body></html>
"""


@respx.mock
def test_search_parses_results():
    route = respx.get(SEARCH_URL).mock(return_value=httpx.Response(200, html=SAMPLE_HTML))
    out = run_tool(query="telekomünikasyon", limit=5)

    assert route.called
    assert out["total"] == 42
    assert out["page"] == 1
    assert out["count"] == 2

    first = out["results"][0]
    assert first["id"] == "guid-1"
    assert first["title"] == "Örnek Telekomünikasyon Kararı"
    assert first["type"] == "Rekabet İhlali"
    assert first["date"] == "10.01.2024"
    assert first["url"] == "https://www.rekabet.gov.tr/Karar?kararId=guid-1"

    second = out["results"][1]
    assert second["id"] == "guid-2"
    assert second["type"] == "Birleşme ve Devralma"


@respx.mock
def test_search_sends_decision_type_guid_and_page():
    route = respx.get(SEARCH_URL).mock(return_value=httpx.Response(200, html=SAMPLE_HTML))
    run_tool(query="birleşme", karar_turu="birlesme_devralma", page=3)

    sent = route.calls.last.request
    params = sent.url.params
    assert params["sayfaAdi"] == "birleşme"
    assert params["KararTuruID"] == DECISION_TYPE_GUIDS["birlesme_devralma"]
    assert params["page"] == "3"


@respx.mock
def test_limit_truncates_results():
    respx.get(SEARCH_URL).mock(return_value=httpx.Response(200, html=SAMPLE_HTML))
    out = run_tool(query="x", limit=1)
    assert out["count"] == 1
    assert out["total"] == 42
    assert out["results"][0]["id"] == "guid-1"


@respx.mock
def test_empty_results():
    respx.get(SEARCH_URL).mock(return_value=httpx.Response(200, html=EMPTY_HTML))
    out = run_tool(query="zzz")
    assert out["total"] == 0
    assert out["count"] == 0
    assert out["results"] == []


def test_unknown_karar_turu_raises():
    try:
        run_tool(query="x", karar_turu="bogus")
    except ValueError as exc:
        assert "bogus" in str(exc)
    else:  # pragma: no cover
        raise AssertionError("expected ValueError for unknown karar_turu")
