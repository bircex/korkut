# search_yargitay

Search **Yargıtay (Court of Cassation)** decisions via the Bedesten API
(`bedesten.adalet.gov.tr`). No key required.

## Parameters
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `query` | string | — (required) | Search phrase. `"exact phrase"`, `+required`, `-exclude`, `AND/OR/NOT`. No wildcards. |
| `limit` | int | 10 | Results per page (1–10). |
| `page` | int | 1 | 1-indexed page. |
| `chamber` | string | "" | Optional chamber, e.g. `1. Hukuk Dairesi`. |
| `date_start` | string | — | Optional `YYYY-MM-DD` (or full ISO 8601). |
| `date_end` | string | — | Optional `YYYY-MM-DD`. |

## CLI
```bash
python -m tools.search_yargitay --query "kira tespiti" --limit 5
echo '{"query": "kira tespiti", "limit": 5, "date_start": "2023-01-01"}' | python -m tools.search_yargitay
```

## Output (stdout JSON)
```json
{
  "total": 124,
  "page": 1,
  "count": 5,
  "results": [
    {
      "documentId": "abc123",
      "court": "1. Hukuk Dairesi",
      "caseNo": "2023/123",
      "decisionNo": "2023/456",
      "decisionDate": "2023-05-10",
      "decisionType": "KARAR"
    }
  ]
}
```
Pass a `documentId` to **fetch_decision** for the full text.

## Notes
- Rate limit: Bedesten ≈ 10 req / 30 s per IP (shared token bucket + 429 back-pressure in `tools/_base`).
- Citations should be verified by fetching the document before relying on them.
