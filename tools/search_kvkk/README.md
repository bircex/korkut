# search_kvkk

Search **KVKK** (Kişisel Verileri Koruma Kurumu / Personal Data Protection Authority) board
decision summaries. KVKK publishes "karar özeti" pages on `kvkk.gov.tr` with no searchable API,
so discovery uses the **Tavily Search API** restricted to the KVKK domain — the same single
provider used by `search_bddk` and `search_sigorta_tahkim`, so the whole toolset needs only one
key. Use `fetch_kvkk` with a result's `url` for the full text.

## Key
Requires `TAVILY_API_KEY` in the environment. Get a key at <https://tavily.com>. Without it, the
registry hides this tool.

## Params
| name | type | required | default | notes |
|------|------|----------|---------|-------|
| `query` | string | yes | — | Turkish keywords |
| `limit` | integer | no | 10 | max results (1-20) |
| `page` | integer | no | 1 | envelope consistency (Tavily has no real pagination) |

## CLI
```
TAVILY_API_KEY=… python -m tools.search_kvkk --query "açık rıza" --limit 5
```

## Output
`{"total", "page", "count", "results"}`; each result is
`{id, title, description, date, decisionNumber, url}`. Pass `url` to `fetch_kvkk`.

## Endpoint
`POST https://api.tavily.com/search` with `include_domains=["kvkk.gov.tr"]` and
`query = <keywords> "karar özeti"`.
