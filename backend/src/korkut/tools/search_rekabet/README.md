# search_rekabet

Search **Rekabet Kurumu** (Turkish Competition Authority) decisions.

## Source

- **Endpoint:** `GET https://www.rekabet.gov.tr/tr/Kararlar`
- **Format:** **HTML** (server-rendered). The portal has no JSON API and needs no key.
- The search page is fetched and the decision list is parsed out of
  `<div id="kararList">` (one `<table class="equalDivide">` per decision). The total record
  count comes from the `Toplam : N` text in the pagination block. Each decision links to a
  landing page at `/Karar?kararId=<GUID>`.

## Parameters

| name         | type    | default | description                                                          |
|--------------|---------|---------|----------------------------------------------------------------------|
| `query`      | string  | —       | Free-text title search (portal field `sayfaAdi`).                    |
| `decision_type` | string  | `""`    | Decision-type filter (see below). Empty = all.                       |
| `limit`      | integer | `10`    | Results per page (1-10).                                             |
| `page`       | integer | `1`     | 1-indexed page number.                                              |

### `decision_type` values

| value                    | meaning                          |
|--------------------------|----------------------------------|
| `birlesme_devralma`      | Merger and acquisition           |
| `diger`                  | Other                            |
| `menfi_tespit_muafiyet`  | Negative clearance and exemption |
| `ozellestirme`           | Privatization                    |
| `rekabet_ihlali`         | Competition infringement         |
| `""` / `all` / `tumu`    | All types (no filter)            |

## CLI

```bash
python -m tools.search_rekabet --query "telekomünikasyon" --decision_type birlesme_devralma --limit 5
# or pipe JSON:
echo '{"query": "telekomünikasyon", "limit": 5}' | python -m tools.search_rekabet
```

## Output

```json
{
  "total": 123,
  "page": 1,
  "count": 1,
  "results": [
    {
      "id": "abc123-...-guid",
      "title": "Örnek karar başlığı",
      "type": "Rekabet İhlali",
      "date": "01.02.2024",
      "url": "https://www.rekabet.gov.tr/Karar?kararId=abc123-...-guid"
    }
  ]
}
```

## Notes

- This is a **best-effort** HTML scraper. The portal's markup (class names, table layout,
  the `Toplam : N` string, the `KararTuruID` GUIDs) can change without notice. **Verify the
  parsing against the live site** if results look empty or malformed, and adjust the
  selectors / GUID map in `client.py` accordingly.
- The offline test in `tests/tools/test_search_rekabet.py` mocks the HTTP layer with `respx`
  and asserts the parser against a representative HTML body — it does not hit the network.
