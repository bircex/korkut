"""Unit tests for fetch_bddk (document fetch mocked with respx)."""

import httpx
import pytest
import respx

import tools.fetch_bddk.client as bddk_client
from tools.fetch_bddk.client import run_tool

URL1 = "https://www.bddk.org.tr/Mevzuat/DokumanGetir/310"
URL2 = "https://www.bddk.org.tr/Mevzuat/Liste/310"


@respx.mock
def test_fetch_pdf(monkeypatch):
    monkeypatch.setattr(bddk_client, "pdf_to_markdown", lambda raw: "BDDK KARAR METNİ")
    respx.get(URL1).mock(
        return_value=httpx.Response(
            200, content=b"%PDF-1.4 fake", headers={"content-type": "application/pdf"}
        )
    )
    out = run_tool(document_id="310")
    assert out["documentId"] == "310"
    assert out["sourceUrl"] == URL1
    assert out["markdown"] == "BDDK KARAR METNİ"


@respx.mock
def test_falls_back_to_second_url_for_html():
    respx.get(URL1).mock(return_value=httpx.Response(404))
    respx.get(URL2).mock(
        return_value=httpx.Response(
            200,
            text="<html><body><p>HTML karar</p></body></html>",
            headers={"content-type": "text/html"},
        )
    )
    out = run_tool(document_id="310")
    assert out["sourceUrl"] == URL2
    assert "HTML karar" in out["markdown"]


@respx.mock
def test_all_urls_fail_raises():
    for path in ("DokumanGetir/9", "Liste/9"):
        respx.get(f"https://www.bddk.org.tr/Mevzuat/{path}").mock(return_value=httpx.Response(404))
    respx.get(url__regex=r".*EkGetir.*").mock(return_value=httpx.Response(404))
    with pytest.raises(ValueError):
        run_tool(document_id="9")


def test_empty_id_raises():
    with pytest.raises(ValueError):
        run_tool(document_id="")
