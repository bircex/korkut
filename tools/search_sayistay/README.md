# search_sayistay

Search **Sayıştay (Court of Accounts)** decisions via the official portal
(`www.sayistay.gov.tr`). No key required.

Sayıştay is Turkey's supreme audit institution. It publishes three kinds of
decisions, each behind its own DataTables search endpoint:

| `decision_type`   | Body            | Endpoint                              |
|----------------|-----------------|---------------------------------------|
| `genel_kurul`  | Genel Kurul (General Assembly) | `/KararlarGenelKurul/DataTablesList` |
| `temyiz_kurulu`| Temyiz Kurulu (Appeals Board)  | `/KararlarTemyiz/DataTablesList`     |
| `daire`        | Daire (Chamber)                | `/KararlarDaire/DataTablesList`      |

## Parameters
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `query` | string | — (required) | Free-text search phrase. |
| `decision_type` | string | `genel_kurul` | One of `genel_kurul`, `temyiz_kurulu`, `daire`. |
| `limit` | int | 10 | Results per page (1–10). |
| `page` | int | 1 | 1-indexed page. |

## CLI
```bash
python -m tools.search_sayistay --query "ihale" --decision_type temyiz_kurulu --limit 5
echo '{"query": "harcırah", "decision_type": "daire"}' | python -m tools.search_sayistay
```

## Output (stdout JSON)
```json
{
  "total": 42,
  "page": 1,
  "count": 5,
  "results": [
    {
      "id": 12345,
      "title": "5415/1",
      "decisionType": "Genel Kurul",
      "date": "10.05.2023",
      "url": "https://www.sayistay.gov.tr/KararlarGenelKurul/Detay/12345/"
    }
  ]
}
```

## Notes
- The portal uses ASP.NET WebForms: the client first fetches the search page to
  obtain a session cookie and the `__RequestVerificationToken` (CSRF) value, then
  submits a form-urlencoded DataTables POST.
- Each decision type returns a different row schema, so per-type field mappings
  are applied to produce the shared output contract. **The exact form field names
  and the full-text search field per type are best-effort and should be verified
  against the live API** before relying on them in production.
- An upstream WAF may answer the search POST with HTTP 418 / a Turkish block page;
  this is surfaced as a clear error.
- Citations should be verified by opening the decision's detail `url`.
