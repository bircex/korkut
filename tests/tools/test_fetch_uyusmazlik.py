"""Unit tests for fetch_uyusmazlik (decision PDF/HTML fetch mocked with respx)."""

import httpx
import pytest
import respx

import tools._base.uyusmazlik as uyus_base
from tools.fetch_uyusmazlik.client import run_tool

PDF_URL = "https://kararlar.uyusmazlik.gov.tr/Uploads/2026-180.pdf"


@respx.mock
def test_fetch_pdf_decision(monkeypatch):
    monkeypatch.setattr(uyus_base, "pdf_to_markdown", lambda raw: "KARAR METNİ")
    respx.get(PDF_URL).mock(
        return_value=httpx.Response(200, content=b"%PDF-1.4 fake", headers={"content-type": "application/pdf"})
    )
    out = run_tool(document_url=PDF_URL)
    assert out["sourceUrl"] == PDF_URL
    assert out["markdown"] == "KARAR METNİ"


def test_rejects_non_url():
    with pytest.raises(ValueError):
        run_tool(document_url="2026-180")


def test_rejects_foreign_host():
    with pytest.raises(ValueError):
        run_tool(document_url="https://example.com/x.pdf")
