"""Tests for the streaming / activity-event plumbing (Adım 3a).

No network: a fake provider with a `step_stream` method drives the agent loop, and we assert the
loop emits token + tool events. Also covers the activity labels and the cloud path's event
handling (token deltas suppressed, tool args restored).
"""

from __future__ import annotations

from korkut.brain import activity
from korkut.core import agent_loop
from korkut.core.agent_loop import StepResult


class _StreamingProvider:
    """Emits a scripted sequence of turns; each turn streams its text through `on_token`."""

    def __init__(self, turns):
        self._turns = list(turns)
        self._i = 0

    def step_stream(self, messages, tools, *, on_token):
        turn = self._turns[self._i]
        self._i += 1
        for piece in turn.get("tokens", []):
            on_token(piece)
        return StepResult(text="".join(turn.get("tokens", [])), tool_calls=turn.get("calls", []))

    def step(self, messages, tools):  # non-streaming fallback (real providers have both)
        turn = self._turns[self._i]
        self._i += 1
        return StepResult(text="".join(turn.get("tokens", [])), tool_calls=turn.get("calls", []))


class _PlainProvider:
    """No `step_stream` — exercises the non-streaming fallback (tool events, no tokens)."""

    def __init__(self, turns):
        self._turns = list(turns)
        self._i = 0

    def step(self, messages, tools):
        turn = self._turns[self._i]
        self._i += 1
        return StepResult(text=turn.get("text", ""), tool_calls=turn.get("calls", []))


def test_loop_streams_tokens_and_emits_tool_events():
    provider = _StreamingProvider(
        [
            {"calls": [{"id": "1", "name": "search_yargitay", "args": {"query": "kira"}}]},
            {"tokens": ["Bul", "dum: ", "karar."]},
        ]
    )
    events = []
    out = agent_loop.run(provider, [{"role": "user", "content": "soru"}], on_event=events.append)

    assert out == "Buldum: karar."
    tokens = [e["text"] for e in events if e["type"] == "token"]
    assert "".join(tokens) == "Buldum: karar."
    tools = [e for e in events if e["type"] == "tool"]
    assert tools == [{"type": "tool", "name": "search_yargitay", "args": {"query": "kira"}}]


def test_non_streaming_provider_still_emits_tool_events():
    provider = _PlainProvider(
        [
            {"calls": [{"id": "1", "name": "fetch_document", "args": {}}]},
            {"text": "tamam"},
        ]
    )
    events = []
    out = agent_loop.run(provider, [{"role": "user", "content": "x"}], on_event=events.append)

    assert out == "tamam"
    assert [e["type"] for e in events] == ["tool"]  # no token events without step_stream


def test_no_events_when_callback_absent():
    provider = _StreamingProvider([{"tokens": ["merhaba"]}])
    # Must not raise and must not stream when on_event is None (falls back to .step if present).
    out = agent_loop.run(provider, [{"role": "user", "content": "x"}])
    assert out == "merhaba"


def test_activity_labels():
    assert "Yargıtay" in activity.tool_label("search_yargitay", {"query": "kira sözleşmesi"})
    assert "kira sözleşmesi" in activity.tool_label("search_yargitay", {"query": "kira sözleşmesi"})
    assert "getir" in activity.tool_label("fetch_document", {}).lower()
    assert activity.tool_label("dilekce_canvas") == "Dilekçeyi hazırlıyorum"
    assert activity.tool_label("büsbütün_yeni_araç")  # unknown → non-empty fallback
