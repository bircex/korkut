# search_kik

Search **KİK (Kamu İhale Kurulu / Public Procurement Authority)** board decisions
via the official **kik.gov.tr v2 API** (`ekapv2.kik.gov.tr`). No key required.

The v2 API splits board decisions into three families, each at its own POST endpoint
under `/b_ihalearaclari/api/KurulKararlari/`. Pick one with `decision_type`:

| `decision_type` | Meaning | Endpoint |
|--------------|---------|----------|
| `uyusmazlik` (default) | Disputes | `https://ekapv2.kik.gov.tr/b_ihalearaclari/api/KurulKararlari/GetKurulKararlari` |
| `duzenleyici` | Regulatory | `https://ekapv2.kik.gov.tr/b_ihalearaclari/api/KurulKararlari/GetKurulKararlariDk` |
| `mahkeme` | Court | `https://ekapv2.kik.gov.tr/b_ihalearaclari/api/KurulKararlari/GetKurulKararlariMk` |

## Parameters
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `query` | string | — (required) | Free-text search over decision content (`KararMetni`). May be empty. |
| `decision_type` | string | `uyusmazlik` | Decision family: `uyusmazlik` / `duzenleyici` / `mahkeme`. |
| `limit` | int | 10 | Results per page (>= 1). |
| `page` | int | 1 | 1-indexed page. |
| `karar_no` | string | "" | Optional decision number, e.g. `2025/UH.II-1801`. |
| `basvuran` | string | "" | Optional applicant name. |
| `idare_adi` | string | "" | Optional administration / procuring-entity name. |
| `date_start` | string | "" | Optional `YYYY-MM-DD`. |
| `date_end` | string | "" | Optional `YYYY-MM-DD`. |

## CLI
```bash
python -m tools.search_kik --query "aşırı düşük teklif" --limit 5
python -m tools.search_kik --query "" --decision_type duzenleyici --limit 10
echo '{"query": "ihale iptali", "decision_type": "mahkeme", "limit": 5}' | python -m tools.search_kik
```

## Output (stdout JSON)
```json
{
  "total": 500,
  "page": 1,
  "count": 5,
  "results": [
    {
      "id": "177280",
      "title": "...başvuru konusu...",
      "decisionType": "uyusmazlik",
      "date": "2025-01-15T00:00:00",
      "url": "https://ekap.kik.gov.tr/EKAP/Vatandas/KurulKararGoster.aspx?KararId=177280"
    }
  ]
}
```

`id` is the `gundemMaddesiId` returned by the API.

## Notes & caveats
- **No API key.** The endpoints are public.
- **Legacy TLS.** The KİK host serves TLS through a legacy stack. This tool builds its own
  `httpx.Client` with `OP_LEGACY_SERVER_CONNECT` and a broad cipher list, because the shared
  `BaseClient` transport cannot express a custom SSL context. The rest of `BaseClient`
  (token bucket, HTTP 429 back-pressure, cache, retries) is reused unchanged.
- **Request signing (best-effort).** The live gateway expects per-request `X-Custom-Request-*`
  headers signed with AES. They are generated only when the optional `cryptography` package is
  installed; otherwise the request is sent without them. If the live API rejects unsigned
  requests, install `cryptography` and verify.
- **Pagination is client-side.** The v2 search endpoints return the full result set in one
  response, so `limit`/`page` slice the records locally; `total` is the full count.
- **Verify against the live API.** The endpoint shapes and signing headers were derived from
  the public web app and may change. Confirm field names, the signing scheme, and the document
  URL against a live response before relying on this tool in production. The document URL
  (`KurulKararGoster.aspx?KararId=<gundemMaddesiId>`) may require an encrypted `KararId` on the
  live site; treat it as a best-effort pointer.
```
