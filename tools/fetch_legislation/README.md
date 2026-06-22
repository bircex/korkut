# fetch_legislation

Fetch a piece of Turkish legislation's **full text as Markdown** via the Bedesten API
(`bedesten.adalet.gov.tr/mevzuat`). No key required. Handles both HTML and PDF source
documents, and can fetch either the whole legislation or a single article.

## Parameters
| Name | Type | Description |
|------|------|-------------|
| `mevzuat_id` | string (required) | Legislation `id` from a `search_mevzuat` result. |
| `madde_id` | string (optional) | Article id; when given, only that article is fetched. |

## CLI
```bash
python -m tools.fetch_legislation --mevzuat_id "12345"
# single article:
python -m tools.fetch_legislation --mevzuat_id "12345" --madde_id "67890"
# or pipe JSON:
echo '{"mevzuat_id": "12345"}' | python -m tools.fetch_legislation
```

## Output (stdout JSON)
```json
{
  "id": "12345",
  "title": "Türk Borçlar Kanunu",
  "markdown": "# Türk Borçlar Kanunu\n\nMADDE 1 ...",
  "sourceUrl": "https://mevzuat.adalet.gov.tr/mevzuat?mevzuatId=12345"
}
```
`title` is included only when the API supplies it.

## Endpoint
`POST https://bedesten.adalet.gov.tr/mevzuat/getDocumentContent`

Request body:
```json
{"data": {"documentType": "MEVZUAT", "id": "<mevzuat_id>"}, "applicationName": "UyapMevzuat"}
```
For a single article, `documentType` is `"MADDE"` and `id` is the `madde_id`. The response
returns the document body base64-encoded under `data.content` with a `data.mimeType`
(`text/html` or `application/pdf`), which is decoded and converted to Markdown.

> **Verify against the live API.** The article-level (`MADDE`) field names for the returned
> title (`maddeBaslik` / `baslik`) and the exact `mimeType` casing were inferred from the
> public endpoint's shape and may vary; the `title` key is therefore best-effort. The
> document body decoding (base64 + HTML/PDF → Markdown) is the load-bearing path and is
> covered by the offline test.

## Why it matters
This is the **legislation-reading** primitive: Korkut resolves a `search_mevzuat` hit to its
actual statutory text before quoting or relying on it — eliminating fabricated wording.
