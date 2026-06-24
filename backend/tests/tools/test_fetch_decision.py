"""Unit tests for fetch_decision (Bedesten document endpoint mocked)."""

import base64

import httpx
import pytest
import respx

from korkut.tools.fetch_decision.client import run_tool

DOC_URL = "https://bedesten.adalet.gov.tr/emsal-karar/getDocumentContent"


def _doc_response(html: str) -> dict:
    return {
        "data": {
            "content": base64.b64encode(html.encode("utf-8")).decode("ascii"),
            "mimeType": "text/html",
            "version": 1,
        },
        "metadata": {"FMTY": "SUCCESS"},
    }


@respx.mock
def test_fetch_converts_html_to_markdown():
    html = "<h1>Yargıtay 1. Hukuk Dairesi</h1><p>Esas No: 2023/123</p>"
    respx.post(DOC_URL).mock(return_value=httpx.Response(200, json=_doc_response(html)))
    out = run_tool(document_id="doc-1")
    assert out["documentId"] == "doc-1"
    assert out["mimeType"] == "text/html"
    assert "Yargıtay 1. Hukuk Dairesi" in out["markdown"]
    assert "doc-1" in out["sourceUrl"]


def test_missing_document_id_raises():
    with pytest.raises(ValueError):
        run_tool(document_id="")
