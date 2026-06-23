"""Anthropic Claude provider — one model turn. The loop lives in core.agent_loop.

`step()` serializes the provider-agnostic conversation into Anthropic Messages form (system
pulled out as a top-level param; tool calls/results as tool_use/tool_result blocks), makes a
single API call, and returns a normalized StepResult. Only redacted text reaches here.
"""

from __future__ import annotations

import os
from typing import Any

from core import tool_registry
from core.agent_loop import StepResult
from core.tool_registry import ToolSpec

# Per the claude-api guidance: default to Opus 4.8 unless explicitly overridden.
DEFAULT_MODEL = os.getenv("ANTHROPIC_MODEL", "claude-opus-4-8")
MAX_TOKENS = int(os.getenv("ANTHROPIC_MAX_TOKENS", "16000"))


class ClaudeProvider:
    def __init__(self, *, model: str | None = None, client: Any = None) -> None:
        self._model = model or DEFAULT_MODEL
        self._client = client

    def _ensure_client(self) -> Any:
        if self._client is None:
            import anthropic  # imported lazily; only needed for live calls

            self._client = anthropic.Anthropic()  # uses ANTHROPIC_API_KEY
        return self._client

    @staticmethod
    def _split(messages: list[dict[str, Any]]) -> tuple[str, list[dict[str, Any]]]:
        system = "\n\n".join(m.get("content", "") for m in messages if m["role"] == "system")
        conv: list[dict[str, Any]] = []
        for m in messages:
            role = m["role"]
            if role == "user":
                conv.append({"role": "user", "content": m.get("content", "")})
            elif role == "assistant":
                blocks: list[dict[str, Any]] = []
                if m.get("content"):
                    blocks.append({"type": "text", "text": m["content"]})
                for c in m.get("tool_calls", []):
                    blocks.append(
                        {"type": "tool_use", "id": c["id"], "name": c["name"], "input": c.get("args") or {}}
                    )
                conv.append({"role": "assistant", "content": blocks})
            elif role == "tool":
                conv.append(
                    {
                        "role": "user",
                        "content": [
                            {"type": "tool_result", "tool_use_id": r["id"], "content": r["output"]}
                            for r in m.get("tool_results", [])
                        ],
                    }
                )
        return system, conv

    def step(self, messages: list[dict[str, Any]], tools: list[ToolSpec]) -> StepResult:
        client = self._ensure_client()
        system, conv = self._split(messages)
        kwargs: dict[str, Any] = {"model": self._model, "max_tokens": MAX_TOKENS, "messages": conv}
        if system:
            kwargs["system"] = system
        if tools:
            kwargs["tools"] = tool_registry.to_anthropic_tools(tools)
        message = client.messages.create(**kwargs)

        if message.stop_reason == "refusal":
            return StepResult(text="Bu isteği güvenlik nedeniyle yanıtlayamıyorum.")
        text = "".join(getattr(b, "text", "") for b in message.content if getattr(b, "type", None) == "text")
        calls = [
            {"id": b.id, "name": b.name, "args": dict(b.input)}
            for b in message.content
            if getattr(b, "type", None) == "tool_use"
        ]
        return StepResult(text=text, tool_calls=calls)
