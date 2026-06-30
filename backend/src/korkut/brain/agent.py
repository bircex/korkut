"""Korkut agent entry point — the single brain used by both the CLI and the web app.

Wires the persona + the available tools + the model router into one `answer()` call. The
router picks the backend (cloud=Claude for the trial), redacts cloud-bound text, runs the
provider's tool-calling loop, and restores tokens in the reply.
"""

from __future__ import annotations

from korkut.brain.persona import build_persona
from korkut.core import tool_registry
from korkut.core.providers.claude import ClaudeProvider
from korkut.core.providers.gpt import GptProvider
from korkut.core.providers.groq import GroqProvider
from korkut.core.router import ModelCfg, Router, single_model_router

_router: Router | None = None


def get_router() -> Router:
    global _router
    if _router is None:
        _router = Router(
            {"claude": ClaudeProvider(), "gpt": GptProvider(), "groq": GroqProvider()},
            local_available=lambda: False,  # no local model yet → cloud
        )
    return _router


def answer(
    user_text: str,
    *,
    specialty: str = "research",
    office_name: str | None = None,
    history: list[dict] | None = None,
    extra_tools: list | None = None,
    handlers: dict | None = None,
    model_cfg: ModelCfg | None = None,
) -> str:
    """Answer a user message as Korkut, using tools + the configured model.

    `office_name` sets Korkut's per-office identity in the system persona (the authenticated
    user's tenant). `extra_tools`/`handlers` let a caller add session-bound tools for this turn
    (e.g. the web app's document-Canvas signal) without touching the global tool registry.
    `model_cfg`, when set, routes this turn to the office's own custom model instead of the
    built-in default.
    """
    router = single_model_router(model_cfg) if model_cfg is not None else get_router()
    tools = tool_registry.available_tools()
    messages: list[dict] = [{"role": "system", "content": build_persona(office_name)}]
    if history:
        messages.extend(history)
    messages.append({"role": "user", "content": user_text})
    return router.respond(
        specialty, "chat", messages, tools=tools, extra_tools=extra_tools, handlers=handlers
    )
