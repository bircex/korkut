"""Tests for core.providers.gpt — tool-calling loop with a faked OpenAI client."""

from core import tool_registry
from core.providers.gpt import GptProvider
from core.tool_registry import ToolSpec


# --- minimal fakes mimicking the OpenAI response shape we read ---
class _Fn:
    def __init__(self, name, args):
        self.name = name
        self.arguments = args


class _TC:
    def __init__(self, id, name, args):
        self.id = id
        self.type = "function"
        self.function = _Fn(name, args)


class _Msg:
    def __init__(self, content=None, tool_calls=None):
        self.content = content
        self.tool_calls = tool_calls


class _Resp:
    def __init__(self, msg):
        self.choices = [type("C", (), {"message": msg})()]


class _Completions:
    def __init__(self, script):
        self.script = script
        self.calls = []

    def create(self, **kwargs):
        self.calls.append(kwargs)
        return _Resp(self.script.pop(0))


class _Client:
    def __init__(self, script):
        self.chat = type("Chat", (), {"completions": _Completions(script)})()


def test_tool_loop_executes_tool_then_returns_final(monkeypatch):
    # Stub the actual tool so no network is hit.
    monkeypatch.setattr(
        tool_registry, "run", lambda name, args: {"count": 1, "query": args.get("query")}
    )
    script = [
        _Msg(content=None, tool_calls=[_TC("c1", "search_yargitay", '{"query": "kira"}')]),
        _Msg(content="Bir karar buldum."),
    ]
    provider = GptProvider(client=_Client(script))
    spec = ToolSpec("search_yargitay", "desc", {"query": {"type": "string", "required": True}}, None, "tools.search_yargitay")

    out = provider.generate([{"role": "user", "content": "kira emsali"}], tools=[spec])

    assert out == "Bir karar buldum."
    # second model call must include the tool result message
    second_call_messages = provider._client.chat.completions.calls[1]["messages"]
    assert any(m.get("role") == "tool" for m in second_call_messages)


def test_no_tools_returns_direct_answer():
    provider = GptProvider(client=_Client([_Msg(content="merhaba")]))
    out = provider.generate([{"role": "user", "content": "selam"}])
    assert out == "merhaba"


def test_to_openai_tools_schema():
    spec = ToolSpec(
        "search_yargitay",
        "Search Yargıtay.",
        {
            "query": {"type": "string", "required": True, "description": "phrase"},
            "limit": {"type": "integer", "default": 10},
        },
        None,
        "tools.search_yargitay",
    )
    tools = tool_registry.to_openai_tools([spec])
    fn = tools[0]["function"]
    assert fn["name"] == "search_yargitay"
    assert fn["parameters"]["properties"]["query"]["type"] == "string"
    assert fn["parameters"]["required"] == ["query"]
