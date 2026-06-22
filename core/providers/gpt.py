"""OpenAI GPT provider (cloud).

Phase 1 (not yet implemented). Wraps the `openai` SDK; exposes a uniform
`uret(mesajlar, tools)` returning either final text or tool calls, so the agent loop stays
provider-independent. Only redacted text reaches this provider.
"""

from __future__ import annotations

from typing import Any


def uret(mesajlar: list[dict[str, Any]], tools: list[dict[str, Any]] | None = None) -> Any:  # noqa: ARG001
    raise NotImplementedError("Phase 1: GPT provider not yet implemented")
