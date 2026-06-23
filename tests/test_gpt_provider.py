"""Tests for core.providers.gpt — one-turn step() with a faked OpenAI client."""

from core import tool_registry
from core.providers.gpt import GptProvider
from core.tool_registry import ToolSpec

SPEC = ToolSpec("search_yargitay", "Search.", {"query": {"type": "string", "required": True}}, None, "tools.search_yargitay")


class _Fn:
    def __init__(self, name, args):
        self.name = name
        self.arguments = args


class _TC:
    def __init__(self, id, name, args):
        self.id, self.function = id, _Fn(name, args)


class _Msg:
    def __init__(self, content=None, tool_calls=None):
        self.content, self.tool_calls = content, tool_calls


class _Completions:
    def __init__(self, resp):
        self.resp, self.calls = resp, []

    def create(self, **kwargs):
        self.calls.append(kwargs)
        return type("R", (), {"choices": [type("C", (), {"message": self.resp})()]})


class _Client:
    def __init__(self, resp):
        self.chat = type("Chat", (), {"completions": _Completions(resp)})()


def test_step_parses_tool_calls():
    provider = GptProvider(client=_Client(_Msg(content=None, tool_calls=[_TC("c1", "search_yargitay", '{"query": "kira"}')])))
    result = provider.step([{"role": "user", "content": "q"}], [SPEC])
    assert result.tool_calls == [{"id": "c1", "name": "search_yargitay", "args": {"query": "kira"}}]


def test_step_text_only():
    provider = GptProvider(client=_Client(_Msg(content="merhaba")))
    result = provider.step([{"role": "user", "content": "selam"}], [])
    assert result.text == "merhaba" and result.tool_calls == []


def test_to_wire_serializes_full_history():
    provider = GptProvider(client=_Client(_Msg(content="ok")))
    messages = [
        {"role": "system", "content": "persona"},
        {"role": "user", "content": "soru"},
        {"role": "assistant", "content": "", "tool_calls": [{"id": "c1", "name": "search_yargitay", "args": {"query": "k"}}]},
        {"role": "tool", "tool_results": [{"id": "c1", "name": "search_yargitay", "output": "{}"}]},
    ]
    provider.step(messages, [SPEC])
    wire = provider._client.chat.completions.calls[0]["messages"]
    roles = [m["role"] for m in wire]
    assert roles == ["system", "user", "assistant", "tool"]
    assert wire[2]["tool_calls"][0]["function"]["name"] == "search_yargitay"
    assert wire[3]["tool_call_id"] == "c1"


def test_to_openai_tools_schema():
    tools = tool_registry.to_openai_tools([SPEC])
    fn = tools[0]["function"]
    assert fn["name"] == "search_yargitay"
    assert fn["parameters"]["required"] == ["query"]
