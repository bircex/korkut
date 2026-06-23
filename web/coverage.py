"""Coverage stats — how many judicial decisions Korkut can search, and from which sources.

Korkut does LIVE search (no stored corpus), so "how many decisions" is *measured* against the
official databases, not a fixed/fabricated number. We probe the Bedesten API with a near-universal
term ("karar", present in virtually every decision) per court type and sum the real `total`s the
API returns. Cached 24h so a login doesn't hammer the rate-limited upstream.
"""

from __future__ import annotations

import json
import time
from pathlib import Path

from tools._base.bedesten import BedestenClient

_CACHE = Path(__file__).resolve().parent.parent / "data" / "coverage.json"
_TTL = 24 * 3600
_PROBE = "karar"
_COURTS = [
    ("YARGITAYKARARI", "Yargıtay"),
    ("DANISTAYKARAR", "Danıştay"),
    ("YERELHUKUK", "Yerel mahkemeler"),
    ("ISTINAFHUKUK", "İstinaf (BAM)"),
    ("KYB", "Kanun yararına bozma"),
]

# Every official database Korkut can search live (for "neye erişiyorsun?").
DECISION_SOURCES = [
    "Yargıtay", "Danıştay", "UYAP Emsal", "Anayasa Mahkemesi",
    "KİK", "Rekabet Kurumu", "Sayıştay", "GİB özelgeleri",
]
LEGISLATION = "tüm yürürlükteki mevzuat (mevzuat.gov.tr): kanun, KHK, yönetmelik, CB kararnameleri"

_mem: dict | None = None


def _compute() -> dict:
    client = BedestenClient()
    by_court: list[dict] = []
    total = 0
    for code, label in _COURTS:
        try:
            n = int(client.search([code], _PROBE, limit=1).get("total") or 0)
        except Exception:
            n = 0
        if n:
            by_court.append({"label": label, "total": n})
            total += n
    return {"total_decisions": total, "by_court": by_court, "measured_at": int(time.time())}


def _fresh(data: dict | None) -> bool:
    return bool(data) and (time.time() - data.get("measured_at", 0) < _TTL)


def get(force: bool = False) -> dict:
    """Measured decision coverage, cached in memory and on disk for 24h."""
    global _mem
    if not force and _fresh(_mem):
        return _mem  # type: ignore[return-value]
    if not force and _CACHE.exists():
        try:
            disk = json.loads(_CACHE.read_text(encoding="utf-8"))
            if _fresh(disk):
                _mem = disk
                return disk
        except Exception:
            pass
    data = _compute()
    if data["total_decisions"]:  # only cache a real measurement
        try:
            _CACHE.parent.mkdir(parents=True, exist_ok=True)
            _CACHE.write_text(json.dumps(data), encoding="utf-8")
        except Exception:
            pass
    _mem = data
    return data


def summary() -> dict:
    cov = get()
    return {
        "total_decisions": cov["total_decisions"],
        "by_court": cov["by_court"],
        "decision_sources": DECISION_SOURCES,
        "legislation": LEGISLATION,
    }
