"""Tests for core.tool_registry — discovery, key gating, run dispatch."""

from korkut.core import tool_registry
from korkut.core.tool_registry import ToolSpec

EXPECTED = {
    "search_yargitay",
    "search_danistay",
    "search_emsal",
    "search_kyb",
    "search_mevzuat",
    "search_anayasa",
    "search_uyusmazlik",
    "search_kik",
    "search_rekabet",
    "search_sayistay",
    "search_gib",
    "search_kvkk",
    "search_bddk",
    "search_sigorta_tahkim",
    "search_within_sigorta_tahkim",
    "fetch_decision",
    "fetch_legislation",
    "fetch_uyusmazlik",
    "fetch_kvkk",
    "fetch_bddk",
    "fetch_sigorta_tahkim",
}

# Tools that go through the Tavily web-search provider (no real source API).
# See tools/README.md "Sources & keys".
KEY_GATED = {
    "search_kvkk": "TAVILY_API_KEY",
    "search_bddk": "TAVILY_API_KEY",
    "search_sigorta_tahkim": "TAVILY_API_KEY",
}


def test_discovers_all_tools():
    names = {s.name for s in tool_registry.discover()}
    assert names >= EXPECTED, f"missing: {EXPECTED - names}"


def test_required_keys_match_documented_set():
    """Only the documented web-search-provider tools may declare a required key."""
    for spec in tool_registry.discover():
        if spec.name in KEY_GATED:
            assert spec.requires_key == KEY_GATED[spec.name]
        else:
            assert spec.requires_key is None, f"{spec.name} unexpectedly requires a key"
            assert spec.available


def test_available_gating_when_key_missing():
    spec = ToolSpec(
        name="x", description="", parameters={}, requires_key="SOME_MISSING_KEY", module="tools.x"
    )
    assert spec.available is False


def test_unknown_tool_raises():
    import pytest

    with pytest.raises(KeyError):
        tool_registry.run("nope", {})
