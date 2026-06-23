"""Model router — the single point where a specialty is mapped to a backend, the
local→cloud fallback is applied, and redaction is enforced for cloud-bound traffic.

Flow (ARCHITECTURE.md §4, §13):
  respond(specialty, task_type, messages)
    → pick backend from config/model-routing.yaml (cloud|local)
    → if local requested but unavailable and fallback enabled → cloud (+log)
    → if cloud: redact message contents, call provider, restore tokens in the reply
    → if local: call provider directly (no redaction; text never leaves the machine)

Providers are injected (a dict name→Provider) so the router stays provider-independent and
unit-testable; `local_available` lets the caller report whether the local model is up.
"""

from __future__ import annotations

import logging
from collections.abc import Callable
from pathlib import Path
from typing import Any, Protocol

import yaml

from core import redactor as redaction

logger = logging.getLogger("korkut.router")

_CONFIG = Path(__file__).resolve().parent.parent / "config" / "model-routing.yaml"


class Provider(Protocol):
    def generate(self, messages: list[dict[str, Any]], tools: list[dict[str, Any]] | None = None) -> str:
        ...


def _load_config() -> dict:
    if _CONFIG.exists():
        return yaml.safe_load(_CONFIG.read_text(encoding="utf-8")) or {}
    return {}


class Router:
    def __init__(
        self,
        providers: dict[str, Provider],
        *,
        config: dict | None = None,
        local_available: Callable[[], bool] = lambda: False,
    ) -> None:
        self._cfg = config if config is not None else _load_config()
        self._providers = providers
        self._local_available = local_available
        self._backends: dict[str, str] = self._cfg.get("backends") or {}
        self._specialties: dict[str, dict] = self._cfg.get("specialties") or {}
        self._fallback = bool(self._cfg.get("fallback_to_cloud_if_no_local", True))

    def _resolve_tier(self, specialty: str) -> str:
        """Return 'cloud' or 'local' for a specialty, applying the fallback rule."""
        tier = (self._specialties.get(specialty) or {}).get("model", "cloud")
        if tier == "local" and not self._local_available():
            if not self._fallback:
                raise RuntimeError(f"local model unavailable for '{specialty}' and fallback disabled")
            logger.warning("local model down for '%s'; falling back to cloud", specialty)
            tier = "cloud"
        return tier

    def _provider_for(self, tier: str) -> Provider:
        name = self._backends.get(tier)
        if not name or name not in self._providers:
            raise RuntimeError(f"no provider configured for tier '{tier}' (backend={name!r})")
        return self._providers[name]

    def respond(
        self,
        specialty: str,
        task_type: str,
        messages: list[dict[str, Any]],
        *,
        tools: list[dict[str, Any]] | None = None,
    ) -> str:
        tier = self._resolve_tier(specialty)
        provider = self._provider_for(tier)

        if tier == "local":
            # Local model: data never leaves the machine → no redaction.
            return provider.generate(messages, tools)

        # Cloud: redact every message's content with a shared mapping, then restore.
        contents = [str(m.get("content", "")) for m in messages]
        redacted, mapping = redaction.Redactor().redact_batch(contents)
        safe_messages = [{**m, "content": c} for m, c in zip(messages, redacted)]
        reply = provider.generate(safe_messages, tools)
        return redaction.restore(reply, mapping)
