"""Tests for the shared model-call resilience layer (`brain.resilience`).

The brain (`agent.answer`) is patched so nothing hits the network; we only assert on the
retry/classification policy and the user-facing message mapping that every channel shares.
"""

from __future__ import annotations

from unittest.mock import patch

import pytest

from korkut.brain import resilience


class _Timeout(Exception):
    """Transient by name — the classifier keys off the exception type name."""


class _RateLimitError(Exception):
    pass


def test_every_failure_gets_the_same_generic_message():
    # no matter the cause, the user only ever sees the one generic line (no internals leak)
    for exc in (_Timeout("read timed out"), _RateLimitError("429"), ValueError("weird 500 detail")):
        assert resilience.friendly_detail(exc) == resilience.GENERIC_DETAIL
    assert "weird" not in resilience.GENERIC_DETAIL and "yoğunluk" in resilience.GENERIC_DETAIL


def test_transient_error_is_retried_then_succeeds():
    calls = {"n": 0}

    def flaky(*_a, **_k):
        calls["n"] += 1
        if calls["n"] == 1:
            raise _Timeout("connection reset")
        return "sonunda oldu"

    with (
        patch("korkut.brain.agent.answer", side_effect=flaky),
        patch("korkut.brain.resilience.time.sleep"),
    ):
        assert resilience.guarded_answer("soru") == "sonunda oldu"
    assert calls["n"] == 2  # failed once, retried, succeeded


def test_context_overflow_is_not_retried():
    """A permanent fault must fail fast — retrying only makes the user wait for the same error."""
    calls = {"n": 0}

    def overflow(*_a, **_k):
        calls["n"] += 1
        raise ValueError("maximum context length exceeded")

    with (
        patch("korkut.brain.agent.answer", side_effect=overflow),
        patch("korkut.brain.resilience.time.sleep"),
        pytest.raises(resilience.ModelError) as ei,
    ):
        resilience.guarded_answer("uzun sohbet")
    assert calls["n"] == 1
    assert ei.value.detail == resilience.GENERIC_DETAIL


def test_persistent_transient_error_gives_up_with_friendly_detail():
    with (
        patch("korkut.brain.agent.answer", side_effect=_Timeout("down")),
        patch("korkut.brain.resilience.time.sleep"),
        pytest.raises(resilience.ModelError) as ei,
    ):
        resilience.guarded_answer("soru")
    assert ei.value.detail == resilience.GENERIC_DETAIL
    assert isinstance(ei.value.cause, _Timeout)


def test_no_retry_after_side_effects():
    calls = {"n": 0}

    def _fail(*a, **k):
        calls["n"] += 1
        exc = _Timeout("blip after a write")  # transient by name, but flagged non-retriable
        exc._korkut_no_retry = True
        raise exc

    with (
        patch("korkut.brain.agent.answer", side_effect=_fail),
        patch("korkut.brain.resilience.time.sleep"),
        pytest.raises(resilience.ModelError),
    ):
        resilience.guarded_answer("soru")
    assert calls["n"] == 1  # flagged non-retriable -> called once, no retry
