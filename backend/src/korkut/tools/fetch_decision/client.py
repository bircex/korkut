"""Fetch a court decision's full text as Markdown via the Bedesten API.

Works for any Bedesten-sourced decision (Yargıtay, Danıştay, local, appellate, KYB);
the documentId comes from a search_* tool result.
"""

from __future__ import annotations

from typing import Any

from korkut.tools._base.bedesten import BedestenClient


def run_tool(document_id: str) -> dict[str, Any]:
    """Fetch the full decision text (Markdown) for a Bedesten documentId."""
    if not document_id:
        raise ValueError("document_id is required")
    with BedestenClient() as client:
        return client.document_markdown(document_id)
