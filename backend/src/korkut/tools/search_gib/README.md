# search_gib

Search **GİB (Gelir İdaresi Başkanlığı / Revenue Administration)** tax rulings
(*özelge*) and the tax legislation they reference, via the official
gib.gov.tr JSON API. No key required.

GİB özelgeler are official ruling letters that answer taxpayer questions on VAT,
income tax, corporate tax, stamp duty and related matters.

## Endpoint

```
POST https://gib.gov.tr/api/gibportal/mevzuat/ozelge/list
```

Pagination and sort options (`page`, `size`, `sortFieldName`, `sortType`) are
sent as query-string parameters; the search filters are sent in the JSON body.
The body always includes the backend-mandated `status=2`, `deleted=false` and
`ktype=99` (where `ktype=99` selects özelge). The free-text term is sent on the
`title`, `kanunNo` and `description` fields, which the backend ORs together.

## Parameters
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `query` | string | — (required) | Free-text phrase, matched across title, law number and body text. |
| `limit` | int | 10 | Results per page (1–50). |
| `page` | int | 1 | 1-indexed page (sent to the API 0-indexed). |

## CLI
```bash
python -m tools.search_gib --query "katma değer vergisi istisnası" --limit 5
echo '{"query": "stopaj", "limit": 5, "page": 1}' | python -m tools.search_gib
```

## Output (stdout JSON)
```json
{
  "total": 124,
  "page": 1,
  "count": 5,
  "results": [
    {
      "id": "12345",
      "title": "KDV istisnası uygulaması hakkında",
      "type": "ozelge",
      "date": "2023-05-10T00:00:00.000Z",
      "url": "https://www.gib.gov.tr/node/..."
    }
  ]
}
```

## Notes
- This tool maps the live API best-effort. Field names and the endpoint path are
  documented from observation and **should be verified against the live API**
  before relying on them in production; the GİB backend may change shapes
  without notice.
- Rate limiting and HTTP 429 back-pressure are handled by the shared client in
  `tools/_base`.
- Citations should be verified by opening the source `url` before relying on them.
