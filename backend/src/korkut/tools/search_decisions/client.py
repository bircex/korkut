"""Composite search tool — one `source`-routed entry point over every legal-database search.

Dispatches to the underlying per-source `search_*` tool via the registry. Accepted params are read
from the target tool's own spec, so each source only receives the params it actually supports
(e.g. `chamber` reaches yargitay/danistay but is dropped for gib) and nothing drifts out of sync.
"""

from __future__ import annotations

from typing import Any

# Composite source → underlying tool name. The underlying tools keep all their real logic; this
# layer only picks one and forwards the supported params.
_SOURCES: dict[str, str] = {
    "yargitay": "search_yargitay",
    "danistay": "search_danistay",
    "emsal": "search_emsal",
    "kyb": "search_kyb",
    "anayasa": "search_anayasa",
    "bddk": "search_bddk",
    "kvkk": "search_kvkk",
    "sigorta_tahkim": "search_sigorta_tahkim",
    "gib": "search_gib",
    "mevzuat": "search_mevzuat",
    "rekabet": "search_rekabet",
    "sayistay": "search_sayistay",
    "kik": "search_kik",
    "uyusmazlik": "search_uyusmazlik",
}


def run_tool(source: str, query: str, **params: Any) -> dict[str, Any]:
    from korkut.core import tool_registry

    name = _SOURCES.get((source or "").strip().lower())
    if name is None:
        return {
            "error": "unknown_source",
            "detail": f"Bilinmeyen kaynak: {source!r}. Geçerli: {', '.join(_SOURCES)}",
        }
    spec = next((s for s in tool_registry.discover() if s.name == name), None)
    accepted = set((spec.parameters or {}).keys()) if spec else set()
    # Forward query + only the optional params the target supports and that were actually provided.
    call = {"query": query}
    call.update({k: v for k, v in params.items() if k in accepted and v is not None})
    return tool_registry.run(name, call)
