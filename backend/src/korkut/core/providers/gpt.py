"""OpenAI GPT provider — one model turn. The loop lives in core.agent_loop.

`step()` serializes the provider-agnostic conversation into OpenAI Chat Completions form,
makes a single API call, and returns a normalized StepResult (final text or tool calls).
Only redacted text reaches this provider (the router redacts first).
"""

from __future__ import annotations

import json
import os
from collections.abc import Callable
from typing import Any

from korkut.core import tool_registry, usage
from korkut.core.agent_loop import StepResult
from korkut.core.tool_registry import ToolSpec


def _record_usage(u: Any) -> None:
    """Add one API response's token usage to the request-scoped accumulator (if reported)."""
    if u is None:
        return
    usage.add(
        getattr(u, "prompt_tokens", 0) or 0,
        getattr(u, "completion_tokens", 0) or 0,
    )

DEFAULT_MODEL = os.getenv("OPENAI_MODEL", "gpt-4o")


class GptProvider:
    def __init__(self, *, model: str | None = None, client: Any = None) -> None:
        self._model = model or DEFAULT_MODEL
        self._client = client

    def _ensure_client(self) -> Any:
        if self._client is None:
            from openai import OpenAI  # imported lazily; only needed for live calls

            self._client = OpenAI()  # uses OPENAI_API_KEY
        return self._client

    @staticmethod
    def _to_wire(messages: list[dict[str, Any]]) -> list[dict[str, Any]]:
        wire: list[dict[str, Any]] = []
        for m in messages:
            role = m["role"]
            if role in ("system", "user"):
                wire.append({"role": role, "content": m.get("content", "")})
            elif role == "assistant":
                msg: dict[str, Any] = {"role": "assistant", "content": m.get("content") or None}
                if m.get("tool_calls"):
                    msg["tool_calls"] = [
                        {
                            "id": c["id"],
                            "type": "function",
                            "function": {
                                "name": c["name"],
                                "arguments": json.dumps(c.get("args") or {}),
                            },
                        }
                        for c in m["tool_calls"]
                    ]
                wire.append(msg)
            elif role == "tool":
                for r in m.get("tool_results", []):
                    wire.append({"role": "tool", "tool_call_id": r["id"], "content": r["output"]})
        return wire

    def step(self, messages: list[dict[str, Any]], tools: list[ToolSpec]) -> StepResult:
        client = self._ensure_client()
        kwargs: dict[str, Any] = {"model": self._model, "messages": self._to_wire(messages)}
        if tools:
            kwargs["tools"] = tool_registry.to_openai_tools(tools)
        resp = client.chat.completions.create(**kwargs)
        _record_usage(getattr(resp, "usage", None))
        message = resp.choices[0].message
        calls = [
            {
                "id": tc.id,
                "name": tc.function.name,
                "args": json.loads(tc.function.arguments or "{}"),
            }
            for tc in (getattr(message, "tool_calls", None) or [])
        ]
        return StepResult(text=message.content or "", tool_calls=calls)

    def step_stream(
        self,
        messages: list[dict[str, Any]],
        tools: list[ToolSpec],
        *,
        on_token: Callable[[str], None],
    ) -> StepResult:
        """Like `step`, but stream the answer: call `on_token` for each text delta.

        Tool-call fragments arrive spread across chunks (id/name in one, arguments across many),
        keyed by `index`, so we accumulate per index and parse the arguments once the stream ends.
        The wire format and normalized StepResult are identical to `step` — only the delivery
        differs, so the agent loop treats streaming and non-streaming turns the same.
        """
        client = self._ensure_client()
        kwargs: dict[str, Any] = {
            "model": self._model,
            "messages": self._to_wire(messages),
            "stream": True,
            # Ask the server to append a final usage-only chunk so streamed turns are counted too.
            "stream_options": {"include_usage": True},
        }
        if tools:
            kwargs["tools"] = tool_registry.to_openai_tools(tools)

        text_parts: list[str] = []
        frags: dict[int, dict[str, Any]] = {}
        for chunk in client.chat.completions.create(**kwargs):
            # The trailing usage-only chunk carries no choices; record it and move on.
            if getattr(chunk, "usage", None):
                _record_usage(chunk.usage)
            if not chunk.choices:
                continue
            delta = chunk.choices[0].delta
            if getattr(delta, "content", None):
                text_parts.append(delta.content)
                on_token(delta.content)
            for tc in getattr(delta, "tool_calls", None) or []:
                frag = frags.setdefault(tc.index, {"id": None, "name": "", "args": ""})
                if tc.id:
                    frag["id"] = tc.id
                if tc.function and tc.function.name:
                    frag["name"] = tc.function.name
                if tc.function and tc.function.arguments:
                    frag["args"] += tc.function.arguments

        calls = [
            {"id": f["id"], "name": f["name"], "args": json.loads(f["args"] or "{}")}
            for f in frags.values()
            if f["name"]
        ]
        return StepResult(text="".join(text_parts), tool_calls=calls)
