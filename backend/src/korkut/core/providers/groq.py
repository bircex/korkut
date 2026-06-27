"""Groq provider — OpenAI-compatible Chat Completions over Groq's fast inference.

Groq serves the OpenAI Chat Completions API, so this reuses `GptProvider`'s message
serialization and tool-calling normalization and only swaps the client (Groq base URL +
``GROQ_API_KEY``) and the default model. Only redacted text reaches this provider (the router
redacts first).

Pick a tool-calling-capable model via ``GROQ_MODEL`` (default: ``llama-3.3-70b-versatile``).
"""

from __future__ import annotations

import os
from typing import Any

from korkut.core.providers.gpt import GptProvider

GROQ_BASE_URL = "https://api.groq.com/openai/v1"
DEFAULT_MODEL = os.getenv("GROQ_MODEL", "llama-3.3-70b-versatile")


class GroqProvider(GptProvider):
    def __init__(self, *, model: str | None = None, client: Any = None) -> None:
        super().__init__(model=model or DEFAULT_MODEL, client=client)

    def _ensure_client(self) -> Any:
        if self._client is None:
            from openai import OpenAI  # imported lazily; only needed for live calls

            self._client = OpenAI(base_url=GROQ_BASE_URL, api_key=os.getenv("GROQ_API_KEY"))
        return self._client
