# search_kyb

Search **Kanun Yararına Bozma** (KYB — extraordinary appeal) decisions via the Ministry of
Justice **Bedesten** API (`bedesten.adalet.gov.tr`). KYB is the extraordinary remedy the Chief
Public Prosecutor's Office uses to challenge finalised decisions in the interest of law.

This is the Bedesten court type (`itemTypeList = ["KYB"]`) not already covered by
`search_yargitay`, `search_danistay`, or `search_emsal`. No API key.

## Params
| name | type | required | default | notes |
|------|------|----------|---------|-------|
| `query` | string | yes | — | `"exact phrase"`, `+required`, `-exclude`, `AND/OR/NOT`; no wildcards |
| `limit` | integer | no | 10 | results per page (1-10) |
| `page` | integer | no | 1 | 1-indexed |
| `date_start` | string | no | — | `YYYY-MM-DD` or full ISO 8601 |
| `date_end` | string | no | — | `YYYY-MM-DD` or full ISO 8601 |

## CLI
```
python -m tools.search_kyb --query "zincirleme suç" --limit 5
echo '{"query": "zincirleme suç", "limit": 5}' | python -m tools.search_kyb
```

## Output
Standard envelope `{"total", "page", "count", "results"}`; each result is
`{documentId, court, caseNo, decisionNo, decisionDate, decisionType}`. Pass `documentId`
to `fetch_decision` for full text.

## Endpoint
`POST https://bedesten.adalet.gov.tr/emsal-karar/searchDocuments` (shared `BedestenClient`).
