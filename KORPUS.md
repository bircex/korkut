# Corpus Strategy — Research & Petition Data Access

> Decision: **RAG now (query-time).** A phased roadmap for the future is below.
> Context: KAYNAKLAR.md (sources), RAKIPLER.md §2b (data is public).

## 0. Rate limit reality (the constraint that shapes the design)
- **Bedesten API** (measured, vendor 2026-05-08): **10 requests per 30 s per IP**
  (≈ 1 request/3 s steady; ≈ 20/min; ≈ 28.800/day theoretical 24/7). Returns 429 + `Retry-After`.
- For other endpoints (Yargıtay/Danıştay native, UYAP Emsal, mevzuat.gov.tr) the limit is undocumented →
  act with similar caution, verify by measuring.
- **Brute-force download math:** 3.5M decisions ÷ ~28.800/day ≈ **120+ days** (single IP, document
  only). 10–15M → **years**. → "Download everything" is unrealistic; it must be **focused**.
- **Legislation** is on the order of tens of thousands → **the entirety can be mirrored within days.**

## 1. Principles
- **RAG-first:** coarse filtering on the state's search (live), fine ranking + synthesis on our side.
- **Never brute-force bulk download:** rate-limit + ToS. Crawl is always polite (within the limit).
- **Citation verification mandatory:** every decision/article that GPT cites is confirmed against the API
  (fabricated citation = 0). This is our quality leap.
- **Phased:** an index built up from usage + focused; not "all of Türkiye" but "our own practice".

## 2. Phase A — RAG (to be implemented NOW)
Query flow (the same for research and petition-citation):
```
question → (live) top-N candidate decisions/legislation via the state search
        → rank the small candidate set by meaning (embed on-the-fly if needed)
        → GPT-sourced synthesis (summary + citations)
        → CITATION VERIFICATION: confirm each decision/article exists via the API
        → Korkut response (only with verified citations)
```
- **Lazy cache:** fetched document + metadata into the local DB → repeat calls are reduced (rate-limit friendly).
- **Polite rate limiter:** comply with Bedesten 10/30s; on 429 wait for `Retry-After`; avoid parallel
  repeated calls (token-bucket — ready-made logic exists in the vendor).
- Tools: `tools/ictihat_ara`, `tools/mevzuat_ara`, `tools/karar_metin` (MIMARI.md §12).

## 3. Phase B — Focused local index (medium term)
- **Mirror legislation FULLY:** since it is small, the entirety goes into the local DB + kept current →
  legislation queries become fully local, fast, and unlimited.
- **Focused index for case law:** grow the office's practice areas (e.g. divorce/enforcement/rent/criminal)
  in a targeted way from a polite nightly crawl plus the cache that builds up through usage → semantic search
  with **pgvector**.
- **Embedding:** case law (içtihat)/legislation (mevzuat) is PUBLIC → cloud embedding is KVKK-clean (not client
  (müvekkil) data); or local embedding once a GPU arrives (data never leaves). See KAYNAKLAR.md (local
  embedding option available in .env).
- Incremental update: new daily decisions/Resmî Gazete → add to the index.

## 4. Phase C — Broad corpus (only if needed)
- Reality: pulling 10M+ from a single IP takes **years**. Options:
  - (a) **Long-running/multi-IP polite crawl** (months-years; mind the ToS),
  - (b) **Licensed data** (Lexpera/Kazancı) — richest + old/non-digitized content,
  - (c) **Settling for focused coverage** (sufficient for most offices).
- GPU-backed local embedding + fully local semantic search.
- Tie this phase to the GPU/local-model phase; do not open it unless necessary.

## 5. Cross-cutting topics
- **Cache + incremental update** (in every phase).
- **Citation verification** (in every phase — the quality anchor).
- **Rate-limit measurement:** verify the limits periodically (they may change), tune from config
  (`BEDESTEN_RATE_*`).
- **ToS/legal:** respect each portal's terms for automated access; UYAP (one's own files) is separate.
- **DB once scaled:** Postgres + pgvector; document text + vector + metadata.

## 6. Summary
| Phase | What | When |
|-----|----|----|
| **A — RAG** | live search + fine ranking + synthesis + citation verification + lazy cache | **now** |
| **B — Focused index** | full legislation mirror + practice-area case law index (pgvector) | medium term |
| **C — Broad corpus** | long crawl / licensed data + GPU embedding | if needed (GPU phase) |
