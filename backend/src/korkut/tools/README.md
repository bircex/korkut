# tools/ — Korkut's capabilities

Each external capability is an **independent, CLI-runnable, self-documenting** tool. Korkut
(its specialty modules) uses them as a toolset; each can also be tested on its own from the
shell. See ARCHITECTURE: ARCHITECTURE.md §12.

## Layout (`tools/<name>/`)
```
tools/search_yargitay/
  ├─ __init__.py
  ├─ __main__.py    # CLI entry (delegates to tools._base.cli.run)
  ├─ client.py      # thin client over tools/_base + run_tool(**params)
  ├─ tool.yaml      # name, description, params (JSON-schema-ish), requires_key
  └─ README.md      # what it does, params, example input/output, endpoint
```

## CLI contract
```
python -m tools.search_yargitay --sorgu "kira tespiti" --limit 5
# or pipe JSON:
echo '{"sorgu": "kira tespiti", "limit": 5}' | python -m tools.search_yargitay
```
- **Input:** `--key value` flags or a single JSON object on stdin.
- **Output:** exactly one JSON object on stdout (machine-readable, not prose).
- **Logs:** stderr.  **Exit code:** 0 success, non-zero on error (error is also JSON on stdout).

## Shared base (`tools/_base/`)
- `BaseClient` — synchronous httpx wrapper + token-bucket rate limiter + HTTP 429
  back-pressure (`Retry-After`) + in-process cache. Each source client only sets its
  endpoint and maps params/results.
- `html_to_markdown` / `pdf_to_markdown` — document conversion for the `fetch_*` tools.
- `chunk_markdown` — paginate a long document into fixed-size pages (PDF journals, long rulings).
- `extract_div_html` — pull the inner HTML of a `<div class="…">` (stdlib parser; no bs4).
- `bedesten.py` / `uyusmazlik.py` / `sigorta_tahkim.py` — per-source clients/helpers.
- `search_providers.py` — `TavilyClient` for sources with no real API (KVKK, BDDK, Sigorta).
- `tools._base.cli.run` — the CLI harness every `__main__.py` calls.

## Sources & keys
- **No key (core):** `search_yargitay`, `search_danistay`, `search_emsal`, `search_kyb`,
  `search_anayasa`, `search_uyusmazlik`, `fetch_uyusmazlik`, `search_kik`, `search_rekabet`,
  `search_sayistay`, `search_gib`, `search_mevzuat`, `fetch_decision`, `fetch_legislation`,
  `fetch_kvkk`, `fetch_bddk`, `fetch_sigorta_tahkim`, `search_within_sigorta_tahkim`.
- **Needs a search-provider key (optional, deferred):** `search_kvkk`, `search_bddk`,
  `search_sigorta_tahkim` — all use a single key, `TAVILY_API_KEY`. These authorities have no
  searchable API, so discovery goes through the Tavily web-search provider restricted to their
  domain; the matching `fetch_*` tool then downloads the document directly (no key). Declared with
  `requires_key` in `tool.yaml`; the registry hides a tool whose key is missing.

## Registry
`core/tool_registry.py` scans this folder, reads each `tool.yaml`, and exposes the tools to
the model as function-calling tools (`config/tools.yaml` maps specialty → tools). A tool with
a missing `requires_key` is disabled (never offered to the model).
