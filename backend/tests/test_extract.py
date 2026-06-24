"""Secure file ingestion — validation (magic bytes, type/size) and local text extraction."""

import io

import pytest

from korkut.api import extract


def _docx_bytes(text: str) -> bytes:
    import docx

    d = docx.Document()
    d.add_paragraph(text)
    buf = io.BytesIO()
    d.save(buf)
    return buf.getvalue()


def test_txt_extract():
    e = extract.extract("Merhaba dünya".encode(), "not.txt")
    assert e.kind == "txt" and "Merhaba" in e.text


def test_docx_extract():
    e = extract.extract(_docx_bytes("Dava dilekçesi içeriği"), "dilekce.docx")
    assert e.kind == "docx" and "Dava" in e.text


def test_pdf_magic_detected():
    assert extract.sniff_kind(b"%PDF-1.7\n%trailer", "x.pdf") == "pdf"


def test_png_magic_detected():
    assert extract.sniff_kind(b"\x89PNG\r\n\x1a\n" + b"\x00" * 10, "x.png") == "png"


def test_reject_unknown_type():
    with pytest.raises(extract.RejectedFile):
        extract.extract(b"MZ\x90\x00 garbage binary", "a.exe")


def test_reject_svg():
    with pytest.raises(extract.RejectedFile):
        extract.extract(b"<svg xmlns='x'><script>alert(1)</script></svg>", "x.svg")


def test_reject_extension_magic_mismatch():
    # PNG bytes pretending to be a PDF must be rejected.
    with pytest.raises(extract.RejectedFile):
        extract.sniff_kind(b"\x89PNG\r\n\x1a\n" + b"\x00" * 20, "evil.pdf")


def test_reject_oversize():
    with pytest.raises(extract.RejectedFile):
        extract.sniff_kind(b"%PDF-" + b"0" * (extract.MAX_BYTES + 1), "big.pdf")


def test_reject_empty():
    with pytest.raises(extract.RejectedFile):
        extract.sniff_kind(b"", "empty.txt")


def test_image_ocr_roundtrip():
    from PIL import Image, ImageDraw, ImageFont

    img = Image.new("RGB", (640, 200), "white")
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.load_default(size=84)
    except TypeError:
        font = ImageFont.load_default()
    draw.text((20, 50), "MAHKEME", fill="black", font=font)
    buf = io.BytesIO()
    img.save(buf, "PNG")
    try:
        e = extract.extract(buf.getvalue(), "tarama.png")
    except extract.RejectedFile as ex:
        if "tesseract" in str(ex).lower():
            pytest.skip("tesseract kurulu değil")
        raise
    assert e.kind == "png"
    assert "MAHKEME" in e.text.upper()
