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
from dataclasses import dataclass
from typing import Any

import yaml

from korkut.core import agent_loop
from korkut.core import redactor as redaction
from korkut.core.agent_loop import Provider
from korkut.core.tool_registry import ToolSpec
from korkut.paths import CONFIG_DIR

logger = logging.getLogger("korkut.router")

_CONFIG = CONFIG_DIR / "model-routing.yaml"


@dataclass(frozen=True)
class ModelCfg:
    """An office's custom model (defined in the Özelleştir UI).

    `local` True means the endpoint runs on-prem (Ollama etc.) → the router skips redaction
    because the text never leaves the machine; False means an external endpoint, so cloud-bound
    text is still redacted before the call.
    """

    model: str
    base_url: str
    api_key: str | None = None
    local: bool = False


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
        # Tier used when a specialty is not listed (default "cloud" preserves prior behavior; a
        # single-model router sets this so every specialty resolves to its one tier).
        self._default_tier: str = self._cfg.get("default_tier", "cloud")
        self._fallback = bool(self._cfg.get("fallback_to_cloud_if_no_local", True))

    def _resolve_tier(self, specialty: str) -> str:
        """Return 'cloud' or 'local' for a specialty, applying the fallback rule."""
        tier = (self._specialties.get(specialty) or {}).get("model", self._default_tier)
        if tier == "local" and not self._local_available():
            if not self._fallback:
                raise RuntimeError(
                    f"local model unavailable for '{specialty}' and fallback disabled"
                )
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
        tools: list[ToolSpec] | None = None,
        extra_tools: list[Any] | None = None,
        handlers: dict[str, Any] | None = None,
        known_entities: list[tuple[str, str]] | None = None,
    ) -> str:
        tier = self._resolve_tier(specialty)
        provider = self._provider_for(tier)
        # `extra_tools` are session-bound tool specs (e.g. the Canvas signal); their schemas are
        # not message content, so they are never redacted.
        all_tools = list(tools or []) + list(extra_tools or [])

        if tier == "local":
            # Local model: data never leaves the machine → no redaction.
            return agent_loop.run(provider, messages, all_tools, handlers=handlers)

        # Cloud: redact every message's content with a shared mapping, then restore.
        # The shared agent loop carries the redacted conversation through every model turn;
        # tool outputs are public case-law/legislation, so no un-redacted PII reaches the model.
        contents = [str(m.get("content", "")) for m in messages]
        redacted, mapping = redaction.Redactor(known_entities=known_entities).redact_batch(contents)
        safe_messages = [{**m, "content": c} for m, c in zip(messages, redacted, strict=True)]
        reply = agent_loop.run(provider, safe_messages, all_tools, handlers=handlers)
        return redaction.restore(reply, mapping)


def single_model_router(cfg: ModelCfg) -> Router:
    """A router that sends every specialty to one custom model (an office's own model).

    Redaction follows `cfg.local`: a local (on-prem) endpoint runs un-redacted because text never
    leaves the machine; an external endpoint is treated as the cloud tier and redacted. No
    fallback — the office picked this model, so a failure surfaces rather than silently routing
    elsewhere.
    """
    from korkut.core.providers.openai_compat import OpenAICompatProvider

    provider = OpenAICompatProvider(model=cfg.model, base_url=cfg.base_url, api_key=cfg.api_key)
    tier = "local" if cfg.local else "cloud"
    config = {
        "backends": {tier: "custom"},
        "default_tier": tier,
        "fallback_to_cloud_if_no_local": False,
    }
    return Router({"custom": provider}, config=config, local_available=lambda: cfg.local)
