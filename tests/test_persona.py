"""Tests for the per-office system persona (korkut.persona.build_persona)."""

from korkut.persona import build_persona


def test_persona_includes_office_name():
    p = build_persona("Falanca Hukuk")
    assert "Falanca Hukuk" in p
    assert "Cankar" not in p  # the old hardcoded name must be gone


def test_persona_neutral_without_office():
    for office in (None, "", "   "):
        p = build_persona(office)
        assert "a Turkish law office" in p
        assert "Cankar" not in p


def test_persona_keeps_guardrails():
    p = build_persona("X Hukuk")
    # Core guardrails survive the templating.
    assert "Citation discipline" in p
    assert "GÜVENİLMEZ İÇERİK" in p
