"""Tests for core.providers.claude — one-turn step() with a faked Anthropic client."""

from core import tool_registry
from core.providers.claude import ClaudeProvider
from core.tool_registry import ToolSpec

SPEC = ToolSpec(
    "search_yargitay",
    "Search.",
    {"query": {"type": "string", "required": True}},
    None,
    "tools.search_yargitay",
)


class _Text:
    type = "text"

    def __init__(self, text):
        self.text = text


class _ToolUse:
    type = "tool_use"

    def __init__(self, id, name, input):
        self.id, self.name, self.input = id, name, input


class _Message:
    def __init__(self, stop_reason, content):
        self.stop_reason, self.content = stop_reason, content


class _Messages:
    def __init__(self, resp):
        self.resp, self.calls = resp, []

    def create(self, **kwargs):
        self.calls.append(kwargs)
        return self.resp


class _Client:
    def __init__(self, resp):
        self.messages = _Messages(resp)


def test_step_parses_tool_use():
    provider = ClaudeProvider(
        client=_Client(_Message("tool_use", [_ToolUse("t1", "search_yargitay", {"query": "kira"})]))
    )
    result = provider.step([{"role": "user", "content": "q"}], [SPEC])
    assert result.tool_calls == [{"id": "t1", "name": "search_yargitay", "args": {"query": "kira"}}]


def test_step_text_only():
    provider = ClaudeProvider(client=_Client(_Message("end_turn", [_Text("merhaba")])))
    assert provider.step([{"role": "user", "content": "selam"}], []).text == "merhaba"


def test_step_pulls_system_out_and_serializes_tools():
    provider = ClaudeProvider(client=_Client(_Message("end_turn", [_Text("ok")])))
    messages = [
        {"role": "system", "content": "persona"},
        {"role": "user", "content": "soru"},
        {
            "role": "assistant",
            "content": "",
            "tool_calls": [{"id": "t1", "name": "search_yargitay", "args": {"query": "k"}}],
        },
        {"role": "tool", "tool_results": [{"id": "t1", "name": "search_yargitay", "output": "{}"}]},
    ]
    provider.step(messages, [SPEC])
    call = provider._client.messages.calls[0]
    assert call["system"] == "persona"
    assert [m["role"] for m in call["messages"]] == ["user", "assistant", "user"]
    assert call["messages"][1]["content"][-1]["type"] == "tool_use"
    assert call["messages"][2]["content"][0]["type"] == "tool_result"


def test_refusal():
    provider = ClaudeProvider(client=_Client(_Message("refusal", [])))
    assert "güvenlik" in provider.step([{"role": "user", "content": "x"}], []).text


def test_to_anthropic_tools_schema():
    tools = tool_registry.to_anthropic_tools([SPEC])
    assert tools[0]["name"] == "search_yargitay"
    assert tools[0]["input_schema"]["required"] == ["query"]
