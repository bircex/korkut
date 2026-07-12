"""Tests for core.agent_loop — the provider-agnostic tool-running loop."""

import pytest

from korkut.core import agent_loop, tool_registry
from korkut.core.agent_loop import StepResult
from korkut.core.tool_registry import ToolSpec

SPEC = ToolSpec(
    "search_yargitay", "d", {"query": {"type": "string"}}, None, "tools.search_yargitay"
)


class ScriptedProvider:
    """Returns a scripted sequence of StepResults; records what each step saw."""

    def __init__(self, script):
        self.script = list(script)
        self.seen = []

    def step(self, messages, tools):
        self.seen.append([dict(m) for m in messages])
        return self.script.pop(0)


def test_loop_runs_tool_then_returns_final(monkeypatch):
    monkeypatch.setattr(
        tool_registry, "run", lambda name, args: {"count": 1, "q": args.get("query")}
    )
    provider = ScriptedProvider(
        [
            StepResult(
                text="",
                tool_calls=[{"id": "c1", "name": "search_yargitay", "args": {"query": "kira"}}],
            ),
            StepResult(text="Buldum."),
        ]
    )
    out = agent_loop.run(provider, [{"role": "user", "content": "kira emsali"}], [SPEC])
    assert out == "Buldum."
    # second step saw the appended assistant tool-call turn + tool results
    second = provider.seen[1]
    assert any(m["role"] == "assistant" and m.get("tool_calls") for m in second)
    assert any(m["role"] == "tool" for m in second)


def test_loop_returns_directly_when_no_tools():
    provider = ScriptedProvider([StepResult(text="merhaba")])
    assert agent_loop.run(provider, [{"role": "user", "content": "selam"}], []) == "merhaba"


def test_tool_error_is_fed_back_not_raised(monkeypatch):
    def boom(name, args):
        raise ValueError("nope")

    monkeypatch.setattr(tool_registry, "run", boom)
    provider = ScriptedProvider(
        [
            StepResult(tool_calls=[{"id": "c1", "name": "search_yargitay", "args": {}}]),
            StepResult(text="handled"),
        ]
    )
    out = agent_loop.run(provider, [{"role": "user", "content": "x"}], [SPEC])
    assert out == "handled"
    # the tool error is fed back as a generic marker, without the raw exception text (no leak)
    tool_turn = next(m for m in provider.seen[1] if m["role"] == "tool")
    out_json = tool_turn["tool_results"][0]["output"]
    assert "tool_failed" in out_json and "nope" not in out_json


def test_provider_failure_after_tool_marks_non_retriable(monkeypatch):
    # a provider fault AFTER a tool ran (possibly a write) must be flagged non-retriable so a
    # full re-run of answer() can't duplicate side effects.
    monkeypatch.setattr(tool_registry, "run", lambda name, args: {"ok": True})

    class FailAfterTool:
        def __init__(self):
            self.calls = 0

        def step(self, messages, tools):
            self.calls += 1
            if self.calls == 1:
                return StepResult(tool_calls=[{"id": "c1", "name": "search_yargitay", "args": {}}])
            raise ConnectionError("blip")

    with pytest.raises(ConnectionError) as ei:
        agent_loop.run(FailAfterTool(), [{"role": "user", "content": "x"}], [SPEC])
    assert getattr(ei.value, "_korkut_no_retry", False) is True


def test_provider_failure_before_any_tool_stays_retriable():
    class FailFirst:
        def step(self, messages, tools):
            raise ConnectionError("blip")

    with pytest.raises(ConnectionError) as ei:
        agent_loop.run(FailFirst(), [{"role": "user", "content": "x"}], [SPEC])
    assert getattr(ei.value, "_korkut_no_retry", False) is False
