"""CLI harness shared by every tool's `__main__.py`.

Contract (see tools/README.md):
  • Input  : CLI flags (--key value) or a single JSON object on stdin.
  • Output : exactly one JSON object on stdout (machine-readable).
  • Logs   : stderr.   • Exit code: 0 on success, non-zero on error.

A tool's `__main__.py` is then a one-liner:

    from korkut.tools._base.cli import run
    from .tool import run_tool
    run(run_tool)
"""

from __future__ import annotations

import argparse
import json
import sys
from collections.abc import Callable
from typing import Any


def _parse_args(argv: list[str]) -> dict[str, Any]:
    """Read params from stdin JSON if piped, else from --key value flags."""
    if not sys.stdin.isatty():
        raw = sys.stdin.read().strip()
        if raw:
            return json.loads(raw)
    parser = argparse.ArgumentParser(add_help=True)
    parser.add_argument("--json", help="Parameters as a JSON object")
    known, rest = parser.parse_known_args(argv)
    if known.json:
        return json.loads(known.json)
    params: dict[str, Any] = {}
    key: str | None = None
    for token in rest:
        if token.startswith("--"):
            key = token[2:]
            params[key] = True  # bare flag → boolean true
        elif key is not None:
            params[key] = _coerce(token)
            key = None
    return params


def _coerce(value: str) -> Any:
    for cast in (int, float):
        try:
            return cast(value)
        except ValueError:
            continue
    return value


def run(tool_fn: Callable[..., Any]) -> None:
    """Execute `tool_fn(**params)`, print its JSON result, set the exit code."""
    try:
        params = _parse_args(sys.argv[1:])
    except json.JSONDecodeError as exc:
        print(json.dumps({"error": "invalid_input", "detail": str(exc)}), file=sys.stdout)
        sys.exit(2)
    try:
        result = tool_fn(**params)
    except TypeError as exc:  # bad/missing params
        print(json.dumps({"error": "bad_params", "detail": str(exc)}))
        sys.exit(2)
    except Exception as exc:
        print(json.dumps({"error": type(exc).__name__, "detail": str(exc)}))
        sys.exit(1)
    print(json.dumps(result, ensure_ascii=False, default=str))
