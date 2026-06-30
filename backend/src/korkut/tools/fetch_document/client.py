"""Composite fetch tool — one `source`-routed entry point over every legal full-text fetch.

Each source's underlying fetch_* tool names its identifier differently (document_id, decision_url,
issue_number, document_url, legislation_id); this layer maps the generic `id` to the right name and
forwards the supported extras (article_id, page, page_size) read from the target tool's own spec.
"""

from __future__ import annotations

from typing import Any

# Composite source → (underlying tool name, the underlying's identifier param name).
_SOURCES: dict[str, tuple[str, str]] = {
    "decision": ("fetch_decision", "document_id"),
    "legislation": ("fetch_legislation", "legislation_id"),
    "bddk": ("fetch_bddk", "document_id"),
    "kvkk": ("fetch_kvkk", "decision_url"),
    "sigorta_tahkim": ("fetch_sigorta_tahkim", "issue_number"),
    "uyusmazlik": ("fetch_uyusmazlik", "document_url"),
}


def run_tool(source: str, id: str, **params: Any) -> dict[str, Any]:
    from korkut.core import tool_registry

    entry = _SOURCES.get((source or "").strip().lower())
    if entry is None:
        return {
            "error": "unknown_source",
            "detail": f"Bilinmeyen kaynak: {source!r}. Geçerli: {', '.join(_SOURCES)}",
        }
    name, id_param = entry
    spec = next((s for s in tool_registry.discover() if s.name == name), None)
    accepted = set((spec.parameters or {}).keys()) if spec else set()
    call = {id_param: id}
    call.update({k: v for k, v in params.items() if k in accepted and v is not None})
    return tool_registry.run(name, call)
