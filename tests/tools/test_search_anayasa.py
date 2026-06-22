"""Unit tests for search_anayasa (Constitutional Court portals mocked with respx)."""

import httpx
import respx

from tools.search_anayasa.client import run_tool

NORM_URL = "https://normkararlarbilgibankasi.anayasa.gov.tr/Ara"
BIREYSEL_URL = "https://kararlarbilgibankasi.anayasa.gov.tr/Ara"

NORM_HTML = """
<html><body>
  <div class="bulunankararsayisi">2 Karar Bulundu</div>
  <div class="birkarar">
    <a href="/Dosyalar/2023-456">link</a>
    <div class="bkararbaslik">E. 2023/12 , K. 2023/45 Sayılı Karar</div>
    <div class="kararbilgileri">İptal | Başvuran | Ret | Karar Tarihi: 10.05.2023</div>
  </div>
  <div class="birkarar">
    <a href="/Dosyalar/2022-100">link</a>
    <div class="bkararbaslik">E. 2022/9 , K. 2022/30 Sayılı Karar</div>
    <div class="kararbilgileri">Ret | Başvuran | Ret | Karar Tarihi: 01.02.2022</div>
  </div>
</body></html>
"""

BIREYSEL_HTML = """
<html><body>
  <div class="bulunankararsayisi">1 Karar Bulundu</div>
  <div class="KararBulteniBirKarar">
    <h4><strong>Ahmet Yılmaz Başvurusu</strong></h4>
    <div class="AltiCizili">
      <a href="/BB/2021/20295">2021/20295</a>
      Bireysel Başvuru | Genel Kurul |
      Başvuru Tarihi : 01/01/2021 | Karar Tarihi : 15/06/2022
    </div>
  </div>
</body></html>
"""


@respx.mock
def test_norm_parses_results():
    route = respx.get(NORM_URL).mock(return_value=httpx.Response(200, text=NORM_HTML))
    out = run_tool(query="ifade özgürlüğü", basvuru_turu="norm", limit=5)
    assert route.called
    assert out["total"] == 2
    assert out["count"] == 2
    assert out["page"] == 1
    first = out["results"][0]
    assert first["id"] == "2023-456"
    assert first["type"] == "norm"
    assert first["title"] == "E. 2023/12 , K. 2023/45"
    assert first["date"] == "10.05.2023"
    assert first["url"] == "https://normkararlarbilgibankasi.anayasa.gov.tr/Dosyalar/2023-456"


@respx.mock
def test_norm_sends_keyword_and_respects_limit():
    route = respx.get(NORM_URL).mock(return_value=httpx.Response(200, text=NORM_HTML))
    out = run_tool(query="mülkiyet", basvuru_turu="norm", limit=1)
    sent = route.calls.last.request
    assert "KelimeAra%5B%5D=m%C3%BClkiyet" in str(sent.url) or "KelimeAra[]" in str(sent.url)
    assert out["count"] == 1
    assert out["total"] == 2


@respx.mock
def test_bireysel_parses_results():
    route = respx.get(BIREYSEL_URL).mock(
        return_value=httpx.Response(200, text=BIREYSEL_HTML)
    )
    out = run_tool(query="adil yargılanma", basvuru_turu="bireysel", limit=5)
    assert route.called
    assert out["total"] == 1
    assert out["count"] == 1
    first = out["results"][0]
    assert first["id"] == "20295"
    assert first["type"] == "bireysel"
    assert first["title"] == "Ahmet Yılmaz Başvurusu"
    assert first["date"] == "15/06/2022"
    assert first["url"] == "https://kararlarbilgibankasi.anayasa.gov.tr/BB/2021/20295"


@respx.mock
def test_empty_results():
    respx.get(NORM_URL).mock(
        return_value=httpx.Response(200, text="<html><body></body></html>")
    )
    out = run_tool(query="zzz", basvuru_turu="norm")
    assert out["total"] == 0
    assert out["results"] == []


def test_invalid_basvuru_turu():
    import pytest

    with pytest.raises(ValueError):
        run_tool(query="x", basvuru_turu="hatali")
