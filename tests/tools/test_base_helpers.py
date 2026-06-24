"""Unit tests for shared helpers in tools/_base/client.py."""

import ssl

from tools._base.client import chunk_markdown, extract_div_html, legacy_ssl_context


def test_chunk_markdown_pages():
    out = chunk_markdown("x" * 12000, page=2, page_size=5000)
    assert out["total_pages"] == 3
    assert out["page"] == 2
    assert len(out["markdown"]) == 5000


def test_chunk_markdown_clamps_page():
    out = chunk_markdown("short", page=99, page_size=5000)
    assert out["page"] == 1
    assert out["total_pages"] == 1


def test_chunk_markdown_empty():
    out = chunk_markdown("", page=1)
    assert out["total_pages"] == 1
    assert out["markdown"] == ""


def test_extract_div_inner_html():
    html = (
        '<html><body><div class="a">skip</div>'
        '<div class="blog-post-inner">Hi <b>there</b><div>nested</div></div>'
        "</body></html>"
    )
    got = extract_div_html(html, "blog-post-inner")
    assert "Hi" in got and "there" in got and "nested" in got
    assert "skip" not in got


def test_extract_div_falls_back_to_whole_doc():
    html = "<html><body><p>no target div</p></body></html>"
    assert extract_div_html(html, "missing") == html


def test_legacy_ssl_context_disables_verification():
    ctx = legacy_ssl_context()
    assert isinstance(ctx, ssl.SSLContext)
    assert ctx.verify_mode == ssl.CERT_NONE
    assert ctx.check_hostname is False
