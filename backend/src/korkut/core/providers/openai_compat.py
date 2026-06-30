"""Generic OpenAI-compatible provider — any Chat Completions endpoint.

A per-office *custom* model: the office owner supplies the model name, base URL, and an optional
API key from the Özelleştir UI. This reuses `GptProvider`'s message serialization and
tool-calling normalization (every OpenAI-compatible server speaks the same wire format) and only
swaps the client (caller-supplied base URL + key) and the model.

Covers on-prem Ollama (`http://localhost:11434/v1`), vLLM/LM Studio, Groq, OpenAI itself, and any
other OpenAI-compatible gateway. Whether redaction runs before the call is decided by the router
(local tier → no redaction; cloud tier → redacted), not here.
"""

from __future__ import annotations

from typing import Any

from korkut.core.providers.gpt import GptProvider


class OpenAICompatProvider(GptProvider):
    def __init__(
        self,
        *,
        model: str,
        base_url: str,
        api_key: str | None = None,
        client: Any = None,
    ) -> None:
        super().__init__(model=model, client=client)
        self._base_url = base_url
        # Local servers (Ollama, vLLM) ignore the key but the OpenAI client requires a non-empty
        # string, so fall back to a placeholder.
        self._api_key = api_key or "not-needed"

    def _ensure_client(self) -> Any:
        if self._client is None:
            from openai import OpenAI  # imported lazily; only needed for live calls

            self._client = OpenAI(base_url=self._base_url, api_key=self._api_key)
        return self._client
