"""Tests for core.providers.groq — OpenAI-compatible step() reused from GptProvider."""

from korkut.core.providers.groq import DEFAULT_MODEL, GroqProvider
from korkut.core.tool_registry import ToolSpec

SPEC = ToolSpec(
    "search_yargitay",
    "Search.",
    {"query": {"type": "string", "required": True}},
    None,
    "korkut.tools.search_yargitay",
)


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


def test_default_model_is_groq():
    assert "llama" in DEFAULT_MODEL
    assert GroqProvider()._model == DEFAULT_MODEL


def test_step_text_only():
    provider = GroqProvider(client=_Client(_Msg(content="merhaba")))
    result = provider.step([{"role": "user", "content": "selam"}], [])
    assert result.text == "merhaba" and result.tool_calls == []


def test_step_parses_tool_calls_and_sends_model():
    client = _Client(
        _Msg(content=None, tool_calls=[_TC("c1", "search_yargitay", '{"query": "kira"}')])
    )
    provider = GroqProvider(model="llama-3.1-8b-instant", client=client)
    result = provider.step([{"role": "user", "content": "q"}], [SPEC])
    assert result.tool_calls == [{"id": "c1", "name": "search_yargitay", "args": {"query": "kira"}}]
    # The chosen model is forwarded to the OpenAI-compatible endpoint.
    assert client.chat.completions.calls[-1]["model"] == "llama-3.1-8b-instant"
