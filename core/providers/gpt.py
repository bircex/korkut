"""OpenAI GPT provider — one model turn. The loop lives in core.agent_loop.

`step()` serializes the provider-agnostic conversation into OpenAI Chat Completions form,
makes a single API call, and returns a normalized StepResult (final text or tool calls).
Only redacted text reaches this provider (the router redacts first).
"""

from __future__ import annotations

import json
import os
from typing import Any

from core import tool_registry
from core.agent_loop import StepResult
from core.tool_registry import ToolSpec

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
        message = client.chat.completions.create(**kwargs).choices[0].message
        calls = [
            {
                "id": tc.id,
                "name": tc.function.name,
                "args": json.loads(tc.function.arguments or "{}"),
            }
            for tc in (getattr(message, "tool_calls", None) or [])
        ]
        return StepResult(text=message.content or "", tool_calls=calls)
