"""Tests for the office system persona (korkut.persona.build_persona)."""

from korkut.brain.persona import PersonaCfg, build_persona


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


def test_empty_persona_cfg_leaves_core_unchanged():
    assert build_persona("X Hukuk") == build_persona("X Hukuk", PersonaCfg())
    assert build_persona("X Hukuk", PersonaCfg("  ", "", "  ")) == build_persona("X Hukuk")


def test_persona_cfg_appends_office_tuning_after_core():
    cfg = PersonaCfg(tone="resmi ve öz", expertise="ceza, aile", extra_rules="kısa yanıt ver")
    p = build_persona("X Hukuk", cfg)
    # Core guardrails still present...
    assert "Citation discipline" in p
    # ...and the office additions are appended.
    assert "Üslup: resmi ve öz" in p
    assert "Uzmanlık alanları: ceza, aile" in p
    assert "Ek kurallar: kısa yanıt ver" in p
    assert p.index("Citation discipline") < p.index("Ofisin tercihleri")


def test_persona_cfg_skips_blank_fields():
    p = build_persona("X Hukuk", PersonaCfg(tone="resmi", expertise="", extra_rules=""))
    assert "Üslup: resmi" in p
    assert "Uzmanlık alanları" not in p
    assert "Ek kurallar" not in p


def test_answer_injects_office_memory(monkeypatch):
    # The office's durable notes are appended to the system message before the model call.
    from korkut.brain import agent

    captured: dict[str, str] = {}

    class FakeRouter:
        def respond(self, specialty, task_type, messages, **kwargs):
            captured["system"] = messages[0]["content"]
            return "ok"

    monkeypatch.setattr(agent, "_router", FakeRouter())
    agent.answer("merhaba", office_name="X Hukuk", memory="- video duruşma tercih edilir")
    assert "Ofis hafızası" in captured["system"]
    assert "- video duruşma tercih edilir" in captured["system"]


def test_answer_without_memory_has_no_memory_block(monkeypatch):
    from korkut.brain import agent

    captured: dict[str, str] = {}

    class FakeRouter:
        def respond(self, specialty, task_type, messages, **kwargs):
            captured["system"] = messages[0]["content"]
            return "ok"

    monkeypatch.setattr(agent, "_router", FakeRouter())
    agent.answer("merhaba", office_name="X Hukuk")
    assert "Ofis hafızası" not in captured["system"]
