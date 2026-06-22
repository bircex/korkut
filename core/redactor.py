"""Redaction — pseudonymize PII before any cloud call, reverse it on the response.

Phase 1 (not yet implemented). See MIMARI.md §5. Detects TCKN (11-digit + checksum),
phone, IBAN, plate, case file number, and person names; replaces with tokens; keeps the
map in RAM per request. Text bound for a local model is not redacted.
"""

from __future__ import annotations


def rumuzla(metin: str) -> tuple[str, dict[str, str]]:
    raise NotImplementedError("Phase 1: redactor not yet implemented")


def geri_ac(metin: str, harita: dict[str, str]) -> str:  # noqa: ARG001
    raise NotImplementedError("Phase 1: redactor not yet implemented")
