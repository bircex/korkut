"""Reachability probe for the office's *effective* model.

Used by the warning banner and the "Bağlantıyı test et" button so a missing/unreachable model
surfaces as guidance ("Ayarlar → Model"), not a crashed chat turn. Two cases:
  - a custom model is configured  → ping its endpoint (`GET {base_url}/models`) with a short timeout
  - no custom model               → report whether the built-in (default cloud) backend has its key
"""

from __future__ import annotations

import logging
import os

import yaml

from korkut.core.router import ModelCfg
from korkut.paths import CONFIG_DIR

logger = logging.getLogger("korkut.health")

_PROBE_TIMEOUT = 5.0  # seconds — a down endpoint shouldn't hang the banner check
_BUILTIN_KEY_ENV = {"claude": "ANTHROPIC_API_KEY", "gpt": "OPENAI_API_KEY", "groq": "GROQ_API_KEY"}


def _builtin_available() -> bool:
    """True if the built-in (default cloud) backend has its API key set in the environment."""
    cfg: dict = {}
    path = CONFIG_DIR / "model-routing.yaml"
    if path.exists():
        cfg = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    backend = (cfg.get("backends") or {}).get("cloud", "groq")
    env = _BUILTIN_KEY_ENV.get(backend)
    return bool(env and os.getenv(env))


def _probe(cfg: ModelCfg) -> tuple[bool, str]:
    """Hit the configured endpoint's `/models` and report whether it answered."""
    try:
        from openai import OpenAI  # lazy: only needed for the live probe

        client = OpenAI(
            base_url=cfg.base_url,
            api_key=cfg.api_key or "not-needed",
            timeout=_PROBE_TIMEOUT,
            max_retries=0,
        )
        client.models.list()
        return True, "Model sunucusuna bağlanıldı."
    except Exception as exc:  # connection refused, timeout, auth, bad URL — all "not reachable"
        logger.warning("model probe failed for %s: %s", cfg.base_url, exc)
        return (
            False,
            f"Model sunucusuna ulaşılamadı ({cfg.base_url}). Sunucunun çalıştığını ve "
            "adresin doğru olduğunu kontrol edin.",
        )


def model_status(cfg: ModelCfg | None) -> dict:
    """`{connected, configured, detail}` for the office's effective model.

    `configured` is True only when the office has its own custom model bound; False means the
    built-in default is in play.
    """
    if cfg is None:
        if _builtin_available():
            return {
                "connected": True,
                "configured": False,
                "detail": "Yerleşik (varsayılan) model kullanılıyor.",
            }
        return {
            "connected": False,
            "configured": False,
            "detail": "Bir model bağlı değil. Ayarlar → Model'den kendi modelinizi bağlayın.",
        }
    ok, detail = _probe(cfg)
    return {"connected": ok, "configured": True, "detail": detail}
