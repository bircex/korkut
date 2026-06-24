"""Tests for core.router — backend selection, fallback, and redaction enforcement."""

import pytest

from core.agent_loop import StepResult
from core.router import Router

CFG = {
    "backends": {"cloud": "gpt", "local": "ollama"},
    "specialties": {"research": {"model": "cloud"}, "hearing": {"model": "local"}},
    "fallback_to_cloud_if_no_local": True,
}


class FakeProvider:
    """Provider that returns one final answer and records what step() saw."""

    def __init__(self, reply="ok"):
        self.reply = reply
        self.seen: list | None = None

    def step(self, messages, tools):
        self.seen = messages
        return StepResult(text=self.reply)


def test_cloud_redacts_then_restores():
    gpt = FakeProvider(reply="Sayın [PERSON_1], TCKN [TCKN_1] doğrulandı.")
    router = Router({"gpt": gpt, "ollama": FakeProvider()}, config=CFG)
    out = router.respond(
        "research", "qa", [{"role": "user", "content": "Ahmet Yılmaz TCKN 10000000146"}]
    )
    assert "Ahmet Yılmaz" not in gpt.seen[0]["content"]
    assert "10000000146" not in gpt.seen[0]["content"]
    assert out == "Sayın Ahmet Yılmaz, TCKN 10000000146 doğrulandı."


def test_local_is_not_redacted():
    ollama = FakeProvider()
    router = Router(
        {"gpt": FakeProvider(), "ollama": ollama}, config=CFG, local_available=lambda: True
    )
    raw = [{"role": "user", "content": "Ahmet Yılmaz TCKN 10000000146"}]
    router.respond("hearing", "add", raw)
    assert ollama.seen[0]["content"] == "Ahmet Yılmaz TCKN 10000000146"


def test_local_falls_back_to_cloud_when_unavailable():
    gpt = FakeProvider(reply="done")
    router = Router(
        {"gpt": gpt, "ollama": FakeProvider()}, config=CFG, local_available=lambda: False
    )
    router.respond("hearing", "add", [{"role": "user", "content": "test"}])
    assert gpt.seen is not None


def test_fallback_disabled_raises():
    cfg = {**CFG, "fallback_to_cloud_if_no_local": False}
    router = Router(
        {"gpt": FakeProvider(), "ollama": FakeProvider()}, config=cfg, local_available=lambda: False
    )
    with pytest.raises(RuntimeError):
        router.respond("hearing", "add", [{"role": "user", "content": "x"}])
