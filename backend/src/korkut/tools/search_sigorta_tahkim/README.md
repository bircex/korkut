# search_sigorta_tahkim

Search **Sigorta Tahkim Komisyonu** (Insurance Arbitration Commission) decisions. The commission
publishes quarterly PDF journals ("Hakem Karar Dergisi") on `sigortatahkim.org` with no
searchable API, so discovery uses the **Tavily Search API** restricted to the commission domain
(the same approach as upstream yargi-mcp).

Each result's `id` is a **journal issue number** — pass it to `fetch_sigorta_tahkim` to read the
issue, or to `search_within_sigorta_tahkim` to find a keyword inside that issue's decisions.

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
TAVILY_API_KEY=… python -m tools.search_sigorta_tahkim --query "değer kaybı" --limit 5
```

## Output
`{"total", "page", "count", "results"}`; each result is `{id, title, content, url}`.

## Endpoint
`POST https://api.tavily.com/search` with `include_domains=["sigortatahkim.org"]`.
