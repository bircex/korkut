# fetch_bddk

Fetch the full text of a **BDDK** decision document as Markdown. BDDK serves documents under a
few different URL shapes (PDF or HTML); this tries each until one responds, converts by content
type, and paginates long documents. No API key.

Pass the `id` from a `search_bddk` result.

## Params
| name | type | required | default | notes |
|------|------|----------|---------|-------|
| `document_id` | string | yes | — | document id from a `search_bddk` result |
| `page` | integer | no | 1 | 1-indexed page of the chunked Markdown |
| `page_size` | integer | no | 5000 | characters per page |

## CLI
```
python -m tools.fetch_bddk --document_id 310
```

## Output
`{"documentId", "sourceUrl", "markdown", "page", "total_pages"}`.
