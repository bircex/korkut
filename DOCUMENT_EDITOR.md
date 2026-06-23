# Document Editor — Architecture (design)

> Status: **design only** (not yet built). This is the agreed blueprint for the petition-drafting
> editor. Build order is in §10. Decisions locked with the user: **Word-like WYSIWYG editor**,
> **UDF export for UYAP** (plus DOCX/PDF), **propose → approve/revert** editing, full versioning.

## 1. Goal
A "Canvas" workspace where Korkut **drafts and edits legal documents** (primarily petitions —
*dilekçe*) and the lawyer reviews/edits them. The screen splits: **chat on one side, the document
editor on the other**. Korkut writes into the editor and proposes targeted edits; the lawyer
accepts or reverts each change. The finished document exports to **UDF (UYAP), DOCX and PDF**.

This sits on the existing stack: the agent loop (tool-calling), the redactor (cloud chokepoint),
the shared session workspace, and the `Petition`/`Case` models in `core/db.py`.

## 2. UX
- **Split view:** chat pane + document pane. Desktop = side by side; mobile = tab toggle
  (Sohbet / Belge), since both can't fit a narrow screen.
- **Editor:** Word-like WYSIWYG (bold/italic, headings, alignment, numbered/bulleted lists,
  page-oriented view). Built on **TipTap (ProseMirror)** — gives a structured document model,
  a familiar toolbar, and a clean schema we control (no arbitrary HTML).
- **Korkut edits surface as a diff:** changed/added/removed runs are highlighted in place with
  **Kabul et / Geri al** controls (per change and "tümünü kabul"). Nothing Korkut writes becomes
  final without the lawyer — the project invariant *onaysız kalıcı işlem yok*.
- **Version history:** a dropdown of versions (who/when/note), with restore. Every edit — by
  Korkut or the lawyer — is a version.
- **Export bar:** UDF · DOCX · PDF.

## 3. Document model
TipTap/ProseMirror **document JSON** is the source of truth (structured blocks: heading,
paragraph, list, etc.). Two derived projections:
- **Text projection** (plain text / lightweight markdown) — what Korkut reads and edits. Keeps
  the model's job simple and diffable, and is what the redactor operates on.
- **Export renderers** — JSON → UDF / DOCX / PDF (§7).

Why structured JSON rather than raw HTML: we control the schema (no script/style injection →
no stored XSS), block identity enables precise targeted edits and diffs, and it maps cleanly to
both UDF paragraphs and DOCX paragraphs.

## 4. How Korkut writes/edits — document tools
When a session has an open document, the agent loop is given document tools (same mechanism as
the legal-search tools):
- `belge_olustur(baslik, govde)` — create/replace the whole document (first draft from a template).
- `belge_duzenle(hedef, yeni)` — targeted replace of a block/range (find the run → propose new text).
- `bolum_ekle(konum, govde)` — insert/append a section.
- `belge_oku()` — return the current text projection (so Korkut can reason over the latest state).

Each tool call does **not mutate the live document directly**; it creates a **ProposedEdit**
(a diff against the current version). The chat reply explains the change ("Sonuç ve İstem'e
Yargıtay 9. HD kararını ekledim"); the editor shows the diff for accept/revert. Accepting writes
a new `DocumentVersion`.

## 5. Redaction / KVKK (the critical part)
Korkut must *see* the document to edit it, so the **text projection joins the cloud-bound
context and passes through `core/redactor.py`** (TCKN / isim / dosya-no → token), exactly like
chat and uploaded files. Consequences:
- Korkut always works on the **redacted** projection. Its `belge_duzenle(hedef, yeni)` targets and
  produces redacted text.
- The server **restores tokens** (the per-request mapping) before turning a ProposedEdit into a
  real document change. Matching (`hedef` → block) is done on the redacted projection, applied to
  the real block via the mapping.
- Net: no un-redacted client data leaves the office; the lawyer always sees real text.

## 6. Data model (new tables, alongside `core/db.py`)
- `Document(id, conversation_id, case_id?, title, doc_type, content_json, status, updated_at)`
- `DocumentVersion(id, document_id, content_json, author "korkut|<user>", note, created_at)`
- `ProposedEdit(id, document_id, base_version_id, diff/new_content_json, status "pending|accepted|reverted", created_at)`
- Links: a finalized `Document` produces/updates a `Petition` (existing: draft → pending_approval
  → approved) and stores the exported file reference.

## 7. Export
JSON → renderers, server-side, downloaded as `attachment` (same hardening as uploads):
- **DOCX** — `python-docx` (already a dependency). Map headings/paragraphs/lists/alignment.
- **PDF** — from the DOCX or directly (e.g. a headless renderer); secondary priority.
- **UDF (UYAP Doküman Formatı)** — **headline export for Turkish courts**. A `.udf` is a ZIP
  containing `content.xml`: the full document text stored once (CDATA) plus an `<elements>` tree
  whose `<paragraph>`/`<content>` nodes reference `startOffset`/`length` into that text with
  style attributes, plus `<styles>` and `<pageFormat>`. Plan: a `web/export/udf.py` that walks the
  document JSON, emits the offset-indexed `content.xml`, and zips it.
  - **Risk / open item:** the UDF schema is not officially published; validate the emitter against
    a real UYAP Editör sample before trusting it, or adopt an existing UDF library. Flag in §11.

## 8. Backend endpoints (sketch)
- `POST /api/sessions/{cid}/document` — create a document (optionally from a template) for a session.
- `GET  /api/documents/{id}` — current content + version list.
- `POST /api/documents/{id}/edits/{eid}/accept` · `/revert` — apply or drop a proposed edit.
- `PUT  /api/documents/{id}` — save the lawyer's manual edit (new version).
- `GET  /api/documents/{id}/export?format=udf|docx|pdf` — download.
- Document tools run inside the agent turn (no separate endpoint); they emit ProposedEdits.

## 9. Templates (dilekçe)
`templates/petition/<type>.json|md` skeletons: *dava dilekçesi, cevap dilekçesi, temyiz/istinaf,
icra takibi, ihtarname …* Standard sections: Mahkeme başlığı · Taraflar · Konu · Açıklamalar ·
Hukuki Sebepler · Deliller · Sonuç ve İstem · Tarih/İmza. Korkut fills a template using the legal
research tools (case law / legislation) + `Case`/`Client` data, then drafts into the document.

## 10. Build order (phases)
1. **Data + tools:** `Document`/`DocumentVersion`/`ProposedEdit` models; document tools in the
   agent loop; redaction round-trip for the text projection. (Backend, fully testable.)
2. **Editor UI:** TipTap WYSIWYG in a split pane; load/save; manual-edit versioning.
3. **Proposed-edit diff UX:** highlight + accept/revert; version history.
4. **Export:** DOCX first, then **UDF** (with schema validation), then PDF.
5. **Templates + petition workflow:** template picker; draft-from-research; link to `Petition`
   (draft → pending_approval → approved).

## 11. Risks / open items
- **UDF schema** is reverse-engineered — must be validated against a genuine UYAP sample (§7).
- **Redacted-text editing** mapping needs care so targeted edits land on the right block.
- **Rich-content safety:** constrain the TipTap schema (no raw HTML/script) → no stored XSS.
- **Token cost:** sending the whole document each turn is expensive on large drafts; consider
  sending only the relevant section for targeted edits.
