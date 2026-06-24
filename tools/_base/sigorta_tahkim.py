"""Shared helpers for Sigorta Tahkim Komisyonu (Insurance Arbitration Commission) decisions.

The commission publishes quarterly PDF journals ("Hakem Karar Dergisi"), each holding many
arbitration decisions. Decisions are not individually addressable: you fetch a journal issue PDF
by its number and read or search within it. This module downloads + converts an issue PDF and
splits it into individual decisions, shared by ``fetch_sigorta_tahkim`` and
``search_within_sigorta_tahkim``.
"""

from __future__ import annotations

import re
from typing import Any

import httpx

from .client import BaseClient, legacy_ssl_context, pdf_to_markdown

PDF_BASE_URL = "https://www.sigortatahkim.org/content/CmsFiles/"

# Decision header within an issue, e.g. "01.02.2024 Tarih ve K-2024/123 Sayılı Hakem Kararı".
_DECISION_HEADER_RE = re.compile(
    r"(\d{2}\.\d{2}\.\d{4}\s+Tarih\s+ve\s+K-\d{4}/\d+\s+Sayılı\s+Hakem\s+Kararı)"
)
# Minimum body length to tell a real decision apart from a table-of-contents line.
_MIN_BODY_LENGTH = 1000

# Turkish-aware lowercase: İ→i, I→ı (str.lower() mishandles these).
_TR_LOWER = str.maketrans("İIÇĞÖŞÜ", "iıçğöşü")


def turkish_lower(text: str) -> str:
    return text.translate(_TR_LOWER).lower()


def pdf_filename(issue_number: int) -> str:
    """Map a journal issue number to its PDF filename on the commission's CMS."""
    if issue_number == 4:
        return "karardergisisayi4.pdf"
    if 57 <= issue_number <= 61:
        return f"revizekd{issue_number}.pdf"
    return f"karardrgs{issue_number}.pdf"


def split_into_decisions(markdown: str) -> list[tuple[str, str]]:
    """Split an issue's text into (header, body) pairs, dropping TOC-only entries."""
    parts = _DECISION_HEADER_RE.split(markdown)
    decisions: list[tuple[str, str]] = []
    for i in range(1, len(parts) - 1, 2):
        header = parts[i].strip()
        body = parts[i + 1].strip() if i + 1 < len(parts) else ""
        if len(body) >= _MIN_BODY_LENGTH:
            decisions.append((header, body))
    return decisions


class SigortaTahkimClient(BaseClient):
    """Downloads and converts Sigorta Tahkim journal-issue PDFs."""

    base_url = ""

    def __init__(self, *, timeout: float = 120.0, **kwargs: Any) -> None:
        # Journals are large (~2 MB) and the host needs the legacy TLS stack.
        super().__init__(timeout=timeout, **kwargs)
        self._client.close()
        self._client = httpx.Client(
            base_url=self.base_url,
            headers=self.default_headers,
            timeout=timeout,
            verify=legacy_ssl_context(),
            follow_redirects=True,
        )

    def download_issue_markdown(self, issue_number: str) -> tuple[str, str]:
        """Return (markdown, pdf_url) for a journal issue number."""
        try:
            number = int(issue_number)
        except (TypeError, ValueError) as exc:
            raise ValueError(f"issue_number must be an integer, got {issue_number!r}") from exc
        pdf_url = f"{PDF_BASE_URL}{pdf_filename(number)}"
        raw, _ = self.get_bytes(pdf_url)
        return pdf_to_markdown(raw).strip(), pdf_url
