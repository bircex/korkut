# fetch_decision

Fetch a court decision's **full text as Markdown** via the Bedesten API
(`bedesten.adalet.gov.tr`). No key required. Handles both HTML and PDF source documents.

## Parameters
| Name | Type | Description |
|------|------|-------------|
| `document_id` | string (required) | `documentId` from a `search_*` tool result. |

## CLI
```bash
python -m tools.fetch_decision --document_id "doc-1"
```

## Output (stdout JSON)
```json
{
  "documentId": "doc-1",
  "mimeType": "text/html",
  "sourceUrl": "https://mevzuat.adalet.gov.tr/ictihat/doc-1",
  "markdown": "# Yargıtay 1. Hukuk Dairesi\n\nEsas No: 2023/123 ..."
}
```

## Why it matters
This is the **citation-verification** primitive: before Korkut presents a cited decision,
it fetches the document by id to confirm it exists — eliminating fabricated citations.
