# search_anayasa

Search **Anayasa Mahkemesi (Constitutional Court)** decisions on the official
`anayasa.gov.tr` bilgi bankası. Covers both decision corpora; no key required.

| `application_type` | Corpus | Portal |
|----------------|--------|--------|
| `norm` (default) | Norm control (norm denetimi) | `normkararlarbilgibankasi.anayasa.gov.tr` |
| `bireysel` | Individual application (bireysel başvuru) | `kararlarbilgibankasi.anayasa.gov.tr` |

Both portals serve plain HTML search pages (no JSON API, no auth); this tool
issues the `GET /Ara` search and parses the rendered result list.

## Parameters
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `query` | string | — (required) | Search phrase, matched as a keyword. |
| `application_type` | string | `norm` | `norm` or `bireysel`. |
| `limit` | int | 10 | Maximum results for this page. |
| `page` | int | 1 | 1-indexed page. |

## CLI
```bash
python -m tools.search_anayasa --query "ifade özgürlüğü" --application_type norm --limit 5
echo '{"query": "adil yargılanma", "application_type": "bireysel", "limit": 5}' | python -m tools.search_anayasa
```

## Output (stdout JSON)
```json
{
  "total": 124,
  "page": 1,
  "count": 5,
  "results": [
    {
      "id": "2023-456",
      "title": "E. 2023/12, K. 2023/45",
      "type": "norm",
      "date": "10.05.2023",
      "url": "https://normkararlarbilgibankasi.anayasa.gov.tr/Dosyalar/2023-456"
    }
  ]
}
```

## Notes
- The two portals are HTML-scraped, not a documented JSON API. The CSS classes and
  query parameters used here (`KelimeAra[]`, `div.birkarar`, `div.bkararbaslik`,
  `div.kararbilgileri`, `div.KararBulteniBirKarar`, `div.bulunankararsayisi`) reflect
  the markup observed on the live site and **should be verified against the live API**
  if results look empty or malformed; the parser degrades gracefully (returns an empty
  list) rather than raising on layout changes.
- Rate limiting and 429 back-pressure come from the shared client in `tools/_base`.
- `total` falls back to the page result count when the site omits the "N Karar Bulundu"
  banner.
