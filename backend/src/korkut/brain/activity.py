"""Human-readable activity labels for the agent's tool calls.

While the model works it calls tools (search a court's case-law, fetch a document). Surfacing each
call as a short Turkish line — "Yargıtay'da arıyorum…" — lets the user see progress instead of
staring at a spinner (the ChatGPT "Searching…" affordance). The SAME label feeds both the web SSE
stream and the Telegram interim message, so the experience matches across channels.

Labels are derived from the tool name (a `search_*` / `fetch_*` verb + a known source) with a plain
fallback, so a new tool still produces something sensible without a code change here.
"""

from __future__ import annotations

# Court / registry sources, keyed by the suffix used in tool names (search_<suffix>).
_SOURCES = {
    "yargitay": "Yargıtay",
    "danistay": "Danıştay",
    "anayasa": "Anayasa Mahkemesi",
    "emsal": "emsal kararlar",
    "uyusmazlik": "Uyuşmazlık Mahkemesi",
    "kik": "KİK",
    "rekabet": "Rekabet Kurumu",
    "sayistay": "Sayıştay",
    "kvkk": "KVKK",
    "bddk": "BDDK",
    "gib": "GİB",
    "sigorta_tahkim": "Sigorta Tahkim",
    "kyb": "kanun yararına bozma",
    "mevzuat": "mevzuat",
    "legislation": "mevzuat",
    "decisions": "içtihat",
    "decision": "karar",
    "document": "belge",
}


def tool_label(name: str, args: dict | None = None) -> str:
    """A short present-tense Turkish line describing a tool call (for progress display)."""
    args = args or {}
    query = str(args.get("query") or args.get("q") or "").strip()
    suffix = name.split("_", 1)[1] if "_" in name else name
    source = _SOURCES.get(suffix, suffix.replace("_", " "))

    if name.startswith("search"):
        base = f"{source}'da arıyorum" if source else "arıyorum"
        return f"{base}: “{query[:60]}”" if query else base
    if name.startswith("fetch"):
        return f"{source} belgesini getiriyorum" if source else "Belgeyi getiriyorum"
    if name == "dilekce_canvas":
        return "Dilekçeyi hazırlıyorum"
    if name == "send_email":
        return "E-posta taslağı hazırlıyorum"
    if name == "remember":
        return "Not alıyorum"
    return "Çalışıyorum…"
