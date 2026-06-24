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
- The portal is ASP.NET Core MVC with a server-side jQuery DataTables grid. The
  client first GETs the search page to obtain the `.AspNetCore.Antiforgery.*`
  cookie and the `__RequestVerificationToken`, then submits a form-urlencoded POST
  to `.../DataTablesList`.
- The POST body must include the **complete** server-side DataTables payload:
  `draw`, `start`, `length`, `search[value]`, one `columns[i][...]` group per
  declared column, and an `order[0][...]` group — plus the page's filter-model
  fields (e.g. `KararlarGenelKurulAra.KARARTAMAMI` carries the full-text query).
  Omitting the `columns[]`/`order[]` arrays makes the ASP.NET model binder fail
  with **HTTP 500**; that was the original cause of this tool's failure.
- Each decision type declares different columns and filter fields, so per-type
  request payloads and row mappings are applied to produce the shared output
  contract. The endpoints, column orders and field names below are verified live
  against `www.sayistay.gov.tr`.
- An upstream WAF may answer with a Turkish block page ("Bilgi Güvenliği
  Politikaları Gereği Kısıtlanmıştır"); this is surfaced as a clear
  `SayistayWafBlocked` error rather than returning empty results.
- Citations should be verified by opening the decision's detail `url`.
