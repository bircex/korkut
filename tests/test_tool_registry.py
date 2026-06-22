"""Tests for core.tool_registry — discovery, key gating, run dispatch."""

from core import tool_registry
from core.tool_registry import ToolSpec

EXPECTED = {
    "search_yargitay",
    "search_danistay",
    "search_emsal",
    "search_mevzuat",
    "search_anayasa",
    "search_kik",
    "search_rekabet",
    "search_sayistay",
    "search_gib",
    "fetch_decision",
    "fetch_legislation",
}


def test_discovers_all_tools():
    names = {s.name for s in tool_registry.discover()}
    assert EXPECTED <= names, f"missing: {EXPECTED - names}"


def test_core_tools_have_no_required_key():
    for spec in tool_registry.discover():
        assert spec.requires_key is None
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
