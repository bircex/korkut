"""Tests for core.agent_loop — the provider-agnostic tool-running loop."""

from core import agent_loop, tool_registry
from core.agent_loop import StepResult
from core.tool_registry import ToolSpec

SPEC = ToolSpec("search_yargitay", "d", {"query": {"type": "string"}}, None, "tools.search_yargitay")


class ScriptedProvider:
    """Returns a scripted sequence of StepResults; records what each step saw."""

    def __init__(self, script):
        self.script = list(script)
        self.seen = []

    def step(self, messages, tools):
        self.seen.append([dict(m) for m in messages])
        return self.script.pop(0)


def test_loop_runs_tool_then_returns_final(monkeypatch):
    monkeypatch.setattr(tool_registry, "run", lambda name, args: {"count": 1, "q": args.get("query")})
    provider = ScriptedProvider(
        [
            StepResult(text="", tool_calls=[{"id": "c1", "name": "search_yargitay", "args": {"query": "kira"}}]),
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
    # the tool result fed back carried the error
    tool_turn = next(m for m in provider.seen[1] if m["role"] == "tool")
    assert "nope" in tool_turn["tool_results"][0]["output"]
