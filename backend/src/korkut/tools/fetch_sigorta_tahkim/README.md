# fetch_sigorta_tahkim

Fetch a **Sigorta Tahkim Komisyonu** (Insurance Arbitration Commission) journal issue ("Hakem
Karar Dergisi") as Markdown. Pass the issue number (`id`) from a `search_sigorta_tahkim` result.
Journals are large, so the converted text is paginated. No API key.

To find a keyword *within* an issue instead of reading it whole, use
`search_within_sigorta_tahkim`.

## Params
| name | type | required | default | notes |
|------|------|----------|---------|-------|
| `issue_number` | string | yes | — | journal issue number from a `search_sigorta_tahkim` result |
| `page` | integer | no | 1 | 1-indexed page of the chunked Markdown |
| `page_size` | integer | no | 5000 | characters per page |

## CLI
```
python -m tools.fetch_sigorta_tahkim --issue_number 64 --page 1
```

## Output
`{"issueNumber", "sourceUrl", "markdown", "page", "total_pages"}`.
