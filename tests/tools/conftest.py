"""Shared fixtures for tool unit tests.

Tool clients throttle through a token-bucket rate limiter (≈1 request / 3.5 s) so they
don't hammer the public portals in production. Unit tests mock the network with respx and
don't exercise rate limiting, so we disable the limiter here to keep multi-request tests fast.
"""

import pytest

from tools._base import client as base_client


@pytest.fixture(autouse=True)
def _no_rate_limit(monkeypatch):
    monkeypatch.setattr(base_client.TokenBucket, "acquire", lambda self, max_wait=30.0: None)
