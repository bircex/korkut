"""OpenAI GPT provider (cloud) with a tool-calling loop.

Exposes a uniform `generate(messages, tools)` so the router/agent stays
provider-independent. `tools` is a list of `core.tool_registry.ToolSpec`; the loop:
  call the model → if it requests tool calls, run them via `tool_registry.run` and feed
  results back → repeat until the model returns a final text answer (or max_iters).

Only redacted text reaches this provider (the router redacts before calling it).
The OpenAI client is injectable for testing; in production it reads OPENAI_API_KEY.
"""

from __future__ import annotations

import json
import os
from typing import Any

from core import tool_registry

DEFAULT_MODEL = os.getenv("OPENAI_MODEL", "gpt-4o")


class GptProvider:
    def __init__(self, *, model: str | None = None, client: Any = None, max_iters: int = 6) -> None:
        self._model = model or DEFAULT_MODEL
        self._client = client
        self._max_iters = max_iters

    def _ensure_client(self) -> Any:
        if self._client is None:
            from openai import OpenAI  # imported lazily; only needed for live calls

            self._client = OpenAI()  # uses OPENAI_API_KEY
        return self._client

    def generate(
        self, messages: list[dict[str, Any]], tools: list[Any] | None = None
    ) -> str:
        client = self._ensure_client()
        oai_tools = tool_registry.to_openai_tools(tools) if tools else None
        convo: list[dict[str, Any]] = list(messages)

        for _ in range(self._max_iters):
            kwargs: dict[str, Any] = {"model": self._model, "messages": convo}
            if oai_tools:
                kwargs["tools"] = oai_tools
            message = client.chat.completions.create(**kwargs).choices[0].message

            tool_calls = getattr(message, "tool_calls", None)
            if not tool_calls:
                return message.content or ""

            convo.append(
                {
                    "role": "assistant",
                    "content": message.content,
                    "tool_calls": [
                        {
                            "id": tc.id,
                            "type": "function",
                            "function": {"name": tc.function.name, "arguments": tc.function.arguments},
                        }
                        for tc in tool_calls
                    ],
                }
            )
            for tc in tool_calls:
                try:
                    args = json.loads(tc.function.arguments or "{}")
                    result = tool_registry.run(tc.function.name, args)
                except Exception as exc:  # noqa: BLE001 — surface tool errors to the model
                    result = {"error": type(exc).__name__, "detail": str(exc)}
                convo.append(
                    {
                        "role": "tool",
                        "tool_call_id": tc.id,
                        "content": json.dumps(result, ensure_ascii=False, default=str),
                    }
                )

        # Out of iterations: return whatever text we last had.
        return getattr(message, "content", "") or ""
