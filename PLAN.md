# Korkut — Implementation Plan

> Status: Design approved, ready for setup. This document is the single source of truth for all decisions.

## 1. Purpose
A system that runs a law office's daily work (hearing reminders, petition drafts, legal
research) through a single assistant called **Korkut**, keeping the data in the office.
Korkut is a single identity on Mattermost; specialty modules (in parallel) run internally.

## 2. Principles
- **Data stays in the office.** The chat server (Mattermost) is on our own machine; `data/` never
  leaves to the outside/repository, it sits on an encrypted disk.
- **Everything going to the cloud is redacted first.** Name/TCKN/case file number → token. The reverse
  mapping is only on the server. (The rule is the same whether the provider is GPT or Claude.)
- **Single identity: Korkut.** The user talks to a single assistant. Internally the specialty modules
  (routing/hearing/petition/research) can run in parallel, but they all respond as Korkut. Reachable both
  from the channel (@Korkut) and **privately (DM)**.
- **No persistent action without approval.** Korkut summarizes what it will do, asks in natural language
  "do you approve?"; until approval is given, no hearing/petition/data is persisted. If a correction is
  requested, it applies it and resubmits for approval. (Details: ARCHITECTURE.md §9)
- **Model selection from config.** Each task → `local` or `cloud`; changed with a single line.
- **Provider is pluggable.** Today cloud = GPT; later Claude / local model.
- **Capabilities are tools.** Every external capability (case law/legislation search,
  text extraction...) is an independent tool under `tools/`: **runs standalone from the CLI**, carries its
  own documentation, and is **configured as a toolset** for Korkut. New capability = new folder. (Details: ARCHITECTURE.md §12)

## 3. Architecture

```
Mattermost (self-hosted — data doesn't leave the office)
   │   (a) channel: @Korkut / Korkut's thread      (b) direct message (DM): every message
   ▼
Dispatcher service  (single Python process, listens to channel+DM)
   │
   └─ Korkut (SINGLE identity) → router understands intent → hands to specialty module(s)
         "tomorrow's hearings"        → hearing module
         "reply petition draft"       → petition module     (several can run in PARALLEL)
         "is there a precedent ruling" → research module
   │   outputs merge into a SINGLE Korkut response
   ▼
core/  (router + redactor + db)
   ├─ model: local  → Ollama         (when hardware arrives; falls back to cloud when absent)
   └─ model: cloud  → redact → GPT   (later Claude/others)
```

**Key rule:** The user sees a **single identity, Korkut**; every message gets a single Korkut response.
Internally the specialty modules (in parallel) run, and they all speak as Korkut.

> Note: **Multi-channel/multi-office** can be added later; for now we start with a single channel + DM.

## 4. Korkut (single identity) and specialty modules

A single bot on Mattermost: **Korkut**. Internally there are these modules (not visible to the user):

| Module | Job | Model |
|-------|----|-------|
| Router | Understands intent → hands to module + general chat | cloud |
| Hearing | Reminders, calendar (add/list) | local → for now cloud |
| Petition | Template + draft generation | cloud |
| Research | Legislation/case law search + summary | cloud |

## 5. Directory structure

```
korkut/
├─ PLAN.md                     # this document
├─ config/
│  ├─ model-routing.yaml       # backend selection + specialty→model
│  ├─ korkut.yaml              # Korkut persona + specialty modules + bot
│  └─ redaction-rules.yaml     # fields to redact
├─ data/                       # .gitignore — never leaves the outside
│  └─ files/<case-no>/      # petitions, attachments, rulings
│                              # (structured data in Postgres: asistan-db)
├─ core/
│  ├─ router.py                # model selector (local/cloud), fallback rule
│  ├─ agent_loop.py            # provider-agnostic tool-calling loop (same skillset for every model)
│  ├─ redactor.py              # redact / reverse
│  ├─ providers/               # gpt.py + claude.py (step() only); ollama.py later
│  ├─ tool_registry.py         # reads tools/ manifests → gives Korkut a toolset
│  └─ db.py
├─ tools/                      # Korkut's CAPABILITIES — one lightweight tool per source, CLI-runnable
│  ├─ README.md                # index of all tools + CLI contract
│  ├─ _base/                   # shared base client (httpx + rate-limit + cache)
│  ├─ search_yargitay/         # Yargıtay search (Bedesten)
│  │   ├─ __main__.py          #   CLI: python -m tools.search_yargitay --query "..."
│  │   ├─ tool.yaml            #   name/description/params + requires_key (function-calling)
│  │   └─ README.md            #   documentation (sample input/output)
│  ├─ search_danistay/         # Danıştay search
│  ├─ search_emsal/            # UYAP Emsal search
│  ├─ search_mevzuat/          # mevzuat.gov.tr search
│  ├─ fetch_decision/          # decision full text (Markdown)
│  ├─ fetch_legislation/       # legislation full text (Markdown)
│  └─ ...                      # new source/capability = new folder (same contract)
├─ bridge/
│  └─ mattermost.py            # channel+DM listener + Korkut dispatcher + single bot identity
├─ korkut/                     # single identity; specialty modules inside
│  ├─ persona.py               # Korkut's single system instruction
│  ├─ dispatcher.py          # intent classification → dispatch to module
│  ├─ hearing.py               # hearing module
│  ├─ petition.py               # petition module
│  └─ research.py             # research module
├─ templates/petition/          # Turkish law petition templates (.docx/.md)
└─ scripts/cron/               # hearing reminder job
```

## 6. Config examples

`config/model-routing.yaml`
```yaml
backends:
  cloud: gpt          # for now OpenAI; later: claude / others
  local: ollama       # active when hardware arrives

# When Ollama is off, jobs requesting "local" automatically fall back to cloud + are logged.
fallback_to_cloud_if_no_local: true

specialties:
  dispatcher: { model: cloud }
  hearing:    { model: local }
  petition:   { model: cloud }
  research:   { model: cloud }
```

`config/korkut.yaml`
```yaml
bot:
  name: "Korkut"             # the single bot identity on Mattermost
  channel: office            # main channel (DM is also always open)
persona_file: korkut/persona.py                         # single system instruction
specialties: [dispatcher, hearing, petition, research]  # internal; not visible to the user
```

## 7. Redaction
- **Detection:** TCKN (11 digits + algorithm check), phone, IBAN, license plate, case file number
  (regex) + name (Turkish name dictionary + rules; later local NER).
- **Redact:** `Ahmet Yılmaz → [PERSON_1]`; mapping in `data/redaction-maps/`.
- **Reverse:** Tokens in the cloud response are replaced with the real values.
- **Rule:** `local` → not redacted (it doesn't leave anyway). `cloud` → **always** redacted.

## 8. Phases

- **Phase 0 — Server & foundation:** Ubuntu 24.04 + LUKS disk encryption, Python 3.12 venv,
  Mattermost (Docker) + application Postgres (asistan-db), OpenAI API key (in env),
  directory skeleton, Postgres schema.
  **Branding:** the setup script asks "Office name?" → writes it to Mattermost `SiteName`
  (mmctl/config.json), so the office name appears everywhere instead of "Mattermost".
  *(No Ollama — a separate mini-phase when hardware arrives.)*
- **Phase 1 — core:** Model router (+ "fall back to cloud when local is absent" rule) + redaction + DB +
  **tool registry** (`tools/` contract: CLI + tool.yaml → agent toolset).
  Test: no real name/TCKN goes to the cloud; sample tool runs from the CLI.
- **Phase 2 — Mattermost bridge:** Channel + **DM** listener, dispatch to specialty module by intent,
  **single Korkut bot identity**. (Parallel modules inside, a single Korkut outside.)
- **Phase 3 — Hearing module:** Manual hearing entry + cron reminder → Korkut writes "tomorrow's
  hearings" to the channel/DM.
- **Phase 4 — Petition module:** Template + cloud draft (redacted) → `.docx` via `python-docx` →
  under `data/files/` with an "awaiting approval" label.
- **Phase 5 — Research module (RAG):** Build one lightweight data tool per source under
  `tools/` — `search_yargitay`, `search_danistay`, `search_emsal`, `search_mevzuat`,
  `fetch_decision`, `fetch_legislation` (our own httpx clients over shared `tools/_base/`).
  Each runs cleanly from the CLI + tool.yaml (+ `requires_key`) + README. **Corpus strategy: RAG** (live search →
  fine ranking → synthesis → **citation verification** + lazy cache + polite rate-limit). Later a
  focused local index. Details: CORPUS.md. Sources: SOURCES.md.
- **Later:** Local model (Ollama + GPU), UYAP integration, authorization/multi-user.

## 9. Technology stack (Python)

| Job | Library |
|----|-----------|
| Cloud LLM | `openai` (later `anthropic`) |
| Local LLM (later) | `ollama` |
| Chat bridge | `mattermostdriver` (WebSocket) |
| DB | Postgres (`psycopg` / `SQLAlchemy`) |
| Petition (Word) | `python-docx` |
| Scheduling | system `cron` (+ `APScheduler` option) |
| Research fetching | `httpx` + `selectolax` / `beautifulsoup4` |
| Redaction | regex + name dictionary (later local NER) |

## 10. KVKK (Turkish data protection law) / legal checklist
- [ ] Clarify data processing terms / DPA status with OpenAI.
- [ ] Client disclosure text + explicit consent template (processing with AI).
- [ ] Compliance with bar association advertising/ethics rules.
- [ ] Disk encryption (LUKS) + access control + backup policy.
- [ ] Redaction tests (proof of no PII leakage).

## 11. Open questions
- **Notification:** Hearing reminder only to the Mattermost channel, or also email?
- **UYAP:** Is there official API access, or is it semi-automatic? (Separate plan after the phases.)
- **Multi-user:** Single lawyer or a team? (Is authorization needed?)
- **Multi-office (later):** Each office = one channel; add office as an organization dimension.

## 12. Scope decisions (fixed)
- **Runtime engine: our own Python framework** — NOT clawbot/Claude Code (that is only a
  development tool; once the product is set up it runs independently). The agent loop is a **thin layer**:
  official provider SDKs (`openai`...) + our own small loop; no heavy framework. (ARCHITECTURE.md §13)
- **Branding: Level 1** — the office name is asked during setup, written as the Site Name.
  Free, no license issue. Logo/white-label later (if it becomes a product).
- **Deployment: single office** — for now, setup at our own office. The multi-office distributed package comes later.
- **White-label license note:** In the open-source (Team) edition, rebranding the code is allowed;
  "Custom Branding" is closed in Enterprise (edit the webapp yourself), the AGPL sharing
  obligation applies only in the case of *distribution/service* — it does not apply at a single office.

## 13. Hardware (for the local model, later)
Target when moving to the local model: a GPU with ~24 GB VRAM (second-hand RTX 3090 / 4090) →
`qwen2.5:32b` gives the closest local result to the cloud for Turkish legal text.
For now **zero hardware investment**, start via GPT.
