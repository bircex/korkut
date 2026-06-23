"""Korkut agent entry point — the single brain used by both the CLI and the web app.

Wires the persona + the available tools + the model router into one `answer()` call. The
router picks the backend (cloud=Claude for the trial), redacts cloud-bound text, runs the
provider's tool-calling loop, and restores tokens in the reply.
"""

from __future__ import annotations

from core import tool_registry
from core.providers.claude import ClaudeProvider
from core.providers.gpt import GptProvider
from core.router import Router
from korkut.persona import PERSONA

_router: Router | None = None


def get_router() -> Router:
    global _router
    if _router is None:
        _router = Router(
            {"claude": ClaudeProvider(), "gpt": GptProvider()},
            local_available=lambda: False,  # no local model yet → cloud
        )
    return _router


def answer(user_text: str, *, specialty: str = "research", history: list[dict] | None = None) -> str:
    """Answer a user message as Korkut, using tools + the configured cloud model."""
    tools = tool_registry.available_tools()
    messages: list[dict] = [{"role": "system", "content": PERSONA}]
    if history:
        messages.extend(history)
    messages.append({"role": "user", "content": user_text})
    return get_router().respond(specialty, "chat", messages, tools=tools)
