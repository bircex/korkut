# search_anayasa

Search **Anayasa Mahkemesi (Constitutional Court)** decisions on the official
"Kararlar Bilgi Bankası" portal. No key required.

The court serves a single React SPA at
`https://normkararlarbilgibankasi.anayasa.gov.tr/kbb/` backed by a JSON API.
Despite the host name, that one portal covers every decision corpus; the search
selects a corpus with a top-level `kararTipi` discriminator.

| `application_type` | Corpus | `kararTipi` |
|--------------------|--------|-------------|
| `norm` (default) | Norm control (norm denetimi) | `NormDenetimi` |
| `individual` | Individual application (bireysel başvuru) | `BireyselBasvuru` |

The tool issues `POST /api/core/public/search` with a JSON body and parses the
`data` array from the response.

## Parameters
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `query` | string | — (required) | Search phrase, matched as a keyword. |
| `application_type` | string | `norm` | `norm` or `individual`. |
| `limit` | int | 10 | Maximum results for this page. |
| `page` | int | 1 | 1-indexed page (mapped to the API's 0-indexed page). |

## CLI
```bash
python -m tools.search_anayasa --query "ifade özgürlüğü" --application_type norm --limit 5
echo '{"query": "adil yargılanma", "application_type": "individual", "limit": 5}' | python -m tools.search_anayasa
```

## Output (stdout JSON)
```json
{
  "total": 660,
  "page": 1,
  "count": 1,
  "results": [
    {
      "id": "95d2e3dc-1a3f-3834-b965-ea1c0936364f",
      "title": "E. 2024/187, K. 2026/42",
      "type": "NormDenetimi",
      "date": "2026-02-12",
      "url": "https://normkararlarbilgibankasi.anayasa.gov.tr/kbb/pages/search/NormDenetimi?id=...&type=NormDenetimi"
    }
  ]
}
```

## Notes
- The search body sent is
  `{"query", "page", "size", "sort": "yayinTarihi", "order": "desc", "kararTipi"}`.
  The response shape is `{"total", "page", "data": [...]}`.
- `title` is built from corpus-specific fields: `E. <esasNo>, K. <kararNo>` for
  norm control, `<basvuruAdi> (B. No: <basvuruNo>)` for individual applications.
- `url` points at the SPA detail page; the decision id is base64url-encoded with
  a `kbb:` seed exactly as the portal does, so the link opens the decision.
- Rate limiting and 429 back-pressure come from the shared client in `tools/_base`.
- `total` falls back to the page result count when the API omits the total.
