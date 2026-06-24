# fetch_uyusmazlik

Fetch the full text of an **Uyuşmazlık Mahkemesi** (Court of Jurisdictional Disputes) decision
as Markdown. Uyuşmazlık decisions are standalone HTML pages (not Bedesten documents), so they
are fetched by URL rather than by `documentId`.

Pass the `url` from a `search_uyusmazlik` result. No API key.

## Params
| name | type | required | notes |
|------|------|----------|-------|
| `document_url` | string | yes | full `kararlar.uyusmazlik.gov.tr` decision URL from a `search_uyusmazlik` result |

## CLI
```
python -m tools.fetch_uyusmazlik --document_url "https://kararlar.uyusmazlik.gov.tr/Karar/Content/..."
echo '{"document_url": "https://kararlar.uyusmazlik.gov.tr/Karar/Content/..."}' | python -m tools.fetch_uyusmazlik
```

## Output
`{"sourceUrl", "markdown"}`.
