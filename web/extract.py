"""Secure file ingestion — validate an uploaded file and extract its text, locally.

Threat model (the user asked for "injection falan istemiyorum"):
  * Malicious upload: we never trust the extension. The byte content is sniffed against a
    strict allow-list of magic numbers; mismatches are rejected. Size is capped, image pixel
    count is capped (decompression bomb), extracted text is capped (token/DoS blowup).
  * The raw bytes are NEVER written to disk and never served back — we extract text in memory
    and discard them. No stored-file attack surface.
  * Prompt injection from file *content* is handled by the caller (web/app.py), which wraps the
    returned text in an explicit "untrusted data, not instructions" envelope before the model
    ever sees it. This module only returns plain extracted text.

Images are OCR'd locally (tesseract) so no raw image leaves the office — only redacted text
goes to the cloud, consistent with the data-stays-in-the-office principle.
"""

from __future__ import annotations

import io
from dataclasses import dataclass

MAX_BYTES = 15 * 1024 * 1024
MAX_CHARS = 50_000
MAX_PDF_PAGES = 60

# kind -> (allowed extensions, list of acceptable magic-byte prefixes)
_MAGIC: dict[str, tuple[tuple[str, ...], tuple[bytes, ...]]] = {
    "pdf": ((".pdf",), (b"%PDF-",)),
    "docx": ((".docx",), (b"PK\x03\x04",)),  # zip container; validated again by python-docx
    "png": ((".png",), (b"\x89PNG\r\n\x1a\n",)),
    "jpg": ((".jpg", ".jpeg"), (b"\xff\xd8\xff",)),
    "txt": ((".txt",), ()),  # no magic; must be decodable UTF-8/Latin-9
}
IMAGE_KINDS = {"png", "jpg"}


class RejectedFile(Exception):
    """Raised when a file fails validation (bad type, too big, corrupt, etc.)."""


@dataclass
class Extracted:
    kind: str
    text: str
    chars: int
    truncated: bool
    note: str = ""


def _ext(filename: str) -> str:
    name = (filename or "").lower().strip()
    return name[name.rfind(".") :] if "." in name else ""


def sniff_kind(data: bytes, filename: str) -> str:
    """Return the validated kind, or raise RejectedFile. Extension must match the magic bytes."""
    if not data:
        raise RejectedFile("Boş dosya")
    if len(data) > MAX_BYTES:
        raise RejectedFile(f"Dosya çok büyük (en fazla {MAX_BYTES // (1024 * 1024)} MB)")

    ext = _ext(filename)
    for kind, (exts, magics) in _MAGIC.items():
        if magics and any(data.startswith(m) for m in magics):
            if ext and ext not in exts:
                raise RejectedFile("Uzantı dosya içeriğiyle uyuşmuyor")
            return kind

    # No magic matched → only a .txt with decodable text is allowed.
    if ext == ".txt":
        try:
            data.decode("utf-8")
        except UnicodeDecodeError:
            try:
                data.decode("iso-8859-9")  # Latin-9 (Turkish)
            except UnicodeDecodeError:
                raise RejectedFile("Metin dosyası okunamadı (kodlama)") from None
        return "txt"

    raise RejectedFile("Desteklenmeyen dosya türü (yalnızca PDF, DOCX, TXT, PNG, JPG)")


def _cap(text: str) -> tuple[str, bool]:
    text = text.strip()
    if len(text) > MAX_CHARS:
        return text[:MAX_CHARS], True
    return text, False


def _from_pdf(data: bytes) -> str:
    from pypdf import PdfReader

    reader = PdfReader(io.BytesIO(data))
    if reader.is_encrypted:
        try:
            reader.decrypt("")
        except Exception:
            raise RejectedFile("PDF şifreli, açılamıyor") from None
    parts = []
    for page in reader.pages[:MAX_PDF_PAGES]:
        parts.append(page.extract_text() or "")
        if sum(len(p) for p in parts) > MAX_CHARS:
            break
    return "\n".join(parts)


def _from_docx(data: bytes) -> str:
    import docx  # python-docx

    try:
        document = docx.Document(io.BytesIO(data))
    except Exception:
        raise RejectedFile("Geçersiz veya bozuk DOCX") from None
    parts = []
    for para in document.paragraphs:
        parts.append(para.text)
        if sum(len(p) for p in parts) > MAX_CHARS:
            break
    return "\n".join(parts)


def _from_image_ocr(data: bytes) -> str:
    from PIL import Image  # Pillow guards decompression bombs by default
    import pytesseract

    try:
        img = Image.open(io.BytesIO(data))
        img.verify()  # detect truncated/corrupt images
        img = Image.open(io.BytesIO(data))  # reopen after verify
    except Exception:
        raise RejectedFile("Geçersiz veya bozuk resim") from None
    try:
        return pytesseract.image_to_string(img, lang="tur+eng")
    except pytesseract.TesseractNotFoundError:
        raise RejectedFile(
            "Sunucuda OCR (tesseract) kurulu değil — yöneticiye 'brew install tesseract "
            "tesseract-lang' kurdurun."
        ) from None
    except Exception:
        raise RejectedFile("Resimden metin çıkarılamadı") from None


def extract(data: bytes, filename: str) -> Extracted:
    """Validate `data` and return its extracted text. Raises RejectedFile on any problem."""
    kind = sniff_kind(data, filename)
    if kind == "pdf":
        raw = _from_pdf(data)
    elif kind == "docx":
        raw = _from_docx(data)
    elif kind == "txt":
        raw = data.decode("utf-8", errors="replace")
    elif kind in IMAGE_KINDS:
        raw = _from_image_ocr(data)
    else:  # unreachable (sniff_kind would have raised)
        raise RejectedFile("Desteklenmeyen dosya türü")

    text, truncated = _cap(raw)
    if not text:
        note = "Dosyadan metin çıkarılamadı (taranmış/boş olabilir)."
        if kind in IMAGE_KINDS:
            note = "Resimde okunabilir metin bulunamadı."
        return Extracted(kind=kind, text="", chars=0, truncated=False, note=note)
    return Extracted(kind=kind, text=text, chars=len(text), truncated=truncated)
