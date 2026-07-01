"""Context budgeting — trim conversation history so a turn fits the model's context window.

The overflow that shows up as "İstek, modelin context penceresinden uzun" is a token problem, but
the old rule kept the last *20 messages* regardless of size — a handful of long turns still blows a
small local window (LM Studio/Ollama default to a few thousand tokens), while the web chat's longer
threads overflow before Telegram's short ones do. Same model, different history length → different
outcome. Budgeting by *tokens* removes that difference.

We can't tokenize an arbitrary local model exactly, so we estimate conservatively (a Turkish token
is ~3-4 chars; we divide by 3 to *over*-count, which trims a little early rather than overflowing).
The system prompt, the tool schemas, the new user message and reply headroom are all charged
against the window; whatever budget is left keeps the most-recent history that fits.

Windows default by tier — a small local model gets a cautious 8192, a cloud model its large native
window — and both are overridable via env so an office can tune without a code change.
"""

from __future__ import annotations

import json
import os

from korkut.core import tool_registry
from korkut.core.router import ModelCfg
from korkut.core.tool_registry import ToolSpec

# Default context windows (tokens). Local endpoints are small and unknown, so assume a cautious
# floor; cloud models (Groq/Claude) are large. Both overridable per deployment.
LOCAL_WINDOW = int(os.getenv("KORKUT_LOCAL_CONTEXT_WINDOW", "8192"))
CLOUD_WINDOW = int(os.getenv("KORKUT_CONTEXT_WINDOW", "128000"))

# Room reserved for the model's own reply so budgeting never eats the answer.
REPLY_HEADROOM = int(os.getenv("KORKUT_REPLY_HEADROOM", "1024"))

# Per-message wire overhead (role, delimiters) the raw text estimate misses.
_PER_MSG_OVERHEAD = 4


def estimate_tokens(text: str) -> int:
    """Conservative token estimate: ~3 chars/token (over-counts on purpose, to stay under cap)."""
    if not text:
        return 0
    return len(text) // 3 + 1


def window_for(model_cfg: ModelCfg | None) -> int:
    """Pick the context window for this turn's model: local → cautious floor, cloud → large."""
    if model_cfg is not None and model_cfg.local:
        return LOCAL_WINDOW
    return CLOUD_WINDOW


def estimate_tools_tokens(tools: list[ToolSpec] | None) -> int:
    """Estimate the tokens the tool schemas add to the prompt (serialized function definitions)."""
    if not tools:
        return 0
    serialized = json.dumps(tool_registry.to_openai_tools(tools), ensure_ascii=False)
    return estimate_tokens(serialized)


def fit_history(
    history: list[dict],
    *,
    system_text: str,
    user_text: str,
    window: int,
    tools_tokens: int = 0,
    reply_headroom: int = REPLY_HEADROOM,
) -> list[dict]:
    """Return the newest slice of `history` that fits the window after the fixed parts are charged.

    Keeps whole messages, newest first, until the budget runs out — so recent context survives and
    the oldest turns are dropped. If nothing fits, returns [] (the turn still sends system + user).
    """
    if not history:
        return []
    fixed = (
        estimate_tokens(system_text)
        + estimate_tokens(user_text)
        + tools_tokens
        + reply_headroom
        + _PER_MSG_OVERHEAD * 2  # system + user messages
    )
    budget = window - fixed
    if budget <= 0:
        return []

    kept: list[dict] = []
    total = 0
    for msg in reversed(history):
        cost = estimate_tokens(str(msg.get("content", ""))) + _PER_MSG_OVERHEAD
        if total + cost > budget:
            break
        kept.append(msg)
        total += cost
    kept.reverse()
    return kept
