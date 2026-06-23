"""Anthropic Claude provider (cloud) with a tool-calling loop.

Same uniform `generate(messages, tools)` interface as the GPT provider, so the router
stays provider-independent. Anthropic specifics handled here:
  • `system` is a separate top-level parameter, so any {"role": "system"} entries in
    `messages` are pulled out and concatenated into `system`.
  • tools use `input_schema`; tool results go back as a user message with
    {"type": "tool_result", "tool_use_id": ..., "content": ...} blocks.
  • the assistant's raw response `content` blocks are appended verbatim (preserving any
    thinking/tool_use blocks) before sending tool results — the canonical multi-turn shape.

Only redacted text reaches this provider (the router redacts before calling it).
The Anthropic client is injectable for testing; in production it reads ANTHROPIC_API_KEY.
"""

from __future__ import annotations

import json
import os
from typing import Any

from core import tool_registry

# Per the claude-api guidance: default to Opus 4.8 unless explicitly overridden.
DEFAULT_MODEL = os.getenv("ANTHROPIC_MODEL", "claude-opus-4-8")
MAX_TOKENS = int(os.getenv("ANTHROPIC_MAX_TOKENS", "16000"))


class ClaudeProvider:
    def __init__(self, *, model: str | None = None, client: Any = None, max_iters: int = 6) -> None:
        self._model = model or DEFAULT_MODEL
        self._client = client
        self._max_iters = max_iters

    def _ensure_client(self) -> Any:
        if self._client is None:
            import anthropic  # imported lazily; only needed for live calls

            self._client = anthropic.Anthropic()  # uses ANTHROPIC_API_KEY
        return self._client

    def generate(self, messages: list[dict[str, Any]], tools: list[Any] | None = None) -> str:
        client = self._ensure_client()
        anthropic_tools = tool_registry.to_anthropic_tools(tools) if tools else None

        system = "\n\n".join(
            str(m.get("content", "")) for m in messages if m.get("role") == "system"
        )
        convo: list[dict[str, Any]] = [
            {"role": m["role"], "content": m["content"]}
            for m in messages
            if m.get("role") in ("user", "assistant")
        ]

        message = None
        for _ in range(self._max_iters):
            kwargs: dict[str, Any] = {
                "model": self._model,
                "max_tokens": MAX_TOKENS,
                "messages": convo,
            }
            if system:
                kwargs["system"] = system
            if anthropic_tools:
                kwargs["tools"] = anthropic_tools
            message = client.messages.create(**kwargs)

            if message.stop_reason == "tool_use":
                # Echo the assistant's content blocks verbatim, then return tool results.
                convo.append({"role": "assistant", "content": message.content})
                results = []
                for block in message.content:
                    if getattr(block, "type", None) != "tool_use":
                        continue
                    try:
                        output = tool_registry.run(block.name, dict(block.input))
                    except Exception as exc:  # noqa: BLE001 — surface tool errors to the model
                        output = {"error": type(exc).__name__, "detail": str(exc)}
                    results.append(
                        {
                            "type": "tool_result",
                            "tool_use_id": block.id,
                            "content": json.dumps(output, ensure_ascii=False, default=str),
                        }
                    )
                convo.append({"role": "user", "content": results})
                continue

            # end_turn / refusal / max_tokens / stop_sequence → return the text.
            return _text(message)

        return _text(message)


def _text(message: Any) -> str:
    if message is None:
        return ""
    if getattr(message, "stop_reason", None) == "refusal":
        return "Bu isteği güvenlik nedeniyle yanıtlayamıyorum."
    parts = [getattr(b, "text", "") for b in message.content if getattr(b, "type", None) == "text"]
    return "".join(parts)
