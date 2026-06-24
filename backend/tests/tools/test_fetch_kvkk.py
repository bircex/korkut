"""Unit tests for fetch_kvkk (decision HTML page mocked with respx)."""

import httpx
import pytest
import respx

from korkut.tools.fetch_kvkk.client import run_tool

DECISION_URL = "https://www.kvkk.gov.tr/Icerik/7288/2021-1303"

PAGE_HTML = """
<html><head><title>KVKK</title></head><body>
<h3 class="blog-post-title">Test Karar Başlığı</h3>
<div class="blog-post-inner">
  <table>
    <tr><td>Karar Tarihi</td><td>:</td><td>23/12/2021</td></tr>
    <tr><td>Karar No</td><td>:</td><td>2021/1303</td></tr>
  </table>
  <p>Karar metni burada yer alır.</p>
</div>
</body></html>
"""


@respx.mock
def test_fetch_extracts_title_and_metadata():
    respx.get(DECISION_URL).mock(return_value=httpx.Response(200, text=PAGE_HTML))
    out = run_tool(decision_url=DECISION_URL)
    assert out["title"] == "Test Karar Başlığı"
    assert out["date"] == "23/12/2021"
    assert out["decisionNumber"] == "2021/1303"
    assert "Karar metni burada" in out["markdown"]
    assert out["page"] == 1
    assert out["total_pages"] >= 1


@respx.mock
def test_pagination():
    long_html = '<div class="blog-post-inner">' + ("a" * 12000) + "</div>"
    respx.get(DECISION_URL).mock(return_value=httpx.Response(200, text=long_html))
    out = run_tool(decision_url=DECISION_URL, page=2, page_size=5000)
    assert out["total_pages"] == 3
    assert out["page"] == 2
    assert len(out["markdown"]) == 5000


def test_rejects_non_url():
    with pytest.raises(ValueError):
        run_tool(decision_url="not-a-url")
