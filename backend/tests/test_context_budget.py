"""Tests for context budgeting (`brain.context_budget`) — the token-aware history trimmer."""

from __future__ import annotations

from korkut.brain import context_budget
from korkut.core.router import ModelCfg


def _history(n, content="mesaj"):
    return [
        {"role": "user" if i % 2 == 0 else "assistant", "content": f"{content}-{i}"}
        for i in range(n)
    ]


def test_window_for_local_vs_cloud():
    local = ModelCfg(model="qwen", base_url="http://localhost:1234/v1", local=True)
    cloud = ModelCfg(model="gpt", base_url="https://x/v1", local=False)
    assert context_budget.window_for(local) == context_budget.LOCAL_WINDOW
    assert context_budget.window_for(cloud) == context_budget.CLOUD_WINDOW
    assert context_budget.window_for(None) == context_budget.CLOUD_WINDOW  # built-in → cloud


def test_small_history_kept_whole():
    hist = _history(4)
    kept = context_budget.fit_history(hist, system_text="kısa", user_text="selam", window=8192)
    assert kept == hist  # all four fit comfortably


def test_trims_oldest_first_under_pressure():
    # A tiny window forces most of the history out; the newest survive, in order.
    hist = _history(10, content="x" * 300)  # ~100 tokens each
    kept = context_budget.fit_history(
        hist, system_text="", user_text="", window=400, reply_headroom=0
    )
    assert 0 < len(kept) < len(hist)
    assert kept == hist[len(hist) - len(kept) :]  # a newest-first contiguous slice
    assert kept[-1] == hist[-1]  # the most recent turn is always kept when anything fits


def test_returns_empty_when_fixed_parts_exceed_window():
    hist = _history(4)
    kept = context_budget.fit_history(hist, system_text="s" * 10000, user_text="u", window=1000)
    assert kept == []  # nothing left after the system prompt; turn still sends system + user


def test_estimate_tokens_monotonic_and_conservative():
    assert context_budget.estimate_tokens("") == 0
    assert context_budget.estimate_tokens("a" * 300) >= 100  # ~3 chars/token, over-counts
    assert context_budget.estimate_tokens("uzun" * 100) > context_budget.estimate_tokens("uzun")
