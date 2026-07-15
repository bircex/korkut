"""Per-request token accounting.

A chat turn fans out into several provider calls inside the agent loop (model → tools → model …),
so token usage has to be summed across all of them. Rather than thread a usage object through five
layers (app → agent → router → agent_loop → provider) and change every signature, the lowest layer
(the provider) adds each call's usage to a request-scoped ContextVar, and the top layer (the app
handler) resets it before the turn and snapshots it after, to persist on the assistant message.

ContextVar keeps this safe under concurrency: each request/thread sees its own accumulator.
"""

from __future__ import annotations

import contextvars

_usage: contextvars.ContextVar[dict[str, int] | None] = contextvars.ContextVar(
    "korkut_token_usage", default=None
)


def reset() -> None:
    """Begin a fresh accounting window (call once before a chat turn)."""
    _usage.set({"prompt": 0, "completion": 0})


def add(prompt_tokens: int, completion_tokens: int) -> None:
    """Add one provider call's usage to the current window (no-op if none is open)."""
    acc = _usage.get()
    if acc is None:
        return
    acc["prompt"] += int(prompt_tokens or 0)
    acc["completion"] += int(completion_tokens or 0)


def snapshot() -> dict[str, int]:
    """Total tokens accumulated in the current window (zeros if none is open)."""
    acc = _usage.get()
    if acc is None:
        return {"prompt": 0, "completion": 0}
    return dict(acc)
