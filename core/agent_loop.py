"""Provider-agnostic agent loop — the single place tools are orchestrated.

Every model uses the SAME skillset and the SAME loop. A provider only knows how to take
one turn: given the normalized conversation + the tool specs, it returns either a final
answer or a set of tool calls. This module runs the loop and executes the tools centrally
(via `core.tool_registry`), so swapping GPT↔Claude↔local changes nothing about how tools
are defined, selected, or run.

Normalized conversation (what the loop maintains; providers serialize it to their wire form):
  {"role": "system",    "content": str}
  {"role": "user",      "content": str}
  {"role": "assistant", "content": str, "tool_calls": [ToolCall]}   # tool_calls optional
  {"role": "tool",      "tool_results": [ToolResult]}

ToolCall   = {"id": str, "name": str, "args": dict}
ToolResult = {"id": str, "name": str, "output": str}   # output is a JSON string
"""

from __future__ import annotations

import json
from collections.abc import Callable
from dataclasses import dataclass, field
from typing import Any, Protocol

from core import tool_registry
from core.tool_registry import ToolSpec


@dataclass
class StepResult:
    """One model turn: a final answer, or a request to call tools."""

    text: str = ""
    tool_calls: list[dict[str, Any]] = field(default_factory=list)


class Provider(Protocol):
    def step(self, messages: list[dict[str, Any]], tools: list[ToolSpec]) -> StepResult:
        """Take one model turn over the normalized conversation; do NOT run tools."""
        ...


def run(
    provider: Provider,
    messages: list[dict[str, Any]],
    tools: list[ToolSpec] | None = None,
    *,
    handlers: dict[str, Callable[[dict[str, Any]], Any]] | None = None,
    max_iters: int = 8,
) -> str:
    """Drive the model→tools→model loop until a final answer (or max_iters).

    `handlers` maps a tool name to an in-process callable (used for stateful, session-bound
    tools like the document Canvas signal); names not in `handlers` run via `tool_registry`.
    """
    tools = tools or []
    handlers = handlers or {}
    convo: list[dict[str, Any]] = list(messages)
    result = StepResult()
    for _ in range(max_iters):
        result = provider.step(convo, tools)
        if not result.tool_calls:
            return result.text
        convo.append({"role": "assistant", "content": result.text, "tool_calls": result.tool_calls})
        tool_results = []
        for call in result.tool_calls:
            try:
                args = call.get("args") or {}
                if call["name"] in handlers:
                    output = handlers[call["name"]](args)
                else:
                    output = tool_registry.run(call["name"], args)
            except Exception as exc:
                output = {"error": type(exc).__name__, "detail": str(exc)}
            tool_results.append(
                {
                    "id": call["id"],
                    "name": call["name"],
                    "output": json.dumps(output, ensure_ascii=False, default=str),
                }
            )
        convo.append({"role": "tool", "tool_results": tool_results})
    return result.text
