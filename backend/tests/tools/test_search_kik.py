"""Unit tests for search_kik (legacy WebForms portal mocked with respx)."""

import httpx
import pytest
import respx

from korkut.tools.search_kik.client import KikClient, run_tool

SEARCH_URL = "https://ekap.kik.gov.tr/EKAP/Vatandas/KurulKararSorgu.aspx"

# Minimal seed page exposing the WebForms state fields the client replays.
SEED_HTML = """
<html><body><form>
<input type="hidden" name="__VIEWSTATE" value="vs-token" />
<input type="hidden" name="__VIEWSTATEGENERATOR" value="gen" />
<input type="hidden" name="__EVENTVALIDATION" value="ev-token" />
</form></body></html>
"""

# Minimal results grid: one row with the 6 cells the parser expects.
GRID_HTML = """
<html><body>
<table id="grdKurulKararSorguSonuc">
  <tr>
    <td><a href="#" onclick="__doPostBack(&#39;grd$ctl02$btnOnizle&#39;,&#39;&#39;)">onizle</a></td>
    <td>2026/UY.I-1661</td>
    <td>18.06.2026</td>
    <td>Karayollari Genel Mudurlugu</td>
    <td>ABC Insaat A.S.</td>
    <td>Mini bakim onarim ihalesi</td>
  </tr>
</table>
</body></html>
"""


@respx.mock
def test_search_parses_grid_row():
    respx.get(SEARCH_URL).mock(return_value=httpx.Response(200, text=SEED_HTML))
    respx.post(SEARCH_URL).mock(return_value=httpx.Response(200, text=GRID_HTML))
    with KikClient() as client:
        out = client.search("uyusmazlik", "ihale", limit=2, resolve_urls=False)
    assert out["count"] == 1
    row = out["results"][0]
    assert row["id"] == "2026/UY.I-1661"
    assert row["date"] == "2026-06-18"
    assert row["decisionType"] == "uyusmazlik"
    assert "ihale" in row["title"].lower()


@respx.mock
def test_empty_grid_returns_no_results():
    respx.get(SEARCH_URL).mock(return_value=httpx.Response(200, text=SEED_HTML))
    respx.post(SEARCH_URL).mock(
        return_value=httpx.Response(200, text="<html><body>no grid here</body></html>")
    )
    with KikClient() as client:
        out = client.search("duzenleyici", "zzz", resolve_urls=False)
    assert out["count"] == 0
    assert out["results"] == []


def test_invalid_decision_type_raises():
    with pytest.raises(ValueError):
        run_tool(query="ihale", decision_type="bogus")
