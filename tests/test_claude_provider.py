"""Tests for core.providers.claude — tool-calling loop with a faked Anthropic client."""

from core import tool_registry
from core.providers.claude import ClaudeProvider
from core.tool_registry import ToolSpec


class _Text:
    type = "text"

    def __init__(self, text):
        self.text = text


class _ToolUse:
    type = "tool_use"

    def __init__(self, id, name, input):
        self.id = id
        self.name = name
        self.input = input


class _Message:
    def __init__(self, stop_reason, content):
        self.stop_reason = stop_reason
        self.content = content


class _Messages:
    def __init__(self, script):
        self.script = script
        self.calls = []

    def create(self, **kwargs):
        self.calls.append(kwargs)
        return self.script.pop(0)


class _Client:
    def __init__(self, script):
        self.messages = _Messages(script)


def test_tool_loop_executes_tool_then_returns_final(monkeypatch):
    monkeypatch.setattr(tool_registry, "run", lambda name, args: {"count": 1, "q": args.get("query")})
    script = [
        _Message("tool_use", [_ToolUse("t1", "search_yargitay", {"query": "kira"})]),
        _Message("end_turn", [_Text("Bir karar buldum.")]),
    ]
    provider = ClaudeProvider(client=_Client(script))
    spec = ToolSpec("search_yargitay", "desc", {"query": {"type": "string", "required": True}}, None, "tools.search_yargitay")

    out = provider.generate(
        [{"role": "system", "content": "Sen Korkut'sun."}, {"role": "user", "content": "kira emsali"}],
        tools=[spec],
    )

    assert out == "Bir karar buldum."
    # system was pulled out of messages into the top-level param
    first = provider._client.messages.calls[0]
    assert first["system"] == "Sen Korkut'sun."
    assert all(m["role"] in ("user", "assistant") for m in first["messages"])
    # second call carried the tool_result back
    second = provider._client.messages.calls[1]
    blocks = [
        b
        for m in second["messages"]
        if isinstance(m["content"], list)
        for b in m["content"]
        if isinstance(b, dict)
    ]
    assert any(b.get("type") == "tool_result" for b in blocks)


def test_direct_answer_without_tools():
    provider = ClaudeProvider(client=_Client([_Message("end_turn", [_Text("merhaba")])]))
    assert provider.generate([{"role": "user", "content": "selam"}]) == "merhaba"


def test_refusal_returns_safe_message():
    provider = ClaudeProvider(client=_Client([_Message("refusal", [])]))
    out = provider.generate([{"role": "user", "content": "..."}])
    assert "güvenlik" in out


def test_to_anthropic_tools_schema():
    spec = ToolSpec(
        "search_yargitay", "Search.", {"query": {"type": "string", "required": True}}, None, "tools.search_yargitay"
    )
    tools = tool_registry.to_anthropic_tools([spec])
    assert tools[0]["name"] == "search_yargitay"
    assert tools[0]["input_schema"]["required"] == ["query"]
