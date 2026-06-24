# search_bddk

Search **BDDK** (Bankacılık Düzenleme ve Denetleme Kurumu / Banking Regulation and Supervision
Authority) decisions. BDDK publishes documents on `bddk.org.tr` with no searchable API, so
discovery uses the **Tavily Search API** restricted to the BDDK document domain (the same
approach as upstream yargi-mcp). Use `fetch_bddk` with a result's `id` for the full text.

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
TAVILY_API_KEY=… python -m tools.search_bddk --query "sermaye yeterliliği" --limit 5
```

## Output
`{"total", "page", "count", "results"}`; each result is `{id, title, content, url}`. Pass `id`
to `fetch_bddk`.

## Endpoint
`POST https://api.tavily.com/search` with `include_domains` restricted to BDDK and
`query = <keywords> "Karar Sayısı"`.
