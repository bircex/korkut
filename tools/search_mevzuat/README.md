# search_mevzuat

Search **Turkish legislation (mevzuat)** via the Ministry of Justice
(UYAP Mevzuat) Bedesten API (`bedesten.adalet.gov.tr/mevzuat/searchDocuments`).
No key required. Covers laws (kanun), decree-laws (KHK), statutes (tüzük),
regulations (yönetmelik), Presidential decrees/decisions/circulars, and
communiqués (tebliğ).

## Parameters
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `query` | string | — (required) | Title keyword(s) matched against legislation names. |
| `limit` | int | 10 | Results per page (1–50). |
| `page` | int | 1 | 1-indexed page. |
| `mevzuat_turu` | string | "" | Optional type filter: `KANUN`, `KHK`, `TUZUK`, `YONETMELIK`, `CB_KARARNAME`, `CB_YONETMELIK`, `CB_KARAR`, `CB_GENELGE`, `KKY`, `UY`, `TEBLIGLER`, `MULGA`. Comma-separated for several types. |

## CLI
```bash
python -m tools.search_mevzuat --query "türk borçlar kanunu" --limit 5
python -m tools.search_mevzuat --query "kira" --mevzuat_turu KANUN
echo '{"query": "vergi", "limit": 5, "mevzuat_turu": "KANUN,KHK"}' | python -m tools.search_mevzuat
```

## Output (stdout JSON)
```json
{
  "total": 3,
  "page": 1,
  "count": 1,
  "results": [
    {
      "id": "abc123",
      "title": "TÜRK BORÇLAR KANUNU",
      "type": "KANUN",
      "number": "6098",
      "series": "5",
      "date": "2011-02-04",
      "gazetteNo": "27836",
      "url": "https://mevzuat.adalet.gov.tr/mevzuat?mevzuatId=abc123"
    }
  ]
}
```

## Notes
- Rate limit: Bedesten ≈ 10 req / 30 s per IP (shared token bucket + 429 back-pressure in `tools/_base`).
- The `query` matches legislation **titles** (`mevzuatAdi`); it is not a full-text content search.
- Verify against the live API: type codes and the exact result-field names
  (`mevzuatTur` shape, `resmiGazeteTarihi` format) should be confirmed against a
  real response before relying on citations.
