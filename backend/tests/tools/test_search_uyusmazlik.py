"""Unit tests for search_uyusmazlik (WebForms portal mocked with respx)."""

import httpx
import pytest
import respx

from korkut.tools.search_uyusmazlik.client import run_tool

ROOT = "https://kararlar.uyusmazlik.gov.tr/"

SEED_HTML = """
<html><body><form>
<input type="hidden" name="__VIEWSTATE" value="vs" />
<input type="hidden" name="__VIEWSTATEGENERATOR" value="gen" />
<input type="hidden" name="__EVENTVALIDATION" value="ev" />
<input type="text" name="txtSearch" />
<input type="submit" name="btnSearch" value="Ara" />
</form></body></html>
"""

GRID_HTML = """
<html><body>
<input type="hidden" name="__VIEWSTATE" value="vs2" />
<input type="hidden" name="__VIEWSTATEGENERATOR" value="gen" />
<input type="hidden" name="__EVENTVALIDATION" value="ev2" />
<span id="lblSearchStatus">Arama sonuçları: 2912 dosya bulundu.</span>
<table id="GridView1">
  <tr><th>Esas Numarası</th><th>Karar Numarası</th><th>Karar Tarihi</th><th>İşlemler</th></tr>
  <tr>
    <td>2026/180</td><td>2026/162</td><td>13/04/2026</td>
    <td><a href='Uploads/2026-180.pdf' class="grid-link" target="_blank">Görüntüle</a></td>
  </tr>
  <tr>
    <td colspan="4"><table><tr>
      <td class="current-page"><span>1</span></td>
      <td><a href="javascript:__doPostBack(&#39;GridView1&#39;,&#39;Page$2&#39;)">2</a></td>
    </tr></table></td>
  </tr>
</table>
</body></html>
"""


@respx.mock
def test_search_parses_grid_row():
    respx.get(ROOT).mock(return_value=httpx.Response(200, text=SEED_HTML))
    respx.post(ROOT).mock(return_value=httpx.Response(200, text=GRID_HTML))
    out = run_tool(query="imar", limit=5)
    assert out["total"] == 2912
    assert out["count"] == 1
    row = out["results"][0]
    assert row["esasNo"] == "2026/180"
    assert row["kararNo"] == "2026/162"
    assert row["date"] == "13/04/2026"
    assert row["url"] == "https://kararlar.uyusmazlik.gov.tr/Uploads/2026-180.pdf"


@respx.mock
def test_search_sends_query_and_scope():
    respx.get(ROOT).mock(return_value=httpx.Response(200, text=SEED_HTML))
    route = respx.post(ROOT).mock(return_value=httpx.Response(200, text=GRID_HTML))
    run_tool(query="imar", scope="esas")
    body = route.calls.last.request.content.decode()
    assert "txtSearch=imar" in body
    assert "rblSearchScope=EsasNo" in body
    assert "btnSearch=Ara" in body


def test_unknown_scope_raises():
    with pytest.raises(ValueError):
        run_tool(query="x", scope="bogus")


def test_empty_query_raises():
    with pytest.raises(ValueError):
        run_tool(query="")
