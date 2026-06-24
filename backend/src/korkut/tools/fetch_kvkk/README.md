# fetch_kvkk

Fetch the full text of a **KVKK** board decision as Markdown. KVKK decisions are standalone HTML
pages on `kvkk.gov.tr`; the body lives in a `<div class="blog-post-inner">`, which is extracted
and converted to Markdown. Long decisions are paginated. No API key.

Pass the `url` from a `search_kvkk` result.

## Params
| name | type | required | default | notes |
|------|------|----------|---------|-------|
| `decision_url` | string | yes | — | full `kvkk.gov.tr` decision URL from a `search_kvkk` result |
| `page` | integer | no | 1 | 1-indexed page of the chunked Markdown |
| `page_size` | integer | no | 5000 | characters per page |

## CLI
```
python -m tools.fetch_kvkk --decision_url "https://www.kvkk.gov.tr/Icerik/7288/2021-1303"
```

## Output
`{"sourceUrl", "title", "date", "decisionNumber", "markdown", "page", "total_pages"}`.
