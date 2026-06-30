"""Tool registry — discovers tools under `tools/`, gates them by `requires_key`,
and runs them by name.

Each tool folder has a `tool.yaml` (name, description, parameters, requires_key) and a
`client.py` exposing `run_tool(**params)`. The registry:
  • scans `tools/*/tool.yaml`,
  • hides any tool whose `requires_key` env var is missing (graceful degradation),
  • exposes specs (for building the model's function-calling schema),
  • runs a tool by importing its `run_tool`.
"""

from __future__ import annotations

import importlib
import os
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import yaml

TOOLS_DIR = Path(__file__).resolve().parent.parent / "tools"


@dataclass
class ToolSpec:
    name: str
    description: str
    parameters: dict[str, Any]
    requires_key: str | None
    module: str  # e.g. "korkut.tools.search_yargitay"
    # Names this (composite) tool replaces in the *offered* set. The superseded tools stay
    # discoverable and runnable — a composite dispatches to them — but are hidden from the model so
    # one compact schema stands in for many near-identical ones (keeps the prompt small).
    supersedes: tuple[str, ...] = ()

    @property
    def available(self) -> bool:
        """A tool is available unless it needs a key that is not set in the env."""
        return not self.requires_key or bool(os.getenv(self.requires_key))


def discover() -> list[ToolSpec]:
    specs: list[ToolSpec] = []
    for tool_yaml in sorted(TOOLS_DIR.glob("*/tool.yaml")):
        folder = tool_yaml.parent.name
        if folder.startswith("_"):
            continue
        meta = yaml.safe_load(tool_yaml.read_text(encoding="utf-8")) or {}
        specs.append(
            ToolSpec(
                name=meta.get("name", folder),
                description=(meta.get("description") or "").strip(),
                parameters=meta.get("parameters") or {},
                requires_key=meta.get("requires_key") or None,
                module=f"korkut.tools.{folder}",
                supersedes=tuple(meta.get("supersedes") or ()),
            )
        )
    return specs


def available_tools() -> list[ToolSpec]:
    """Tools offerable to the model: key present, minus any superseded by a composite tool."""
    specs = [s for s in discover() if s.available]
    superseded = {name for s in specs for name in s.supersedes}
    return [s for s in specs if s.name not in superseded]


_JSON_TYPES = {
    "string": "string",
    "integer": "integer",
    "number": "number",
    "boolean": "boolean",
}


def _json_schema(spec: ToolSpec) -> dict[str, Any]:
    """Build a JSON Schema (object) for a tool's parameters."""
    properties: dict[str, Any] = {}
    required: list[str] = []
    for pname, p in (spec.parameters or {}).items():
        p = p or {}
        prop: dict[str, Any] = {"type": _JSON_TYPES.get(p.get("type", "string"), "string")}
        if p.get("description"):
            prop["description"] = p["description"]
        if p.get("options"):
            prop["enum"] = p["options"]
        properties[pname] = prop
        if p.get("required"):
            required.append(pname)
    return {"type": "object", "properties": properties, "required": required}


def to_openai_tools(specs: list[ToolSpec]) -> list[dict[str, Any]]:
    """Convert ToolSpecs into OpenAI function-calling tool definitions."""
    return [
        {
            "type": "function",
            "function": {
                "name": s.name,
                "description": s.description,
                "parameters": _json_schema(s),
            },
        }
        for s in specs
    ]


def to_anthropic_tools(specs: list[ToolSpec]) -> list[dict[str, Any]]:
    """Convert ToolSpecs into Anthropic (Claude) tool definitions."""
    return [
        {"name": s.name, "description": s.description, "input_schema": _json_schema(s)}
        for s in specs
    ]


def run(name: str, params: dict[str, Any]) -> Any:
    """Run a tool by its declared name."""
    for spec in discover():
        if spec.name == name:
            if not spec.available:
                raise RuntimeError(f"tool '{name}' disabled: missing key {spec.requires_key}")
            module = importlib.import_module(f"{spec.module}.client")
            return module.run_tool(**params)
    raise KeyError(f"unknown tool: {name}")
