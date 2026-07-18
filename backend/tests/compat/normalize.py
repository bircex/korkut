"""KDM-003 — response normalization for cross-runtime differential comparison.

Two runtimes (Python now, .NET at KDM-034) must produce *equivalent* observable
output for the same request. "Equivalent" excludes volatile details that are not
part of the contract: generated ids, timestamps, opaque tokens/cookies, and the
ordering of set-like collections. This module reduces a raw HTTP result to a
canonical, comparable form.

The normalization is deliberately conservative and explicit: every volatile
field is masked to a **type token** (``"<int>"``, ``"<iso8601>"`` …) rather than
dropped, so a shape change (e.g. a field turning from int to string) still shows
up as a diff while a value change (a different id) does not.
"""

from __future__ import annotations

import re
from typing import Any

# Header names whose values are inherently volatile / environment-specific and
# therefore excluded from the contract comparison (kept as presence-only).
VOLATILE_HEADERS = {
    "date",
    "server",
    "content-length",
    "set-cookie",
    "x-request-id",
    "x-process-time",
}

# Field-name patterns that carry volatile values across runtimes.
_VOLATILE_KEY_RE = re.compile(
    r"^(id|uid|uuid|.*_id|token|.*_token|created_at|updated_at|.*_at|iat|exp|"
    r"session|cookie|nonce|state|csrf|etag)$",
    re.IGNORECASE,
)

_ISO8601_RE = re.compile(r"^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}")
_UUID_RE = re.compile(r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-", re.IGNORECASE)
_JWT_RE = re.compile(r"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$")


def _mask_scalar(value: Any) -> Any:
    """Map a scalar to a token if it looks volatile, else keep it."""
    if isinstance(value, str):
        if _ISO8601_RE.match(value):
            return "<iso8601>"
        if _UUID_RE.match(value):
            return "<uuid>"
        if _JWT_RE.match(value):
            return "<jwt>"
    return value


def normalize_body(value: Any, *, _key: str | None = None) -> Any:
    """Recursively canonicalize a JSON body for comparison.

    - dict: keys sorted; a value under a volatile key name is masked to a type
      token; other values recursed.
    - list: element order preserved (API order is contractual) but each element
      normalized. Callers that return set-like data should sort upstream.
    - scalars: masked if they look like a volatile value (iso date, uuid, jwt).
    """
    if isinstance(value, dict):
        out = {}
        for k in sorted(value):
            v = value[k]
            if _VOLATILE_KEY_RE.match(k):
                out[k] = _type_token(v)
            else:
                out[k] = normalize_body(v, _key=k)
        return out
    if isinstance(value, list):
        return [normalize_body(v) for v in value]
    return _mask_scalar(value)


def _type_token(value: Any) -> str:
    if isinstance(value, bool):
        return "<bool>"
    if value is None:
        return "<null>"
    if isinstance(value, int):
        return "<int>"
    if isinstance(value, float):
        return "<float>"
    if isinstance(value, str):
        return "<str>"
    if isinstance(value, list):
        return "<list>"
    if isinstance(value, dict):
        return "<dict>"
    return "<other>"


def normalize_headers(headers: dict[str, str]) -> dict[str, Any]:
    """Lower-case header names; volatile headers reduced to presence booleans."""
    out: dict[str, Any] = {}
    for name, val in headers.items():
        low = name.lower()
        if low in VOLATILE_HEADERS:
            out[low] = "<present>"
        else:
            out[low] = val
    return out


def normalize_result(
    *, status: int, headers: dict[str, str], body: Any, capture_headers: list[str] | None = None
) -> dict[str, Any]:
    """Produce the canonical comparable record for one request.

    Only headers explicitly named in ``capture_headers`` are compared (plus their
    volatile-masked form), so incidental server headers never cause false diffs.
    """
    hn = normalize_headers(headers)
    kept = {h: hn.get(h.lower(), "<absent>") for h in (capture_headers or [])}
    return {
        "status": status,
        "headers": kept,
        "body": normalize_body(body),
    }
