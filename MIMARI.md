# Architecture — Technical Design

> PLAN.md is the roadmap; this document is the detail of *how it works*. All structural
> decisions to be clarified before starting setup are here.

## 1. Process topology (single Linux server)

```
┌─ Docker ─────────────────────────────┐
│  mattermost      (web + WebSocket)    │
│  mattermost-db   (Postgres, MM only)  │
└───────────────────────────────────────┘
        ▲ WebSocket (read) + REST (write)
        │
┌─ systemd service ────────────────────┐
│  asistan  (single Python process)     │
│    ├─ bridge/      MM listen (channel+DM)
│    ├─ korkut/      single identity; specialty modules inside (parallel)
│    └─ core/        router+redactor+db  │
└───────────────────────────────────────┘
        │
┌─ Docker ─────────────────────────────┐
│  asistan-db   (Postgres, application) │
└───────────────────────────────────────┘
        ▲
┌─ cron ───────────────────────────────┐
│  hatirlatma  (calls core every morning│
│              → writes hearing summary to MM)│
└───────────────────────────────────────┘
```

- **3 running parts:** Mattermost (Docker), `asistan` (Python service), `cron` (reminder).
- Mattermost uses its own Postgres; **application data lives in a separate Postgres**
  database (same server, different DB). Postgres from the start → ready to grow to
  multiple users/offices.
- All on the same machine; the only external dependency is the OpenAI API (with redacted text).

## 2. Mattermost bot identity — a single "Korkut"
- **Single bot account: Korkut** (single token). The user talks to one assistant.
- Inside there are specialty modules (dispatch, court hearing, petition, research); when
  needed they run **in parallel** but **all write as Korkut** → the user sees a single identity.
- The `asistan` service listens over a single WebSocket and writes as **Korkut** via REST.
- **Two surfaces:** (a) channel — `@Korkut` mention / Korkut's thread; (b) **direct message (DM)**
  — one-on-one conversation with Korkut.
- **Loop protection:** The service ignores Korkut's own messages.

## 3. Message flow (step by step)

```
1. The user writes in a channel or DMs Korkut.
2. bridge receives the message over WS. If it is Korkut's own message → ignore (stop).
3. Trigger:
     a) If DM → every message goes to Korkut (private chat; no mention needed).
     b) If @Korkut is mentioned in a channel OR it is a reply in Korkut's thread → goes to Korkut.
     c) Unmentioned in a channel → ignore (does not flood the channel, no cost).
4. Korkut's dispatcher understands the intent → routes to the relevant specialty module(s)
   (court hearing / petition / research); if more than one is needed they run IN PARALLEL.
5. The module fetches data from the DB; if an LLM is needed it passes (uzmanlik, is_tipi, text) to core.router.
6. core.router: specialty→model mapping.
     - model=local & Ollama down → fall back to cloud + log.
     - model=cloud → redactor.rumuzla → provider(gpt) → redactor.geri_ac.
7. The outputs merge into **a single Korkut response**; written to the thread/DM **as the Korkut identity**.
8. If there is a data write (court hearing/petition) → DB/file + approval status.
```

- **Context = Mattermost thread / DM sequence.** Korkut sees the messages of that
  sequence as context. Persistent information (case/client) is in the DB.

## 4. core contracts (interfaces)

```
router.cevapla(uzmanlik: str, is_tipi: str, mesajlar: list) -> str
    # select specialty→model, local/cloud, fallback rule; applies redaction internally.

redactor.rumuzla(metin: str) -> (rumuzlu_metin: str, harita: dict)
redactor.geri_ac(metin: str, harita: dict) -> str

providers.gpt.uret(mesajlar: list, ayar: dict) -> str      # OpenAI
providers.ollama.uret(...)                                  # later
providers.claude.uret(...)                                  # later

db: müvekkil / dava / duruşma / dilekçe CRUD (schema below)

tool_registry.yukle() -> list[Tool]          # scans tools/ and reads manifests
tool_registry.calistir(ad: str, args: dict) -> dict   # run a tool, return JSON
    # Tools live under tools/; can also be called from the CLI. Detail: §12.
```

- **Adding a provider = a file under `providers/` + one line in config.** The agents
  and bridge do not change at all.
- **Adding a new capability = a tool under `tools/`** (CLI + tool.yaml + README). §12.

## 5. Redaction flow (every text going to the cloud)

```
text ──> detection (regex + name dictionary)
            • TCKN (11 digits + algorithm)  • phone     • IBAN
            • plate                          • case file number (esas/dosya no)
            • person name (dictionary + rule)
       ──> replace with token:  "Ahmet Yılmaz" → "[KİŞİ_1]"
       ──> map: { "[KİŞİ_1]": "Ahmet Yılmaz", ... }  (in RAM, per request)
       ──> provider(gpt) call  (only redacted text is sent)
       ──> put the tokens back in the returned answer
```

- Text going to the `local` model **is not redacted** (it does not leave the machine anyway).
- The map is in RAM per request; if kept persistently (audit), encrypted in `data/`. [KARAR-3]

## 6. Data model (Postgres — first draft)

```sql
müvekkil(id, ad_soyad, tckn, telefon, eposta, adres, notlar, olusturma)
dava(id, dosya_no, müvekkil_id, mahkeme, dava_turu, karsi_taraf,
     durum, acilis_tarihi, notlar)
duruşma(id, dava_id, tarih, saat, yer, tur, sonuc,
        hatirlatma_durumu, notlar)
dilekçe(id, dava_id, tur, dosya_yolu,
        durum,            -- taslak | onay_bekliyor | onaylandi
        olusturan, olusturma)
```

- All sensitive fields are already on a LUKS-encrypted disk. Field-level extra encryption
  for the TCKN is optional (if needed). The `ofis` field will be added in the multi-office phase.

## 7. Korkut's internal structure (single identity, specialty modules)
The user sees a single **Korkut**. Inside, the dispatcher understands intent, runs the
relevant module(s) (may be parallel), and the outputs merge into **a single Korkut response**.

| Module (internal) | Job | LLM / tools | Data |
|-----------------|----|---------------|------|
| **dispatcher** | understand intent → hand to module, general chat | classification | — |
| **court hearing** | add/list hearing, reminder | date/entity extraction | duruşma, dava |
| **petition** | select template + generate draft → .docx | text generation | dava, dilekçe, şablon |
| **research** | search legislation/case law + summarize | tools/ (ictihat_ara, mevzuat_ara, karar_metin) | external portal API |

- Korkut has a **single persona/system instruction** (role, limits, "this is a draft" warning);
  the modules specialize within this persona.

## 8. Data entry (court hearing/client) — KARAR-2: Natural language + approval
- It is written to Korkut in natural language: "Korkut, Ahmet Yılmaz divorce case, July 15
  10:00 at the Antalya 3rd Family Court".
- Korkut **extracts** the fields and shows them as a **structured summary**.
- Before saving, it applies the **natural-language approval dialog in §9** — it does not
  write to the DB until the user approves; if the user says "fix this date" it corrects
  it and presents it for approval again.

## 9. Approval flow — KARAR-4: Natural-language dialog (for all persistent operations)
This applies not only to the petition, but to **every persistent/important operation**
(court hearing record, petition finalization, data change).

```
The agent prepares the work → SUMMARIZES what it will do → ASKS "Do you approve?"
   │   (status: onay_bekliyor — not yet persistent)
   ▼
The user responds in natural language:
   • "okay / fine / I approve / yes"          → the agent FINALIZES the operation
   • "no / stop / cancel"                      → it does NOT perform the operation, drops it
   • "fix this date / add the opposing party"  → the agent APPLIES THE CHANGE,
                                                  re-summarizes and asks for approval again
```

- The agent understands the approval/rejection/correction intent **from natural language** (no fixed command).
- **No persistent operation is performed without approval.** On an ambiguous answer it asks again.
- For the petition, additionally: the agent can **analyze and propose** the places that need
  to change ("the date is missing in this clause, would you like me to add it?").

## 10. Error / fallback behavior
- Ollama down + model=local → fall back to cloud, silent to the user, write to log.
- Provider error/timeout → the agent says "I can't reach it right now, could you try again"; it does not crash.
- If unsure about the redaction detection → **err on the safe side**: redact the suspect field too.
- **No persistent operation without approval** (§9). DB/file writes are first `onay_bekliyor`; they
  are only finalized with natural-language approval. This is a cross-cutting principle for all write operations.

## 11. Design decisions (closed)
- **KARAR-1 — Trigger:** ✅ In a channel only `@Korkut` / Korkut's thread; **every message
  in DM**. Unmentioned messages in a channel are ignored.
- **KARAR-2 — Data entry:** ✅ Natural language + structured summary + approval (§8).
- **KARAR-3 — Redaction map:** ✅ Default: in RAM only, deleted after the request
  (minimal PII trace). If audit is needed, an encrypted persistent log is added as an option.
- **KARAR-4 — Approval:** ✅ Natural-language dialog; no persistent operation without approval (§9).
- **KARAR-5 — Application DB:** ✅ Postgres from the start (separate database).

## 12. Tools layer (agent capabilities)
Each external capability is an **independent, CLI-runnable, self-documenting** tool under
`tools/`. Korkut (the specialty modules) uses these as a toolset.

### Folder contract (`tools/<ad>/`)
```
tools/search_yargitay/
  ├─ __main__.py    # CLI entry
  ├─ tool.yaml      # name, description, parameter schema (JSON Schema) → function-calling
  ├─ README.md      # what it does, example input/output, which endpoint it hits
  └─ client.py      # thin client over tools/_base/ (httpx client adapted from vendor/, MIT notice preserved)
```

### CLI contract (clean, agent-independent)
```
python -m tools.search_yargitay --sorgu "kira tespiti" --limit 10
  • Input: arguments or stdin JSON
  • Output: a SINGLE JSON object to stdout (for machines, not humans)
  • Log: stderr   • Success/error: exit code (0/≠0)
```
This way each tool is tested on its own; it works even without the agent.

### tool.yaml (example)
```yaml
ad: search_yargitay
aciklama: "Searches Yargıtay (Court of Cassation) decisions (Bedesten API)."
requires_key: null        # e.g. BRAVE_API_KEY for niche sources; null = no key needed
parametreler:
  sorgu: { tip: string, zorunlu: true }
  limit: { tip: integer, varsayilan: 10 }
```

### Registration and invocation (registry)
- `core/tool_registry.py` scans `tools/` at startup and reads each `tool.yaml`.
- The manifests are given to GPT as **function-calling tools** (config defines which
  agent can access which tool: `config/tools.yaml`).
- When GPT calls a tool, the registry runs it (in-process import; subprocess CLI if
  needed) and returns the JSON result to the model.

### config/tools.yaml (which specialty module accesses what)
```yaml
# Korkut is a single identity; tools are grouped internally by specialty.
durusma:   [durusma_ekle, durusma_listele]
dilekce:   [sablon_getir, dilekce_uret, search_yargitay, search_mevzuat, fetch_decision]
arastirma: [search_yargitay, search_danistay, search_emsal, search_mevzuat, fetch_decision, fetch_legislation]
```

### Privacy
- If the tool input is going to a cloud model, **redaction rules apply** (§5).
- Case law/legislation queries are public; but if client context is added, that part is redacted.

### Resource granularity (one lightweight tool per source)
- **Shared base client** `tools/_base/` (httpx + token-bucket rate-limit + cache) → each
  source client stays truly thin (just that endpoint + param mapping). Mirrors the vendor
  module-per-source layout.
- **One tool per source:** `search_yargitay`, `search_danistay`, `search_emsal`,
  `search_anayasa`, `search_kik`, `search_rekabet`, `search_sayistay`, `search_gib`,
  `search_mevzuat`, plus `fetch_decision` / `fetch_legislation`. Each has its own
  tool.yaml + README and is CLI-runnable.
- **Tiered exposure (avoid tool bloat):** the registry always offers the **core** tools
  (yargitay, danistay, emsal, mevzuat, fetch_*); **niche** ones (kik, rekabet, sayistay,
  gib, kvkk, bddk, sigorta_tahkim) load on demand when the query targets that domain → GPT's
  tool list stays small and selection stays sharp.

### Key handling (`requires_key`)
- A tool's `tool.yaml` declares any needed key (e.g. `requires_key: BRAVE_API_KEY`). At
  startup `tool_registry` checks the env; **if the key is missing the tool is disabled
  (never offered to GPT) + logged** → graceful degradation, the system never breaks.
- **Group A — no key (core):** Yargıtay, Danıştay, Emsal, Bedesten, Anayasa, KİK, Rekabet,
  Sayıştay, GİB, mevzuat.gov.tr → plain httpx.
- **Group B — needs a search provider:** KVKK, BDDK, Sigorta Tahkim → optional; prefer our
  own search/scrape over Brave/Tavily; deferred (core covers ~95%).
- **Group C — embedding:** semantic search → **local embedding** (HF TEI/Ollama), no
  external key, data stays in the office.
- Keys live in `.env` (never in the repo).

### First tool set (Phase 5)
`search_yargitay` + `search_mevzuat` + `fetch_decision`/`fetch_legislation` — adapted from
the `vendor/` clients (KAYNAKLAR.md §8). Other sources and the court-hearing/petition tools
follow the same contract. (Supersedes the earlier `ictihat_ara`/`mevzuat_ara`/`karar_metin`
sketch — now one tool per source.)

## 13. Runtime engine (framework decision)
- Korkut is **our own Python application**; not clawbot/Claude Code (those are only
  *development* tools — once the product is set up they are not needed, it is an independent service).
- The agent loop is a **thin layer**: official provider SDKs (`openai`; later `anthropic`,
  `ollama`) + our own small loop. **No heavy orchestration framework**
  (LangChain/LangGraph, etc.).
- The loop (provider-independent):
  ```
  messages + tool schemas → model
     ├─ if the model called a tool → tool_registry.calistir → append result → repeat
     └─ final text → return (write as the Korkut identity)
  ```
- All model calls go through `core/router` → redaction + provider selection +
  "fall back to cloud if no local" **at a single point**.
- `core/providers/<ad>.py` converts each provider's function-calling format into a
  **common interface** → GPT↔Claude↔local swap is a single file.
- **Why a thin layer:** (1) provider-swap is our priority, a heavy framework makes it harder;
  (2) redaction guaranteed at a single chokepoint; (3) the loop is small (~150 lines), few
  dependencies = longevity; (4) tools are already in `tools/`+registry, the framework's tool
  abstraction is unnecessary.
