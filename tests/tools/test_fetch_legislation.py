"""Unit tests for fetch_legislation (Bedesten mevzuat document endpoint mocked)."""

import base64

import httpx
import pytest
import respx

from tools.fetch_legislation.client import run_tool

DOC_URL = "https://bedesten.adalet.gov.tr/mevzuat/getDocumentContent"


def _doc_response(body: str, *, mime: str = "text/html", title: str = "") -> dict:
    data = {
        "content": base64.b64encode(body.encode("utf-8")).decode("ascii"),
        "mimeType": mime,
    }
    if title:
        data["mevzuatAdi"] = title
    return {"data": data, "metadata": {"FMTY": "SUCCESS"}}


@respx.mock
def test_fetch_full_text_converts_html_to_markdown():
    html = "<h1>Türk Borçlar Kanunu</h1><p>MADDE 1 - Genel hükümler</p>"
    route = respx.post(DOC_URL).mock(
        return_value=httpx.Response(200, json=_doc_response(html, title="Türk Borçlar Kanunu"))
    )
    out = run_tool(mevzuat_id="12345")
    assert out["id"] == "12345"
    assert out["title"] == "Türk Borçlar Kanunu"
    assert "Türk Borçlar Kanunu" in out["markdown"]
    assert "MADDE 1" in out["markdown"]
    assert "mevzuatId=12345" in out["sourceUrl"]

    sent = route.calls.last.request
    payload = sent.content.decode("utf-8")
    assert '"documentType": "MEVZUAT"' in payload or '"documentType":"MEVZUAT"' in payload
    assert "12345" in payload


@respx.mock
def test_fetch_single_article_uses_madde_id():
    html = "<p>MADDE 27 - Ahlaka aykırı sözleşmeler</p>"
    route = respx.post(DOC_URL).mock(
        return_value=httpx.Response(200, json=_doc_response(html))
    )
    out = run_tool(mevzuat_id="12345", madde_id="67890")
    assert out["id"] == "67890"
    assert "MADDE 27" in out["markdown"]
    assert "mevzuatId=12345" in out["sourceUrl"]

    payload = route.calls.last.request.content.decode("utf-8")
    assert "MADDE" in payload
    assert "67890" in payload


@respx.mock
def test_error_metadata_raises():
    respx.post(DOC_URL).mock(
        return_value=httpx.Response(
            200, json={"data": {}, "metadata": {"FMTY": "ERROR", "FMTE": "not found"}}
        )
    )
    with pytest.raises(ValueError):
        run_tool(mevzuat_id="does-not-exist")


def test_missing_mevzuat_id_raises():
    with pytest.raises(ValueError):
        run_tool(mevzuat_id="")
