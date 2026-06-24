"""Unit tests for fetch_sigorta_tahkim and search_within_sigorta_tahkim.

The PDF download/convert step is monkeypatched so these tests exercise the tool logic
(pagination, decision splitting, keyword ranking) without network or PDF parsing.
"""

import pytest

import tools._base.sigorta_tahkim as sig_base
from tools._base.sigorta_tahkim import pdf_filename, split_into_decisions, turkish_lower
from tools.fetch_sigorta_tahkim.client import run_tool as fetch_run
from tools.search_within_sigorta_tahkim.client import run_tool as within_run

PDF_URL = "https://www.sigortatahkim.org/content/CmsFiles/karardrgs64.pdf"

_BODY_A = "değer kaybı " * 200  # ~2400 chars, well over the MIN_DECISION_BODY_LENGTH gate
_BODY_B = "kasko poliçesi " * 200
ISSUE_MARKDOWN = (
    "İÇİNDEKİLER\n"
    "01.02.2024 Tarih ve K-2024/123 Sayılı Hakem Kararı\n" + _BODY_A + "\n"
    "05.03.2024 Tarih ve K-2024/200 Sayılı Hakem Kararı\n" + _BODY_B + "\n"
)


@pytest.fixture
def _fake_download(monkeypatch):
    monkeypatch.setattr(
        sig_base.SigortaTahkimClient,
        "download_issue_markdown",
        lambda self, issue: (ISSUE_MARKDOWN, PDF_URL),
    )


def test_fetch_paginates(_fake_download):
    out = fetch_run(issue_number="64", page=1, page_size=2000)
    assert out["issueNumber"] == "64"
    assert out["sourceUrl"] == PDF_URL
    assert out["page"] == 1
    assert out["total_pages"] >= 2
    assert len(out["markdown"]) == 2000


def test_fetch_requires_issue():
    with pytest.raises(ValueError):
        fetch_run(issue_number="")


def test_search_within_ranks_matches(_fake_download):
    out = within_run(issue_number="64", keyword="değer kaybı", max_results=5)
    assert out["totalDecisions"] == 2
    assert out["matchingDecisions"] == 1
    top = out["results"][0]
    assert "K-2024/123" in top["header"]
    assert top["relevanceScore"] == _BODY_A.count("değer kaybı")
    assert "değer kaybı" in turkish_lower(top["excerpt"])


def test_search_within_requires_keyword(_fake_download):
    with pytest.raises(ValueError):
        within_run(issue_number="64", keyword="")


def test_pdf_filename_variants():
    assert pdf_filename(4) == "karardergisisayi4.pdf"
    assert pdf_filename(58) == "revizekd58.pdf"
    assert pdf_filename(64) == "karardrgs64.pdf"


def test_split_drops_toc_entries():
    md = "01.01.2024 Tarih ve K-2024/1 Sayılı Hakem Kararı\nshort"
    assert split_into_decisions(md) == []


def test_turkish_lower():
    assert turkish_lower("İŞÇİ") == "işçi"
    assert turkish_lower("IĞDIR") == "ığdır"
