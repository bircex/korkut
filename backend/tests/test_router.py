"""Tests for core.router — backend selection, fallback, and redaction enforcement."""

import pytest

from korkut.core.agent_loop import StepResult
from korkut.core.router import ModelCfg, Router, single_model_router

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


def test_cloud_masks_known_entities_the_detectors_miss():
    # "Oğuzhan Karaca" and the address are not caught by the base detectors; only the DB-known
    # entities (Layer 4) mask them before the cloud call.
    gpt = FakeProvider(reply="ok")
    router = Router({"gpt": gpt, "ollama": FakeProvider()}, config=CFG)
    router.respond(
        "research",
        "qa",
        [{"role": "user", "content": "Müvekkil Oğuzhan Karaca, adres Bağdat Cad. No:12."}],
        known_entities=[("Oğuzhan Karaca", "PERSON"), ("Bağdat Cad. No:12", "ADDRESS")],
    )
    assert "Oğuzhan Karaca" not in gpt.seen[0]["content"]
    assert "Bağdat Cad. No:12" not in gpt.seen[0]["content"]


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


def test_default_tier_routes_unlisted_specialty():
    # An unlisted specialty resolves to default_tier (here: local → no redaction).
    local = FakeProvider()
    cfg = {"backends": {"local": "x"}, "default_tier": "local"}
    router = Router({"x": local}, config=cfg, local_available=lambda: True)
    router.respond("anything", "qa", [{"role": "user", "content": "Ahmet Yılmaz"}])
    assert local.seen[0]["content"] == "Ahmet Yılmaz"


def test_single_model_router_local_skips_redaction():
    cfg = ModelCfg(model="m", base_url="http://localhost:11434/v1", local=True)
    router = single_model_router(cfg)
    fake = FakeProvider()
    router._providers["custom"] = fake  # swap the real OpenAI-compat client out
    raw = [{"role": "user", "content": "Ahmet Yılmaz TCKN 10000000146"}]
    router.respond("research", "qa", raw)
    assert fake.seen[0]["content"] == "Ahmet Yılmaz TCKN 10000000146"


def test_single_model_router_cloud_redacts():
    cfg = ModelCfg(model="m", base_url="https://api.example.com/v1", local=False)
    router = single_model_router(cfg)
    fake = FakeProvider(reply="ok")
    router._providers["custom"] = fake
    router.respond("research", "qa", [{"role": "user", "content": "Ahmet Yılmaz TCKN 10000000146"}])
    assert "Ahmet Yılmaz" not in fake.seen[0]["content"]
    assert "10000000146" not in fake.seen[0]["content"]
