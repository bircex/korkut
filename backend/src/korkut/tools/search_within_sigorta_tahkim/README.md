# search_within_sigorta_tahkim

Find a keyword **within a single** Sigorta Tahkim Komisyonu (Insurance Arbitration Commission)
journal issue. Downloads the issue PDF (by number, from a `search_sigorta_tahkim` result), splits
it into individual arbitration decisions, and returns the decisions that mention the keyword,
ranked by match count with a short excerpt around the first hit. No API key.

This complements `fetch_sigorta_tahkim` (which returns the whole issue): use this when you want
the specific decisions inside an issue that discuss a topic.

## Params
| name | type | required | default | notes |
|------|------|----------|---------|-------|
| `issue_number` | string | yes | — | journal issue number from a `search_sigorta_tahkim` result |
| `keyword` | string | yes | — | Turkish keyword/phrase (case-insensitive, İ/I-aware) |
| `max_results` | integer | no | 10 | max matching decisions |

## CLI
```
python -m tools.search_within_sigorta_tahkim --issue_number 64 --keyword "değer kaybı"
```

## Output
`{"issueNumber", "keyword", "totalDecisions", "matchingDecisions", "results"}`; each result is
`{header, relevanceScore, excerpt, bodyLength}`.
