# Legal Data Sources — Access List

> Raw material for Korkut's research and petition (dilekçe) modules. In Turkey there is
> **almost no official, documented, SLA-backed open API**; access is mostly through the JSON
> endpoints behind publicly accessible portals (semi-official), scraping, or commercial
> provider APIs.

## 0. Strategy — don't write from scratch, use the ready-made layer
Two open-source projects solve 90% of the work, and they also document the actual endpoints underneath:
- **yargi-mcp** — all judicial decisions + administrative board decisions. github.com/saidsurucu/yargi-mcp
- **mevzuat-mcp** — legislation (mevzuat) (laws/KHK/regulations/presidential decrees...).
  github.com/saidsurucu/mevzuat-mcp
- Both are **FastMCP** (Python). Korkut either consumes them as MCP, or calls the
  endpoints underneath (especially the **Bedesten API**) directly.
- **Cloned:** `vendor/yargi-mcp`, `vendor/mevzuat-mcp` (MIT). Review + vendor source.

### Verified from the code — NO TOKEN REQUIRED
- Data access is via **plain httpx**, with **no auth** (`bedesten_client.py`: "Pure httpx - no
  authentication or Playwright needed"). Browser-impersonating headers + a JSON wrapper are enough.
- The Clerk/Stripe/JWT/Redis/OpenRouter keys in `.env` are **not for data** — they are for the
  author's paid SaaS layer (login/payment/session). We skip them with `ENABLE_AUTH=false`.
- Only optional one: legislation "semantic" search → OpenRouter **or fully local embedding**
  (HF TEI / Ollama, no external service). If local is chosen, the "data stays in the office" principle is preserved.

## 1. Judicial decisions (case law / içtihat)

| Source | What it contains | Official portal | Access |
|--------|-----------|--------------|--------|
| **Bedesten API** ⭐ | Yargıtay + Danıştay + local court + appellate + KYB (5 types, combined) | `bedesten.adalet.gov.tr` | Semi-official JSON, **no auth** |
| **Yargıtay** | Yargıtay decisions | `karararama.yargitay.gov.tr` | JSON endpoint (native + Bedesten) |
| **Danıştay** | Danıştay decisions | `karararama.danistay.gov.tr` | JSON endpoint (keyword + detailed + Bedesten) |
| **UYAP Emsal** | Local + appellate precedent decisions (emsal karar) | `emsal.uyap.gov.tr` | UYAP JSON endpoint |
| **Anayasa Mahkemesi** | Norm review + individual application | `anayasa.gov.tr` | Official search endpoint |
| **Uyuşmazlık Mahkemesi** | Jurisdiction/judgment disputes | official URLs | Direct document URLs |

## 2. Administrative board/agency decisions

| Source | What it contains | Portal | Access |
|--------|-----------|--------|--------|
| **KİK** | Public Procurement Board decisions | `kik.gov.tr` | KİK v2 API, no auth |
| **Rekabet Kurumu** | Competition decisions | `rekabet.gov.tr` | Official endpoint |
| **Sayıştay** | Sayıştay decisions | `sayistay.gov.tr` | Official endpoint |
| **GİB** | Tax rulings (vergi özelgesi)/legislation | `gib.gov.tr` | Official JSON API |
| **KVKK** | Data protection decisions | `kvkk.gov.tr` | On-site search (Brave API key required) |
| **BDDK** | Banking regulation | `bddk.org.tr` | Search + URL (Brave API key) |
| **Sigorta Tahkim** | Insurance arbitration decisions | `sigortatahkim.org` | Tavily search + PDF (API key) |

> Note: KVKK/BDDK/Sigorta Tahkim have no official endpoint; access goes through third-party
> search (Brave/Tavily) → separate API key + more fragile. Low priority.

## 3. Legislation and presidential decrees (with mevzuat-mcp)

Via `mevzuat.gov.tr` (Presidential Legislation Information System), filtered by type:
- **KANUN, KHK, TÜZÜK, YÖNETMELİK, TEBLİĞ, KKY, UY, MÜLGA**
- **Presidential:** CB_KARARNAME, CB_KARAR, CB_YONETMELIK, CB_GENELGE
- In-title search, exact phrase, **Official Gazette (Resmî Gazete) date/issue filter**, fetching
  content as Markdown. (21 tools for mevzuat.gov.tr + 5 tools for bedesten.adalet.gov.tr.)
- A semantic search option is available (requires OPENROUTER_API_KEY).

### Official Gazette (Resmî Gazete) (additional)
- `resmigazete.gov.tr` — daily publication (presidential decrees (kararname), appointments, announcements).
- **No official open API** → archive scraping (date-based HTML/PDF). Most presidential decrees are
  already structured on mevzuat.gov.tr; use the Official Gazette only for "that day's raw publication".

## 4. Your own files / hearing dates (hearing module — SEPARATE category!)
These are not case law; they are the proceedings and hearing dates (duruşma günü) of **the office's own cases (dava)**.
- **UYAP Avukat Portal** (`avukat.uyap.gov.tr`) / **Vatandaş Portal**
  (`vatandas.uyap.gov.tr`).
- **NO official open API.** Login is via **e-Government (e-Devlet) / e-signature (e-imza) / mobile signature**; session-based.
- Result: Hearing-date automation becomes **semi-automatic** (logging in and scraping) or
  **manual entry** (in the plan, the start of Phase 3 is manual). Official integration is separate work.
- ⚠️ For automated access, mind the UYAP terms of use + KVKK.

## 5. International (if needed)
- **HUDOC (ECtHR/ECHR)** — `hudoc.echr.coe.int`. For Turkey decisions. A general
  search/access endpoint exists (to be verified). Valuable in human rights cases.

## 6. Commercial legal databases (paid, richest content)
Case law + legislation + literature + contract templates integrated. API/integration is usually
via an enterprise agreement; a standard self-serve API is not common — must be requested.
- **Lexpera** (`lexpera.com.tr`) — 1.6M+ Yargıtay decisions, full text of books/articles.
- **Kazancı** — case law + legislation automation.
- **HukukTürk**, **Jurix** (journals/articles).
> Decision: Go with the free official/semi-official sources first (Sections 1-3); if
> content/coverage falls short, evaluate a commercial API.

## 7. Risk / caution notes
- The "APIs" in Sections 1-3 are mostly **undocumented JSON endpoints discovered by
  reverse-engineering** → they can change without notice, no SLA. The MCP wrappers take on this
  maintenance, but breakage risk still exists.
- **Rate limit:** Bedesten measured ≈ **10 requests per 30 sec per IP** (429 + Retry-After);
  other endpoints undocumented. Be polite (token-bucket/backoff), avoid bulk crude fetching. "Download
  everything" is not realistic (millions = years). Strategy: KORPUS.md.
- **Terms of use:** For automated access, check each portal's ToS; especially
  UYAP (your own files) is sensitive.
- KVKK/BDDK/Sigorta Tahkim depend on third-party search → low priority, extra key.

## 8. Integration decision (for the research module)
- **Decision: vendor → tool** — cloned into `vendor/`; since it is MIT, we adapt the fetch clients
  (`bedesten_client.py`, `mevzuat_client.py`, etc.) into individual **CLI tools** under `tools/`:
  `ictihat_ara`, `mevzuat_ara`, `karar_metin` (each with CLI + tool.yaml +
  README, MIMARI.md §12). "Exactly the way the author fetches it" — no auth, just httpx.
- **License obligation:** Preserve the MIT copyright notice in the imported files.
- **Core target endpoints:** Bedesten API (case law + legislation), karararama.yargitay/danistay,
  emsal.uyap, anayasa, gib. KVKK/BDDK/Sigorta-Tahkim (dependent on 3rd-party search) → later.
- **Alternative (always open):** Run the MCPs as-is in Docker and make Korkut an MCP
  client. We chose the vendor path because it increases control; this remains a fallback.
- **Maintenance risk:** The endpoints are undocumented and rely on browser-impersonating headers →
  they can change. Once vendored, we track updates by diffing against the upstream in `vendor/`.
