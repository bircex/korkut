"""Render a document's markdown to DOCX and UDF (UYAP).

Source of truth is markdown; we parse a pragmatic subset (headings, paragraphs, bullet/number
lists, blockquotes, **bold**) into blocks, then render.

UDF (UYAP Doküman Formatı) is a ZIP whose `content.xml` stores the whole text once and an
`<elements>` tree of `<paragraph>`/`<content>` nodes that index into it by offset. The schema is
reverse-engineered and NOT officially published — this emitter is best-effort and MUST be
validated against a genuine UYAP Editör sample before production use (see DOCUMENT_EDITOR.md §11).
"""

from __future__ import annotations

import io
import re
import zipfile
from xml.sax.saxutils import escape

_HEADING_DOCX = {"h1": 1, "h2": 2, "h3": 3}
_HEADING_SIZE = {"h1": 18, "h2": 15, "h3": 13}


def _inline(text: str) -> tuple[str, list[tuple[int, int]]]:
    """Strip **bold** markers; return (clean_text, [(start, length), ...]) of bold spans."""
    clean = ""
    spans: list[tuple[int, int]] = []
    bold = False
    for part in re.split(r"(\*\*)", text):
        if part == "**":
            bold = not bold
            continue
        if bold and part:
            spans.append((len(clean), len(part)))
        clean += part
    return clean, spans


def _blocks(md: str) -> list[tuple[str, str]]:
    blocks: list[tuple[str, str]] = []
    for raw in (md or "").split("\n"):
        line = raw.rstrip()
        if not line.strip():
            continue
        if line.startswith("### "):
            blocks.append(("h3", line[4:]))
        elif line.startswith("## "):
            blocks.append(("h2", line[3:]))
        elif line.startswith("# "):
            blocks.append(("h1", line[2:]))
        elif line.startswith("> "):
            blocks.append(("quote", line[2:]))
        elif re.match(r"^[-*] ", line):
            blocks.append(("bullet", line[2:]))
        elif re.match(r"^\d+\.\s", line):
            blocks.append(("number", re.sub(r"^\d+\.\s", "", line)))
        elif len(line) >= 3 and set(line) <= {"-", "—", "*", "_", " "}:
            continue  # horizontal rule
        else:
            blocks.append(("p", line))
    return blocks


def _bold_mask(length: int, spans: list[tuple[int, int]]) -> list[bool]:
    mask = [False] * length
    for start, ln in spans:
        for i in range(start, min(start + ln, length)):
            mask[i] = True
    return mask


def _runs(mask: list[bool], text: str):
    """Yield (chunk_text, bold) groups."""
    i = 0
    while i < len(text):
        b = mask[i]
        j = i
        while j < len(text) and mask[j] == b:
            j += 1
        yield text[i:j], b
        i = j


# ---------------- DOCX ----------------

def to_docx(title: str, md: str) -> bytes:
    import docx

    d = docx.Document()
    if title:
        d.add_heading(title, level=0)
    for kind, text in _blocks(md):
        clean, spans = _inline(text)
        if kind in _HEADING_DOCX:
            p = d.add_paragraph(style=f"Heading {_HEADING_DOCX[kind]}")
        elif kind == "bullet":
            p = d.add_paragraph(style="List Bullet")
        elif kind == "number":
            p = d.add_paragraph(style="List Number")
        elif kind == "quote":
            p = d.add_paragraph(style="Intense Quote")
        else:
            p = d.add_paragraph()
        for chunk, bold in _runs(_bold_mask(len(clean), spans), clean):
            run = p.add_run(chunk)
            run.bold = bold or kind in _HEADING_DOCX
    buf = io.BytesIO()
    d.save(buf)
    return buf.getvalue()


# ---------------- UDF (UYAP) ----------------

def to_udf(title: str, md: str) -> bytes:
    blocks = _blocks(md)
    if title:
        blocks = [("h1", title)] + blocks

    parsed = [(kind, *_inline(text)) for kind, text in blocks]
    full_text = "\n".join(clean for _, clean, _ in parsed)

    elements: list[str] = []
    offset = 0
    for kind, clean, spans in parsed:
        is_heading = kind in _HEADING_SIZE
        mask = _bold_mask(len(clean), spans)
        contents: list[str] = []
        local = 0
        for chunk, bold in _runs(mask, clean):
            attrs = f'startOffset="{offset + local}" length="{len(chunk)}"'
            if bold or is_heading:
                attrs += ' bold="true"'
            if is_heading:
                attrs += f' size="{_HEADING_SIZE[kind]}"'
            contents.append(f"<content {attrs}/>")
            local += len(chunk)
        if not contents:  # empty paragraph
            contents.append(f'<content startOffset="{offset}" length="0"/>')
        align = "1" if is_heading else "0"
        elements.append(f'<paragraph Alignment="{align}">' + "".join(contents) + "</paragraph>")
        offset += len(clean) + 1  # +1 for the "\n" joiner

    content_xml = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n'
        '<template format_id="1.0">'
        f"<content><![CDATA[{full_text}]]></content>"
        '<properties><pageFormat mediaSizeName="1" leftMargin="42.5" rightMargin="28.3" '
        'topMargin="28.3" bottomMargin="14.1" paperOrientation="1" headerFOffset="20.0" '
        'footerFOffset="20.0"/></properties>'
        '<elements resolver="hvl-default">' + "".join(elements) + "</elements>"
        '<styles><style name="default" description="Geçerli" family="Dialog" size="12" '
        'bold="false" italic="false" foreground="-13421773" FONT_ATTR="false"/></styles>'
        "</template>"
    )
    # `escape` only the CDATA-unsafe sequence guard (CDATA already protects most); keep simple.
    _ = escape  # noqa: F841 (kept for future per-attribute escaping)

    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("content.xml", content_xml.encode("utf-8"))
    return buf.getvalue()


RENDERERS = {
    "docx": ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", to_docx),
    "udf": ("application/zip", to_udf),
}
