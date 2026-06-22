"""Model router — picks local/cloud per specialty, applies redaction, single chokepoint.

Phase 1 (not yet implemented). See MIMARI.md §4 and §13. The agent loop calls
`cevapla(uzmanlik, is_tipi, mesajlar)`; the router selects the provider via
config/model-routing.yaml, falls back to cloud if the local model is down, and routes every
cloud-bound message through `core.redactor` first.
"""

from __future__ import annotations

from typing import Any


def cevapla(uzmanlik: str, is_tipi: str, mesajlar: list[dict[str, Any]]) -> str:  # noqa: ARG001
    raise NotImplementedError("Phase 1: model router not yet implemented")
