"""Shared model-call resilience — the one place a provider failure becomes a friendly,
actionable Turkish message, with bounded retry on transient errors.

Every channel answers through the same brain (`agent.answer`), so every channel should fail the
same way. Before this module the web app mapped failures to a helpful 503 while the chat channels
(Telegram/WhatsApp/e-mail) let the exception escape into the runner and the user got *silence*.
`guarded_answer` fixes that: both `api.app` and the integration `gateway` call it, so a user on
any channel gets the SAME message instead of a stack trace (web) or nothing (chat).

Classification is by exception *name* + *text*, not by importing each provider SDK's exception
types — the providers already vary (OpenAI-compatible for Groq/local, Anthropic for Claude) and a
string match keeps this provider-agnostic (the same convention the router uses).

Retry re-runs the whole `answer()` (tool loop included), so it fires only on *transient* faults
(connection/timeout/rate-limit) AND only when no tool has run yet this turn: once a tool executes
(some are WRITE tools — create_matter, add_invoice, …) the agent loop flags the failure
non-retriable, because a re-run would duplicate those side effects.
"""

from __future__ import annotations

import logging
import time

from korkut.brain import agent

logger = logging.getLogger("korkut.resilience")

# Shown when a model call fails (endpoint down, wrong URL, no key) instead of a raw 500, so the
# user is pointed at the fix rather than a stack trace.
MODEL_DOWN_DETAIL = (
    "Yapay zeka modeline ulaşılamadı. Modelin (örn. yerel sunucunuz) çalıştığından emin olun "
    "veya Ayarlar → Model'den bağlantıyı kontrol edin."
)
MODEL_CONTEXT_DETAIL = (
    "İstek, modelin context penceresinden uzun. Modeli daha büyük 'Context Length' "
    "(örn. 16384 veya üzeri) ile yeniden yükleyin ya da sohbeti kısaltıp tekrar deneyin."
)
MODEL_BUSY_DETAIL = (
    "Yapay zeka modeli şu an yoğun (hız sınırı). Birkaç saniye sonra tekrar deneyin."
)

# Bounded retry for transient faults. Kept short: a lawyer is waiting on the other end.
_MAX_RETRIES = 2
_BACKOFF_SECONDS = (0.5, 1.5)


class ModelError(Exception):
    """A model call that could not be completed. `detail` is a user-facing Turkish message."""

    def __init__(self, detail: str, *, cause: Exception | None = None) -> None:
        super().__init__(detail)
        self.detail = detail
        self.cause = cause


def _is_transient(exc: Exception) -> bool:
    """True if retrying might help (connection blip, timeout, rate-limit) — not a permanent fault.

    A context overflow or a malformed request will fail identically on every retry, so those are
    excluded: retrying them only makes the user wait longer for the same error.
    """
    name = type(exc).__name__.lower()
    text = str(exc).lower()
    if "context length" in text or "context window" in text or "tokens to keep" in text:
        return False
    if "connection" in name or "timeout" in name:
        return True
    return (
        "ratelimit" in name
        or "rate limit" in text
        or "429" in text
        or "overloaded" in text
        or "503" in text
        or "502" in text
    )


def friendly_detail(exc: Exception) -> str:
    """Map a provider failure to an actionable Turkish message (busy / down / context / other)."""
    name = type(exc).__name__.lower()
    text = str(exc).lower()
    if "ratelimit" in name or "rate limit" in text or "429" in text or "overloaded" in text:
        return MODEL_BUSY_DETAIL
    if "connection" in name or "timeout" in name:
        return MODEL_DOWN_DETAIL
    if "context length" in text or "context window" in text or "tokens to keep" in text:
        return MODEL_CONTEXT_DETAIL
    return "Yapay zeka modeli isteği şu an yanıtlayamadı, lütfen tekrar deneyin."


def guarded_answer(*args, **kwargs) -> str:
    """`agent.answer` with bounded retry on transient faults; raises `ModelError` on final failure.

    Callers translate `ModelError` to their transport: the web app raises a 503 with `.detail`, the
    chat gateway returns `.detail` as the reply text so the user still hears back on the channel.
    """
    last: Exception | None = None
    for attempt in range(_MAX_RETRIES + 1):
        try:
            return agent.answer(*args, **kwargs)
        except Exception as exc:  # provider/tool failure — classify and maybe retry
            last = exc
            if (
                attempt < _MAX_RETRIES
                and _is_transient(exc)
                and not getattr(exc, "_korkut_no_retry", False)
            ):
                delay = _BACKOFF_SECONDS[min(attempt, len(_BACKOFF_SECONDS) - 1)]
                logger.warning("model call transient failure (attempt %d): %s", attempt + 1, exc)
                time.sleep(delay)
                continue
            logger.exception("model call failed")
            raise ModelError(friendly_detail(exc), cause=exc) from exc
    # Unreachable (loop either returns or raises), but keeps type-checkers happy.
    raise ModelError(friendly_detail(last) if last else "Bilinmeyen hata")
